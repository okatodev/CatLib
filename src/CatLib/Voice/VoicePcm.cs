using System;

namespace CatLib.Voice;

public static class VoicePcm
{
    public const int MinSampleRate = 11025;
    public const int MaxSampleRate = 48000;

    public static int Decode16(byte[] bytes, int byteCount, float[] samples)
    {
        if (bytes == null || samples == null)
        {
            return 0;
        }

        var count = Math.Min(Math.Min(byteCount, bytes.Length) / 2, samples.Length);
        for (var index = 0; index < count; index++)
        {
            samples[index] = (short)(bytes[2 * index] | (bytes[2 * index + 1] << 8)) / 32768f;
        }

        return count;
    }

    public static void Amplify(float[] samples, float gain)
    {
        if (samples == null || Math.Abs(gain - 1f) < 0.001f)
        {
            return;
        }

        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = Math.Max(-1f, Math.Min(1f, samples[index] * gain));
        }
    }

    public static (float SourceVolume, float SampleGain) Split(float volume)
    {
        if (float.IsNaN(volume) || volume <= 0f)
        {
            return (0f, 1f);
        }

        return volume <= 1f ? (volume, 1f) : (1f, volume);
    }

    public static int PlaybackRate(int outputRate)
    {
        if (outputRate <= 0)
        {
            return MaxSampleRate;
        }

        return Math.Max(MinSampleRate, Math.Min(MaxSampleRate, outputRate));
    }

    public static float Peak(float[] samples, int count)
    {
        var peak = 0f;
        if (samples == null)
        {
            return peak;
        }

        for (var index = 0; index < Math.Min(count, samples.Length); index++)
        {
            peak = Math.Max(peak, Math.Abs(samples[index]));
        }

        return peak;
    }
}
