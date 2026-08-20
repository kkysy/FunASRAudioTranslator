using NAudio.Wave;

namespace LiveCaptionsTranslator.asr
{
    internal sealed class MonoSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly int sourceChannels;
        private float[] sourceBuffer = Array.Empty<float>();

        public WaveFormat WaveFormat { get; }

        public MonoSampleProvider(ISampleProvider source)
        {
            this.source = source;
            sourceChannels = source.WaveFormat.Channels;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (sourceChannels == 1)
                return source.Read(buffer, offset, count);

            int required = count * sourceChannels;
            if (sourceBuffer.Length < required)
                sourceBuffer = new float[required];

            int samplesRead = source.Read(sourceBuffer, 0, required);
            int framesRead = samplesRead / sourceChannels;

            for (int frame = 0; frame < framesRead; frame++)
            {
                float sum = 0;
                int frameOffset = frame * sourceChannels;
                for (int channel = 0; channel < sourceChannels; channel++)
                    sum += sourceBuffer[frameOffset + channel];
                buffer[offset + frame] = sum / sourceChannels;
            }

            return framesRead;
        }
    }
}
