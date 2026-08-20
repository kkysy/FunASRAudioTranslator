using System.Buffers.Binary;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LiveCaptionsTranslator.asr
{
    public sealed class FunAsrCaptionSource : IDisposable
    {
        private const int TargetSampleRate = 16000;
        private const int MaxWindowSeconds = 20;
        private const int ActiveTailSeconds = 3;
        private const int InferenceIntervalMilliseconds = 1500;
        private const float SilenceRmsThreshold = 0.001f;

        private readonly object audioLock = new();
        private readonly object stateLock = new();
        private readonly HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(90)
        };
        private readonly List<short> rollingSamples = [];
        private readonly TranscriptStabilizer stabilizer = new();

        private WasapiLoopbackCapture? capture;
        private BufferedWaveProvider? bufferedProvider;
        private WdlResamplingSampleProvider? resampler;
        private CancellationTokenSource? cancellation;
        private Task? inferenceTask;
        private long audioVersion;
        private long processedAudioVersion;
        private int transcriptSessionVersion;
        private string currentSnapshot = string.Empty;
        private string status = "Stopped";
        private bool disposed;

        public event Action<string>? StatusChanged;

        public string ServerUrl { get; set; }
        public string Language { get; private set; }

        public string CurrentSnapshot
        {
            get
            {
                lock (stateLock)
                    return currentSnapshot;
            }
        }

        public string Status
        {
            get
            {
                lock (stateLock)
                    return status;
            }
        }

        public FunAsrCaptionSource(string serverUrl, string language = "ja")
        {
            ServerUrl = serverUrl;
            Language = language;
        }

        public void Start()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (capture != null)
                return;

            try
            {
                SetStatus("Starting system audio capture...");

                capture = new WasapiLoopbackCapture();
                bufferedProvider = new BufferedWaveProvider(capture.WaveFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(3),
                    DiscardOnBufferOverflow = true,
                    ReadFully = false
                };

                ISampleProvider monoProvider = new MonoSampleProvider(bufferedProvider.ToSampleProvider());
                resampler = new WdlResamplingSampleProvider(monoProvider, TargetSampleRate);

                capture.DataAvailable += OnDataAvailable;
                capture.RecordingStopped += OnRecordingStopped;
                capture.StartRecording();

                cancellation = new CancellationTokenSource();
                inferenceTask = Task.Run(() => InferenceLoopAsync(cancellation.Token));
                SetStatus("Capturing system audio; waiting for FunASR...");
            }
            catch (Exception ex)
            {
                capture?.Dispose();
                capture = null;
                bufferedProvider = null;
                resampler = null;
                SetStatus($"Unable to capture system audio: {ex.Message}");
            }
        }

        public async Task RestartAsync(string serverUrl)
        {
            Stop();
            ServerUrl = serverUrl;
            lock (audioLock)
            {
                rollingSamples.Clear();
                audioVersion = 0;
                processedAudioVersion = 0;
            }
            lock (stateLock)
            {
                currentSnapshot = string.Empty;
                stabilizer.Reset();
            }
            Start();
            await ProbeAsync();
        }

        public void ChangeLanguage(string language)
        {
            Language = language;
            ResetTranscriptSession();
            SetStatus($"Recognition language changed to {GetLanguageName(language)}.");
        }

        public void ResetTranscriptSession()
        {
            Interlocked.Increment(ref transcriptSessionVersion);
            lock (audioLock)
            {
                rollingSamples.Clear();
                audioVersion = 0;
                processedAudioVersion = 0;
            }
            lock (stateLock)
            {
                currentSnapshot = string.Empty;
                stabilizer.Reset();
            }
        }

        public async Task<bool> ProbeAsync()
        {
            try
            {
                string url = ServerUrl.TrimEnd('/') + "/health";
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var response = await client.GetAsync(url, timeout.Token);
                response.EnsureSuccessStatusCode();
                SetStatus("FunASR connected; capturing system audio.");
                return true;
            }
            catch (Exception ex)
            {
                SetStatus($"FunASR unavailable: {ex.Message}");
                return false;
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs args)
        {
            try
            {
                if (bufferedProvider == null || resampler == null || args.BytesRecorded == 0)
                    return;

                bufferedProvider.AddSamples(args.Buffer, 0, args.BytesRecorded);
                int estimatedSamples = Math.Max(
                    256,
                    (int)Math.Ceiling(
                        args.BytesRecorded /
                        (double)Math.Max(1, capture!.WaveFormat.BlockAlign) *
                        TargetSampleRate /
                        capture.WaveFormat.SampleRate) + 32);

                float[] sampleBuffer = new float[estimatedSamples];
                int read;
                while ((read = resampler.Read(sampleBuffer, 0, sampleBuffer.Length)) > 0)
                {
                    lock (audioLock)
                    {
                        for (int index = 0; index < read; index++)
                        {
                            float sample = Math.Clamp(sampleBuffer[index], -1f, 1f);
                            rollingSamples.Add((short)Math.Round(sample * short.MaxValue));
                        }

                        int maxSamples = MaxWindowSeconds * TargetSampleRate;
                        if (rollingSamples.Count > maxSamples)
                            rollingSamples.RemoveRange(0, rollingSamples.Count - maxSamples);
                        audioVersion++;
                    }

                    if (read < sampleBuffer.Length)
                        break;
                }
            }
            catch (Exception ex)
            {
                SetStatus($"System audio capture failed: {ex.Message}");
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs args)
        {
            if (args.Exception != null)
                SetStatus($"System audio capture stopped: {args.Exception.Message}");
        }

        private async Task InferenceLoopAsync(CancellationToken token)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(InferenceIntervalMilliseconds));
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                    await TranscribeCurrentWindowAsync(token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task TranscribeCurrentWindowAsync(CancellationToken token)
        {
            short[] samples;
            long version;
            int sessionVersion = Volatile.Read(ref transcriptSessionVersion);

            lock (audioLock)
            {
                version = audioVersion;
                if (version == processedAudioVersion ||
                    rollingSamples.Count < TargetSampleRate / 2)
                    return;
                samples = rollingSamples.ToArray();
            }

            int recentLength = Math.Min(samples.Length, ActiveTailSeconds * TargetSampleRate);
            if (CalculateRms(samples.AsSpan(samples.Length - recentLength, recentLength)) < SilenceRmsThreshold)
                return;

            try
            {
                byte[] wav = BuildWave(samples);
                using var form = new MultipartFormDataContent();
                using var audioContent = new ByteArrayContent(wav);
                audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                form.Add(audioContent, "file", "system-audio.wav");
                form.Add(new StringContent(Language), "language");
                form.Add(new StringContent("true"), "itn");
                form.Add(new StringContent("true"), "vad_filter");
                form.Add(new StringContent("true"), "word_timestamps");

                string url = ServerUrl.TrimEnd('/') + "/inference";
                using HttpResponseMessage response = await client.PostAsync(url, form, token);
                string responseText = await response.Content.ReadAsStringAsync(token);
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {responseText}");

                using JsonDocument payload = JsonDocument.Parse(responseText);
                string windowText = payload.RootElement.TryGetProperty("text", out JsonElement textElement)
                    ? textElement.GetString()?.Trim() ?? string.Empty
                    : string.Empty;

                token.ThrowIfCancellationRequested();
                if (sessionVersion != Volatile.Read(ref transcriptSessionVersion))
                    return;
                processedAudioVersion = version;
                if (windowText.Length == 0)
                    return;

                TranscriptUpdate update;
                lock (stateLock)
                {
                    update = stabilizer.ProcessWindow(windowText);
                    currentSnapshot = update.Snapshot;
                }

                if (update.Advanced)
                {
                    lock (audioLock)
                    {
                        int keepSamples = ActiveTailSeconds * TargetSampleRate;
                        if (rollingSamples.Count > keepSamples)
                            rollingSamples.RemoveRange(0, rollingSamples.Count - keepSamples);
                    }
                }

                SetStatus("FunASR connected; transcribing system audio.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                SetStatus($"FunASR inference failed: {ex.Message}");
            }
        }

        private static float CalculateRms(ReadOnlySpan<short> samples)
        {
            if (samples.Length == 0)
                return 0;

            double sum = 0;
            foreach (short sample in samples)
            {
                double normalized = sample / (double)short.MaxValue;
                sum += normalized * normalized;
            }
            return (float)Math.Sqrt(sum / samples.Length);
        }

        private static byte[] BuildWave(ReadOnlySpan<short> samples)
        {
            const short channels = 1;
            const short bitsPerSample = 16;
            int dataLength = samples.Length * sizeof(short);
            byte[] wav = new byte[44 + dataLength];
            Span<byte> header = wav.AsSpan(0, 44);

            "RIFF"u8.CopyTo(header);
            BinaryPrimitives.WriteInt32LittleEndian(header[4..], 36 + dataLength);
            "WAVE"u8.CopyTo(header[8..]);
            "fmt "u8.CopyTo(header[12..]);
            BinaryPrimitives.WriteInt32LittleEndian(header[16..], 16);
            BinaryPrimitives.WriteInt16LittleEndian(header[20..], 1);
            BinaryPrimitives.WriteInt16LittleEndian(header[22..], channels);
            BinaryPrimitives.WriteInt32LittleEndian(header[24..], TargetSampleRate);
            BinaryPrimitives.WriteInt32LittleEndian(
                header[28..],
                TargetSampleRate * channels * bitsPerSample / 8);
            BinaryPrimitives.WriteInt16LittleEndian(header[32..], (short)(channels * bitsPerSample / 8));
            BinaryPrimitives.WriteInt16LittleEndian(header[34..], bitsPerSample);
            "data"u8.CopyTo(header[36..]);
            BinaryPrimitives.WriteInt32LittleEndian(header[40..], dataLength);

            Span<byte> data = wav.AsSpan(44);
            for (int index = 0; index < samples.Length; index++)
                BinaryPrimitives.WriteInt16LittleEndian(data[(index * 2)..], samples[index]);
            return wav;
        }

        public void Stop()
        {
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;

            if (capture != null)
            {
                capture.DataAvailable -= OnDataAvailable;
                capture.RecordingStopped -= OnRecordingStopped;
                try
                {
                    capture.StopRecording();
                }
                catch
                {
                }
                capture.Dispose();
                capture = null;
            }

            bufferedProvider = null;
            resampler = null;
            inferenceTask = null;
            SetStatus("Stopped");
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Stop();
            client.Dispose();
        }

        private void SetStatus(string value)
        {
            lock (stateLock)
                status = value;
            StatusChanged?.Invoke(value);
        }

        private static string GetLanguageName(string language)
        {
            return language == "en" ? "English" : "Japanese";
        }
    }
}
