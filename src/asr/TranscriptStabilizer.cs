using System.Text;
using System.Text.RegularExpressions;

namespace LiveCaptionsTranslator.asr
{
    internal readonly record struct TranscriptUpdate(
        string FinalizedText,
        string PartialText,
        string Snapshot,
        bool Advanced);

    internal sealed partial class TranscriptStabilizer
    {
        private static readonly HashSet<char> StrongBoundaries =
            ['。', '！', '？', '.', '!', '?', ';', '；', ':', '：', '\n'];
        private static readonly HashSet<char> SoftBoundaries = [' ', '\t', ',', '，', '、'];

        private const int MinBoundaryCommitChars = 4;
        private const int TrailingStabilityBuffer = 4;
        private const int LongStableSegmentChars = 16;
        private const int MaxUncommittedTailChars = 240;
        private const int FinalizedDedupeTailChars = 1200;
        private const int MinMovingWindowOverlapChars = 3;
        private const int MinNormalizedOverlapChars = 8;

        private string committedText = string.Empty;
        private string previousTail = string.Empty;

        public string CommittedText => committedText;

        public TranscriptUpdate ProcessWindow(string windowText)
        {
            string cleanedWindow = CollapseInternalRepetitions(windowText.Trim());
            string snapshot = committedText +
                              StripLeadingOverlap(cleanedWindow, committedText, FinalizedDedupeTailChars);
            return ProcessSnapshot(snapshot);
        }

        public void Reset()
        {
            committedText = string.Empty;
            previousTail = string.Empty;
        }

        private TranscriptUpdate ProcessSnapshot(string snapshot)
        {
            snapshot = snapshot.Replace("\r\n", "\n");
            string currentTail = snapshot.StartsWith(committedText, StringComparison.Ordinal)
                ? snapshot[committedText.Length..]
                : snapshot;

            int commonPrefixLength = LongestCommonPrefixLength(previousTail, currentTail);
            string stablePrefix = currentTail[..commonPrefixLength];
            string finalizedText = string.Empty;
            string partialText = currentTail;
            bool advanced = false;

            if (stablePrefix.Length > 0)
            {
                int commitLength = GetCommitLength(stablePrefix);
                if (commitLength > 0)
                {
                    string candidate = stablePrefix[..commitLength];
                    finalizedText = Commit(candidate);
                    partialText = currentTail[candidate.Length..];
                    advanced = true;
                }
            }

            if (finalizedText.Length == 0)
            {
                int movingOverlap = SuffixPrefixOverlapLength(previousTail, currentTail);
                if (movingOverlap > 0 && previousTail.Length > movingOverlap)
                {
                    string candidate = previousTail[..(previousTail.Length - movingOverlap)];
                    finalizedText = Commit(candidate);
                    partialText = currentTail;
                    advanced = true;
                }
                else if (currentTail.Length >= MaxUncommittedTailChars)
                {
                    int commitLength = GetCommitLength(currentTail);
                    if (commitLength > 0)
                    {
                        string candidate = currentTail[..commitLength];
                        finalizedText = Commit(candidate);
                        partialText = currentTail[candidate.Length..];
                        advanced = true;
                    }
                }
            }

            previousTail = partialText;
            return new TranscriptUpdate(
                finalizedText,
                partialText,
                committedText + partialText,
                advanced);
        }

        private string Commit(string text)
        {
            string cleaned = CollapseInternalRepetitions(text);
            string finalized = StripLeadingOverlap(cleaned, committedText, FinalizedDedupeTailChars);
            committedText += finalized;
            return finalized;
        }

        private static int GetCommitLength(string candidate)
        {
            int strongBoundary = FindLastBoundary(candidate, StrongBoundaries);
            if (strongBoundary >= MinBoundaryCommitChars)
                return strongBoundary;

            if (candidate.Length > TrailingStabilityBuffer)
            {
                string stableWindow = candidate[..(candidate.Length - TrailingStabilityBuffer)];
                int softBoundary = FindLastBoundary(stableWindow, SoftBoundaries);
                if (softBoundary >= MinBoundaryCommitChars)
                    return softBoundary;
            }

            return candidate.Length >= LongStableSegmentChars
                ? candidate.Length - TrailingStabilityBuffer
                : 0;
        }

