using System;
using System.Linq;
using CatLib.Il2Cpp;
using CatLib.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Audio;

namespace CatLib.Voice;

internal sealed unsafe class VoiceOutput
{
    public const float MouthHeight = 0.6f;

    private readonly CatLogger _log;
    private readonly int _rate;
    private GameObject _object;
    private AudioSource _source;
    private AudioClip _clip;
    private Il2CppStructArray<float> _block;
    private Il2CppStructArray<float> _silence;
    private VoiceSound _sound = (VoiceSound)(-1);
    private float _full = -1f;
    private float _hear = -1f;
    private bool _placed;

    public VoiceOutput(ulong speaker, int rate, CatLogger log)
    {
        Speaker = speaker;
        _rate = rate;
        _log = log;
        Stream = VoiceStream.ForRate(rate);
    }

    public ulong Speaker { get; }

    public VoiceStream Stream { get; }

    public bool IsPlaying => Stream.IsPlaying;

    public bool HasGhost { get; private set; }

    public float Distance { get; private set; } = -1f;

    public void Push(float[] samples, int count) => Stream.Push(samples, count);

    public void Update(Transform root, AudioMixerGroup group, VoiceSound sound, float fullVolumeDistance, float hearDistance, float volume, Vector3? position, Vector3? listener)
    {
        if (!Stream.IsPlaying && Stream.Queued == 0)
        {
            return;
        }

        if (!Ensure(root, group))
        {
            Stream.Clear();
            return;
        }

        HasGhost = position.HasValue;
        var spatial = sound == VoiceSound.Spatial && position.HasValue;
        Place(spatial ? VoiceSound.Spatial : VoiceSound.Flat, fullVolumeDistance, hearDistance);
        if (spatial)
        {
            _object.transform.position = position.Value + Vector3.up * MouthHeight;
            Distance = listener.HasValue ? Vector3.Distance(listener.Value, _object.transform.position) : -1f;
        }
        else
        {
            Distance = -1f;
        }

        var (sourceVolume, gain) = VoicePcm.Split(volume);
        _source.volume = sourceVolume;
        var step = Stream.Update(Stream.IsPlaying ? _source.timeSamples : 0);
        foreach (var write in step.Writes)
        {
            Write(write, gain);
        }

        switch (step.Command)
        {
            case VoiceCommand.Play:
                _source.timeSamples = 0;
                _source.Play();
                break;
            case VoiceCommand.Stop:
                _source.Stop();
                break;
            default:
                if (Stream.IsPlaying && !_source.isPlaying)
                {
                    _source.Play();
                }

                break;
        }
    }

    public void Silence()
    {
        Stream.Clear();
        if (_source != null && _object != null)
        {
            _source.Stop();
        }
    }

    public void Destroy()
    {
        Stream.Clear();
        if (_object != null)
        {
            UnityEngine.Object.Destroy(_object);
        }

        if (_clip != null)
        {
            UnityEngine.Object.Destroy(_clip);
        }

        _object = null;
        _source = null;
        _clip = null;
    }

    private bool Ensure(Transform root, AudioMixerGroup group)
    {
        if (_object != null && _source != null && _clip != null)
        {
            return true;
        }

        try
        {
            _object = new GameObject("CatLib_Voice_" + Speaker);
            if (root != null)
            {
                _object.transform.SetParent(root, false);
            }

            _source = _object.AddComponent<AudioSource>();
            _clip = AudioClip.Create("CatLib voice " + Speaker, Stream.ClipSamples, 1, _rate, false);
            _source.clip = _clip;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.priority = 0;
            _source.dopplerLevel = 0f;
            _source.spread = 0f;
            if (group != null)
            {
                _source.outputAudioMixerGroup = group;
            }

            _block = new Il2CppStructArray<float>(Stream.BlockSamples);
            _silence = new Il2CppStructArray<float>(Stream.BlockSamples);
            _sound = (VoiceSound)(-1);
            _placed = false;
            return true;
        }
        catch (Exception exception)
        {
            _log.Error($"Creating the voice output of {Speaker} failed", exception);
            Destroy();
            return false;
        }
    }

    private void Place(VoiceSound sound, float full, float hear)
    {
        if (_placed && sound == _sound && Math.Abs(full - _full) < 0.01f && Math.Abs(hear - _hear) < 0.01f)
        {
            return;
        }

        _placed = true;
        _sound = sound;
        _full = full;
        _hear = hear;
        if (sound == VoiceSound.Flat)
        {
            _source.spatialBlend = 0f;
            return;
        }

        var curve = new AnimationCurve();
        foreach (var (distance, value) in VoiceRolloff.Points(full, hear))
        {
            curve.AddKey(distance, value);
        }

        _source.spatialBlend = 1f;
        _source.rolloffMode = AudioRolloffMode.Custom;
        _source.minDistance = 0.1f;
        _source.maxDistance = Math.Max(VoiceRolloff.MinHearDistance, Math.Min(VoiceRolloff.MaxHearDistance, hear));
        _source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
    }

    private void Write(VoiceBlock block, float gain)
    {
        if (block.IsSilence)
        {
            _clip.SetData(_silence, block.Offset);
            return;
        }

        var data = (float*)((byte*)_block.Pointer + Il2CppArrays.DataOffset);
        var count = Math.Min(block.Samples.Length, Stream.BlockSamples);
        for (var index = 0; index < count; index++)
        {
            var value = block.Samples[index] * gain;
            data[index] = value > 1f ? 1f : value < -1f ? -1f : value;
        }

        _clip.SetData(_block, block.Offset);
    }

    public static AudioMixerGroup FindGroup(CatLogger log)
    {
        try
        {
            if (!Singleton<AudioManager>.HasInstance())
            {
                return null;
            }

            var mixer = Singleton<AudioManager>.Instance.AudioMixer;
            if (mixer == null)
            {
                return null;
            }

            var groups = mixer.FindMatchingGroups(string.Empty);
            if (groups == null || groups.Length == 0)
            {
                groups = mixer.FindMatchingGroups("Master");
            }

            if (groups == null || groups.Length == 0)
            {
                log.Info("The game's mixer has no groups for voice, voice plays straight to the listener");
                return null;
            }

            var all = Enumerable.Range(0, groups.Length).Select(index => groups[index]).Where(item => item != null).ToList();
            var names = all.Select(item => item.name).ToList();
            var chosen = all.FirstOrDefault(item => VoiceMixer.IsVoiceGroup(item.name))
                         ?? all.FirstOrDefault(item => string.Equals(item.name, "Master", StringComparison.OrdinalIgnoreCase))
                         ?? all.FirstOrDefault();
            log.Info($"Voice plays through the mixer group {chosen?.name ?? "none"}, the game has {string.Join(", ", names)}");
            return chosen;
        }
        catch (Exception exception)
        {
            log.Warning($"Finding the game's mixer group for voice failed: {exception.Message}");
            return null;
        }
    }
}

public static class VoiceMixer
{
    public static bool IsVoiceGroup(string name) =>
        !string.IsNullOrEmpty(name) && new[] { "voice", "voip", "chat" }.Any(part => name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0);
}
