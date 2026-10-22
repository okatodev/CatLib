using System;
using System.Collections.Generic;

namespace CatLib.Voice;

public enum VoiceCommand
{
    None,
    Play,
    Stop
}

public readonly struct VoiceBlock
{
    public VoiceBlock(int offset, float[] samples)
    {
        Offset = offset;
        Samples = samples;
    }

    public int Offset { get; }

    public float[] Samples { get; }

    public bool IsSilence => Samples == null;
}

public sealed class VoiceStep
{
    public VoiceCommand Command { get; internal set; }

    public List<VoiceBlock> Writes { get; } = new();
}

public sealed class VoiceStream
{
    private readonly float[] _queue;
    private int _head;
    private int _count;
    private long _read;
    private long _write;
    private int _lastPosition;
    private int _silent;

    public VoiceStream(int sampleRate, int blockSamples, int clipBlocks, int leadBlocks, int prefillBlocks, int maxQueuedBlocks, int idleBlocks)
    {
        if (sampleRate <= 0 || blockSamples <= 0 || clipBlocks <= leadBlocks + 1 || leadBlocks <= 0 || prefillBlocks <= 0 || maxQueuedBlocks <= prefillBlocks || idleBlocks <= 0)
        {
            throw new ArgumentException("The voice stream sizes do not fit together.");
        }

        SampleRate = sampleRate;
        BlockSamples = blockSamples;
        ClipBlocks = clipBlocks;
        LeadBlocks = leadBlocks;
        PrefillBlocks = prefillBlocks;
        MaxQueuedBlocks = maxQueuedBlocks;
        IdleBlocks = idleBlocks;
        _queue = new float[(maxQueuedBlocks + 2) * blockSamples * 2];
    }

    public static VoiceStream ForRate(int sampleRate) => new(sampleRate, Math.Max(1, sampleRate / 100), 100, 10, 6, 40, 60);

    public int SampleRate { get; }

    public int BlockSamples { get; }

    public int ClipBlocks { get; }

    public int ClipSamples => ClipBlocks * BlockSamples;

    public int LeadBlocks { get; }

    public int PrefillBlocks { get; }

    public int MaxQueuedBlocks { get; }

    public int IdleBlocks { get; }

    public int Queued => _count;

    public bool IsPlaying { get; private set; }

    public long Written => _write;

    public int Dropped { get; private set; }

    public int Gaps { get; private set; }

    public int Hitches { get; private set; }

    public void Push(float[] samples, int count)
    {
        if (samples == null || count <= 0)
        {
            return;
        }

        count = Math.Min(count, samples.Length);
        var start = 0;
        if (count > _queue.Length)
        {
            Dropped += count - _queue.Length;
            start = count - _queue.Length;
        }

        for (var index = start; index < count; index++)
        {
            if (_count == _queue.Length)
            {
                _head = (_head + 1) % _queue.Length;
                _count--;
                Dropped++;
            }

            _queue[(_head + _count) % _queue.Length] = samples[index];
            _count++;
        }

        var limit = MaxQueuedBlocks * BlockSamples;
        if (_count > limit)
        {
            Skip(_count - PrefillBlocks * BlockSamples);
        }
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
        IsPlaying = false;
        _silent = 0;
    }

    public VoiceStep Update(int position)
    {
        var step = new VoiceStep();
        if (!IsPlaying)
        {
            if (_count < PrefillBlocks * BlockSamples)
            {
                return step;
            }

            IsPlaying = true;
            _read = 0;
            _write = 0;
            _lastPosition = 0;
            _silent = 0;
            step.Command = VoiceCommand.Play;
            Fill(step, 0);
            return step;
        }

        var delta = position - _lastPosition;
        if (delta < 0)
        {
            delta += ClipSamples;
        }

        _lastPosition = Math.Max(0, Math.Min(ClipSamples - 1, position));
        _read += delta;
        var readBlock = _read / BlockSamples;
        if (_write <= readBlock)
        {
            Hitches++;
            _write = readBlock + 1;
        }

        Fill(step, readBlock);
        if (_silent >= IdleBlocks && _count == 0)
        {
            IsPlaying = false;
            _silent = 0;
            step.Command = VoiceCommand.Stop;
        }

        return step;
    }

    private void Fill(VoiceStep step, long readBlock)
    {
        while (_write < readBlock + LeadBlocks)
        {
            var samples = Take();
            if (samples == null)
            {
                _silent++;
                if (_count > 0)
                {
                    Gaps++;
                }
            }
            else
            {
                _silent = 0;
            }

            step.Writes.Add(new VoiceBlock((int)(_write % ClipBlocks) * BlockSamples, samples));
            _write++;
        }
    }

    private float[] Take()
    {
        if (_count == 0)
        {
            return null;
        }

        if (_count < BlockSamples && _silent == 0)
        {
            return null;
        }

        var block = new float[BlockSamples];
        var taken = Math.Min(BlockSamples, _count);
        for (var index = 0; index < taken; index++)
        {
            block[index] = _queue[(_head + index) % _queue.Length];
        }

        _head = (_head + taken) % _queue.Length;
        _count -= taken;
        return block;
    }

    private void Skip(int samples)
    {
        samples = Math.Min(samples, _count);
        _head = (_head + samples) % _queue.Length;
        _count -= samples;
        Dropped += samples;
    }
}