        private static int FindLastBoundary(string text, HashSet<char> boundaries)
        {
            for (int index = text.Length - 1; index >= 0; index--)
            {
                if (boundaries.Contains(text[index]))
                    return index + 1;
            }
            return 0;
        }

        private static int LongestCommonPrefixLength(string left, string right)
        {
            int length = Math.Min(left.Length, right.Length);
            int index = 0;
            while (index < length && left[index] == right[index])
                index++;
            return index;
        }

        private static int SuffixPrefixOverlapLength(string left, string right)
        {
            int maxLength = Math.Min(left.Length, right.Length);
            for (int length = maxLength; length >= MinMovingWindowOverlapChars; length--)
            {
                if (left.AsSpan(left.Length - length).SequenceEqual(right.AsSpan(0, length)))
                    return length;
            }
            return 0;
        }

        private static string StripLeadingOverlap(string transcript, string committed, int maxTailChars)
        {
            if (transcript.Length == 0 || committed.Length == 0)
                return transcript;

            string tail = committed[^Math.Min(committed.Length, maxTailChars)..];
            int maxLength = Math.Min(tail.Length, transcript.Length);

            for (int length = maxLength; length > 0; length--)
            {
                if (tail.AsSpan(tail.Length - length).SequenceEqual(transcript.AsSpan(0, length)))
                    return transcript[length..];
            }

            string normalizedTail = NormalizeForOverlap(tail);
            string normalizedHead = NormalizeForOverlap(transcript[..maxLength]);
            int normalizedMax = Math.Min(normalizedTail.Length, normalizedHead.Length);
            for (int length = normalizedMax; length >= MinNormalizedOverlapChars; length--)
            {
                if (!normalizedTail.AsSpan(normalizedTail.Length - length)
                        .SequenceEqual(normalizedHead.AsSpan(0, length)))
                    continue;

                int originalLength = MapNormalizedLengthToOriginal(transcript, length);
                return transcript[originalLength..];
            }

            return transcript;
        }

        private static string NormalizeForOverlap(string text)
        {
            var builder = new StringBuilder(text.Length);
            foreach (char character in text)
            {
                if (char.IsWhiteSpace(character) || OverlapPunctuation().IsMatch(character.ToString()))
                    continue;
                builder.Append(char.ToLowerInvariant(character));
            }
            return builder.ToString();
        }

        private static int MapNormalizedLengthToOriginal(string text, int normalizedLength)
        {
            int count = 0;
            for (int index = 0; index < text.Length; index++)
            {
                if (char.IsWhiteSpace(text[index]) || OverlapPunctuation().IsMatch(text[index].ToString()))
                    continue;
                count++;
                if (count >= normalizedLength)
                    return index + 1;
            }
            return text.Length;
        }

        private static string CollapseInternalRepetitions(string text)
        {
            string current = text;
            for (int pass = 0; pass < 8; pass++)
            {
                bool changed = false;
                int maxUnit = Math.Min(80, current.Length / 2);
                for (int unitLength = maxUnit; unitLength >= 4 && !changed; unitLength--)
                {
                    for (int start = 0; start + unitLength * 2 <= current.Length; start++)
                    {
                        string first = NormalizeForOverlap(current.Substring(start, unitLength));
                        string second = NormalizeForOverlap(current.Substring(start + unitLength, unitLength));
                        if (first.Length < 4 || first != second)
                            continue;
                        if (current.AsSpan(start, unitLength * 2).IndexOfAny("。！？.!?;；\n") >= 0)
                            continue;

                        current = current.Remove(start + unitLength, unitLength);
                        changed = true;
                        break;
                    }
                }
                if (!changed)
                    break;
            }
            return current;
        }

        [GeneratedRegex(@"[\s.,;:!?。，“”‘’；：！？、…""']")]
        private static partial Regex OverlapPunctuation();
    }
}
