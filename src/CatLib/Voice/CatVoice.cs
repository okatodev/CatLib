using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using CatLib.Core;
using CatLib.Events;
using CatLib.Game.Events;
using CatLib.Logging;
using CatLib.Net;
using UnityEngine;
using UnityEngine.Audio;

namespace CatLib.Voice;

public enum VoiceSound
{
    Spatial,
    Flat
}

public static class CatVoice
{
    public const int Channel = 0x4356;
    public const int SendFlags = 1 | 32;
    public const int CaptureBufferBytes = 8 * 1024;
    public const int MaxReadsPerFrame = 8;
    public const double DrainSeconds = 1;
    public const double ForgetSeconds = 30;
    public const double StaleSeconds = 0.5;
    public const float DefaultHearDistance = 25f;
    public const float DefaultFullVolumeDistance = 3f;
    public const float MaxVolume = 2f;
    public const string SpeakingChangedName = "Voice.SpeakingChanged";
    public const float SpeakingLevel = 0.02f;
    public const string SampleFileName = "CatLib-voice-sample.bin";

    private static readonly Dictionary<ulong, VoiceOutput> Outputs = new();
    private static readonly Dictionary<ulong, double> LastHeard = new();
    private static readonly Dictionary<ulong, ushort> LastSequence = new();
    private static readonly Dictionary<ulong, float> PlayerVolumes = new();
    private static readonly HashSet<ulong> MutedPlayers = new();
    private static readonly HashSet<string> ReportedProblems = new();
    private static readonly HashSet<ulong> AnnouncedSpeakers = new();
    private static readonly VoiceActivity Activity = new();
    private static readonly VoiceRate Rate = new();
    private static readonly byte[] Capture = new byte[CaptureBufferBytes];
    private static byte[] _pcmBytes = new byte[VoicePcm.MaxSampleRate * 2];
    private static float[] _pcm = new float[VoicePcm.MaxSampleRate];
    private static CatLogger _log;
    private static ISteamVoice _steam;
    private static bool _steamTried;
    private static bool _recording;
    private static double _drainUntil = double.NegativeInfinity;
    private static ushort _sequence;
    private static int _rate;
    private static GameObject _root;
    private static AudioMixerGroup _group;
    private static bool _groupSearched;
    private static float _volume = 1f;
    private static float _hearDistance = DefaultHearDistance;
    private static float _fullVolumeDistance = DefaultFullVolumeDistance;
    private static bool _testTransmit;
    private static bool _deafened;
    private static VoiceSample _sampleRecording;
    private static double _sampleStartedAt;
    private static VoiceSample _replay;
    private static double _replayStartedAt;

    public static event Action<ulong, bool> SpeakingChanged;

    public static string OwnerId { get; private set; }

    public static bool IsEnabled => OwnerId != null;

    public static bool Transmitting { get; set; }

    public static bool IsRecording => _recording;

    public static bool Deafened
    {
        get => _deafened;
        set
        {
            if (_deafened == value)
            {
                return;
            }

            _deafened = value;
            if (value)
            {
                foreach (var output in Outputs.Values)
                {
                    output.Silence();
                }
            }
        }
    }

    public static float Volume
    {
        get => _volume;
        set => _volume = ClampVolume(value);
    }

    public static VoiceSound Sound { get; set; } = VoiceSound.Spatial;

    public static float HearDistance
    {
        get => _hearDistance;
        set => _hearDistance = Math.Max(VoiceRolloff.MinHearDistance, Math.Min(VoiceRolloff.MaxHearDistance, float.IsNaN(value) ? DefaultHearDistance : value));
    }

    public static float FullVolumeDistance
    {
        get => _fullVolumeDistance;
        set => _fullVolumeDistance = Math.Max(0f, float.IsNaN(value) ? DefaultFullVolumeDistance : value);
    }

    public static bool Loopback { get; set; }

    public static ulong LocalId
    {
        get
        {
            var id = SessionNetwork.Transport?.LocalId ?? 0;
            return id != 0 ? id : LocalSteamId;
        }
    }

    public static bool IsLocalSpeaking => Activity.IsSpeaking(LocalId);

    public static IReadOnlyList<ulong> Speakers => Activity.Speaking.OrderBy(id => id).ToList();

    public static int SampleRate => _rate;

    public static string SteamApiName => _steam?.Name;

    public static long PacketsSent { get; private set; }

    public static long PacketsReceived { get; private set; }

    public static long PacketsForwarded { get; private set; }

    public static long PacketsDropped { get; private set; }

    public static long BytesSent { get; private set; }

    private static ulong LocalSteamId { get; set; }

    public static void Enable(BasePlugin plugin)
    {
        if (plugin == null)
        {
            throw new ArgumentNullException(nameof(plugin));
        }

        var metadata = MetadataHelper.GetMetadata(plugin)
                       ?? throw new ArgumentException($"{plugin.GetType().FullName} has no BepInPlugin attribute", nameof(plugin));
        Enable(metadata.GUID);
    }

    public static void Enable(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            throw new ArgumentException("Mod id must not be empty.", nameof(modId));
        }

        if (OwnerId != null && OwnerId != modId)
        {
            throw new InvalidOperationException($"Voice is already enabled by {OwnerId}");
        }

        OwnerId = modId;
        _log?.Info($"Voice is enabled by {modId}, it travels to players who have that mod too");
    }

    public static void Disable()
    {
        if (OwnerId == null)
        {
            return;
        }

        _log?.Info($"Voice is disabled by {OwnerId}");
        OwnerId = null;
        Transmitting = false;
    }

    public static bool IsSpeaking(ulong steamId) => Activity.IsSpeaking(steamId);

    public static float PlayerVolume(ulong steamId) => PlayerVolumes.TryGetValue(steamId, out var volume) ? volume : 1f;

    public static void SetPlayerVolume(ulong steamId, float volume) => PlayerVolumes[steamId] = ClampVolume(volume);

    public static bool IsPlayerMuted(ulong steamId) => MutedPlayers.Contains(steamId);

    public static void SetPlayerMuted(ulong steamId, bool muted)
    {
        if (muted)
        {
            MutedPlayers.Add(steamId);
            if (Outputs.TryGetValue(steamId, out var output))
            {
                output.Silence();
            }
        }
        else
        {
            MutedPlayers.Remove(steamId);
        }
    }

    internal static bool TestTransmit
    {
        get => _testTransmit;
        set => _testTransmit = value;
    }

    internal static void Initialize(CatLogger log)
    {
        _log = log;
        _rate = VoicePcm.PlaybackRate(SafeOutputRate());
        BootstrapEvents.GameRestartStarted += Reset;
        BootstrapEvents.MainMenuLoaded += () => _groupSearched = false;
        BootstrapEvents.LevelLoadStarted += () => _groupSearched = false;
        PlayerEvents.PlayerDisconnected += Forget;
        CatLib.DevTools.DevMenu.Command("Voice", "Voice state in the log", Describe,
            "Writes whether Steam records, what was sent and received, and how every player's voice plays.");
        CatLib.DevTools.DevMenu.Toggle("Voice", "Hear yourself", () => Loopback && _testTransmit, value =>
            {
                Loopback = value;
                _testTransmit = value;
            },
            "Keeps the microphone on and plays your own voice back to you through Steam voice, without other players. Turn it off after the check.");
        CatLib.DevTools.DevMenu.Toggle("Voice", "Record a voice sample", () => _sampleRecording != null, RecordSample,
            "Keeps the microphone on and keeps what Steam records; turned off, it writes the sample to a file every game copy on this computer reads.");
        CatLib.DevTools.DevMenu.Command("Voice", "Send the voice sample", SendSample,
            "Sends the recorded sample to the other players as if you said it now, for a game copy that cannot use the microphone.");
    }

    internal static string SamplePath => System.IO.Path.Combine(Application.persistentDataPath, SampleFileName);

    private static void RecordSample(bool on)
    {
        if (on)
        {
            _sampleRecording = new VoiceSample();
            _sampleStartedAt = FrameLoop.Realtime;
            _testTransmit = true;
            _log.Info("Recording a voice sample, talk now");
            return;
        }

        var sample = _sampleRecording;
        _sampleRecording = null;
        _testTransmit = Loopback && _testTransmit;
        if (sample == null)
        {
            return;
        }

        try
        {
            System.IO.File.WriteAllBytes(SamplePath, sample.ToBytes());
            _log.Info($"Voice sample of {sample.Duration:0.0} s in {sample.Count} packet(s) written to {SamplePath}");
        }
        catch (Exception exception)
        {
            _log.Error($"Writing the voice sample to {SamplePath} failed", exception);
        }
    }

    private static string SendSample()
    {
        try
        {
            var sample = VoiceSample.FromBytes(System.IO.File.ReadAllBytes(SamplePath));
            if (sample.Count == 0)
            {
                return "The voice sample is empty";
            }

            _replay = sample;
            _replayStartedAt = FrameLoop.Realtime;
            _log.Info($"Sending the voice sample of {sample.Duration:0.0} s in {sample.Count} packet(s) from {SamplePath} to {string.Join(", ", Targets())}");
            return "Sending the voice sample";
        }
        catch (Exception exception)
        {
            _log.Warning($"Reading the voice sample from {SamplePath} failed: {exception.Message}");
            return "No voice sample, record one first";
        }
    }

    private static void ReplaySample(double now)
    {
        if (_replay == null)
        {
            return;
        }

        foreach (var data in _replay.Due(now - _replayStartedAt))
        {
            Transmit(data, now);
        }

        if (_replay.IsFinished)
        {
            _log.Info("The voice sample is sent");
            _replay = null;
        }
    }

    internal static void Update()
    {
        if (_log == null)
        {
            return;
        }

        var now = FrameLoop.Realtime;
        try
        {
            UpdateRecording(now);
            ReadCaptured(now);
            ReplaySample(now);
            ReceivePackets(now);
            UpdateOutputs(now);
            ExpireActivity(now);
        }
        catch (Exception exception)
        {
            Report("update", "Voice update failed", exception);
        }
    }

    internal static void Quit()
    {
        if (_recording && _steam != null)
        {
            try
            {
                _steam.Stop();
            }
            catch (Exception)
            {
            }
        }

        _recording = false;
    }

    internal static string Describe()
    {
        var text = new StringBuilder();
        text.AppendLine($"Voice: owner {OwnerId ?? "none"}, Steam API {_steam?.Name ?? (_steamTried ? "unavailable" : "not opened yet")}, plays at {_rate} Hz");
        text.AppendLine($"  transmitting {Transmitting}, recording {_recording}, deafened {Deafened}, loopback {Loopback}, sound {Sound}, hear distance {HearDistance:0.#} m, full volume up to {FullVolumeDistance:0.#} m, volume {Volume:0.##}");
        text.AppendLine($"  role {CatNetwork.Role}, local {LocalId}, sends to {string.Join(", ", Targets())}");
        text.AppendLine($"  sent {PacketsSent} packet(s), {BytesSent} byte(s); received {PacketsReceived}, forwarded {PacketsForwarded}, dropped {PacketsDropped + Rate.Dropped}");
        text.AppendLine($"  mixer group {(_group == null ? "none" : _group.name)}, speaking now: {string.Join(", ", Speakers.Select(Name))}");
        foreach (var output in Outputs.Values.OrderBy(item => item.Speaker))
        {
            var stream = output.Stream;
            text.AppendLine($"  {Name(output.Speaker)}: playing {output.IsPlaying}, queued {stream.Queued} sample(s), gaps {stream.Gaps}, hitches {stream.Hitches}, dropped {stream.Dropped}, " +
                            (output.HasGhost ? $"at the cat{(output.Distance >= 0 ? $", {output.Distance:0.#} m away" : "")}" : "no cat, flat") +
                            $", volume {PlayerVolume(output.Speaker):0.##}{(IsPlayerMuted(output.Speaker) ? ", muted" : "")}");
        }

        var result = text.ToString().TrimEnd();
        _log.Info(result);
        return "Voice state is in the log";
    }

    private static void UpdateRecording(double now)
    {
        var want = (IsEnabled && Transmitting) || _testTransmit;
        if (want == _recording)
        {
            return;
        }

        var steam = Steam();
        if (steam == null)
        {
            return;
        }

        try
        {
            if (want)
            {
                steam.Start();
                _recording = true;
                _drainUntil = double.NegativeInfinity;
                _log.Debug("Steam voice recording started");
            }
            else
            {
                steam.Stop();
                _recording = false;
                _drainUntil = now + DrainSeconds;
                _log.Debug("Steam voice recording stopped");
            }
        }
        catch (Exception exception)
        {
            Report("record", "Starting or stopping Steam voice recording failed", exception);
            _recording = false;
        }
    }

    private static void ReadCaptured(double now)
    {
        if (_steam == null || (!_recording && now > _drainUntil))
        {
            return;
        }

        for (var read = 0; read < MaxReadsPerFrame; read++)
        {
            int result;
            uint written;
            try
            {
                result = _steam.Read(Capture, out written);
            }
            catch (Exception exception)
            {
                Report("read", "Reading Steam voice failed", exception);
                return;
            }

            if (result == VoiceResults.NoData)
            {
                return;
            }

            if (result == VoiceResults.NotRecording)
            {
                _drainUntil = double.NegativeInfinity;
                return;
            }

            if (result != VoiceResults.Ok)
            {
                Report("read" + result, $"Steam voice gave no data: {VoiceResults.Name(result)}");
                return;
            }

            if (written == 0)
            {
                return;
            }

            OnCaptured((int)Math.Min(written, (uint)Capture.Length), now);
        }
    }

    private static void OnCaptured(int count, double now)
    {
        var data = new byte[count];
        Buffer.BlockCopy(Capture, 0, data, 0, count);
        _sampleRecording?.Add(now - _sampleStartedAt, data);
        Transmit(data, now);
    }

    private static void Transmit(byte[] data, double now)
    {
        var local = LocalId;
        _sequence++;
        var packet = VoicePacket.Write(local, _sequence, data, data.Length);
        var api = SessionNetwork.Transport?.Api;
        if (api != null && IsEnabled)
        {
            foreach (var peer in Targets())
            {
                Send(api, peer, packet);
            }
        }

        if (!Decode(local, data, out var samples))
        {
            return;
        }

        if (VoicePcm.Peak(_pcm, samples) >= SpeakingLevel)
        {
            MarkHeard(local, now);
        }

        if (Loopback && !Deafened)
        {
            Output(local).Push(_pcm, samples);
        }
    }

    private static void Send(ISteamChannelApi api, ulong peer, byte[] packet)
    {
        try
        {
            var result = api.Send(peer, packet, SendFlags, Channel);
            if (result == SteamMessagesTransport.ResultOk)
            {
                PacketsSent++;
                BytesSent += packet.Length;
                return;
            }

            PacketsDropped++;
            Report("send" + result, $"Sending voice to {peer} failed with Steam result {result}");
        }
        catch (Exception exception)
        {
            PacketsDropped++;
            Report("send", $"Sending voice to {peer} failed", exception);
        }
    }

    private static IReadOnlyList<ulong> Targets()
    {
        if (!IsEnabled)
        {
            return Array.Empty<ulong>();
        }

        var host = SessionNetwork.Host;
        var client = SessionNetwork.Client;
        var role = CatNetwork.Role;
        return VoiceRoutes.FromLocal(role, client?.HostId ?? 0, client?.SharesWithHost(OwnerId) ?? false, host?.PeersSharing(OwnerId));
    }

    private static void ReceivePackets(double now)
    {
        var transport = SessionNetwork.Transport;
        if (transport == null)
        {
            return;
        }

        try
        {
            transport.Api.Receive(Channel, (sender, bytes) => OnReceived(sender, bytes, now));
        }
        catch (Exception exception)
        {
            Report("receive", "Receiving voice failed", exception);
        }
    }

    private static void OnReceived(ulong sender, byte[] bytes, double now)
    {
        PacketsReceived++;
        if (!IsEnabled || !VoicePacket.TryRead(bytes, out var packet) || !Rate.Allow(sender, now))
        {
            PacketsDropped++;
            return;
        }

        var role = CatNetwork.Role;
        var host = SessionNetwork.Host;
        var client = SessionNetwork.Client;
        var sharing = host?.PeersSharing(OwnerId) ?? (IReadOnlyList<ulong>)Array.Empty<ulong>();
        if (!VoiceRoutes.Accept(role, sender, client?.HostId ?? 0, LocalId, sharing.ToList(), packet, out var speaker))
        {
            PacketsDropped++;
            return;
        }

        if (LastSequence.TryGetValue(speaker, out var last) && !VoicePacket.IsNewer(packet.Sequence, last))
        {
            PacketsDropped++;
            return;
        }

        LastSequence[speaker] = packet.Sequence;
        if (AnnouncedSpeakers.Add(speaker))
        {
            _log.Info($"First voice from {Name(speaker)}");
        }

        var forward = VoiceRoutes.Forward(role, speaker, sharing);
        if (forward.Count > 0)
        {
            var relayed = packet.Speaker == speaker ? bytes : VoicePacket.WithSpeaker(bytes, speaker);
            var api = SessionNetwork.Transport?.Api;
            foreach (var peer in forward)
            {
                if (api != null && relayed != null)
                {
                    Send(api, peer, relayed);
                    PacketsForwarded++;
                }
            }
        }

        Play(speaker, packet.Data, now);
    }

    private static void Play(ulong speaker, byte[] data, double now)
    {
        LastHeard[speaker] = now;
        if (Deafened || MutedPlayers.Contains(speaker))
        {
            MarkHeard(speaker, now);
            return;
        }

        if (!Decode(speaker, data, out var samples))
        {
            return;
        }

        if (VoicePcm.Peak(_pcm, samples) >= SpeakingLevel)
        {
            MarkHeard(speaker, now);
        }

        Output(speaker).Push(_pcm, samples);
    }

    private static VoiceOutput Output(ulong speaker)
    {
        if (!Outputs.TryGetValue(speaker, out var output))
        {
            output = new VoiceOutput(speaker, _rate, _log);
            Outputs[speaker] = output;
        }

        LastHeard[speaker] = FrameLoop.Realtime;
        return output;
    }

    private static bool Decode(ulong speaker, byte[] data, out int samples)
    {
        samples = 0;
        var steam = Steam();
        if (steam == null || data.Length == 0)
        {
            return false;
        }

        uint written;
        int result;
        try
        {
            result = steam.Decompress(data, data.Length, _pcmBytes, out written, (uint)_rate);
            if (result == VoiceResults.BufferTooSmall)
            {
                _pcmBytes = new byte[_pcmBytes.Length * 2];
                result = steam.Decompress(data, data.Length, _pcmBytes, out written, (uint)_rate);
            }
        }
        catch (Exception exception)
        {
            Report("decompress", "Decompressing voice failed", exception);
            return false;
        }

        if (result != VoiceResults.Ok)
        {
            Report("decompress" + result, $"Steam could not decompress voice of {Name(speaker)}: {VoiceResults.Name(result)}");
            return false;
        }

        if (_pcm.Length < written / 2)
        {
            _pcm = new float[written / 2];
        }

        samples = VoicePcm.Decode16(_pcmBytes, (int)written, _pcm);
        return samples > 0;
    }

    private static void UpdateOutputs(double now)
    {
        if (Outputs.Count == 0)
        {
            return;
        }

        var root = Root();
        if (!_groupSearched)
        {
            _groupSearched = true;
            _group = VoiceOutput.FindGroup(_log);
        }

        var listener = ListenerPosition();
        var local = LocalId;
        foreach (var output in Outputs.Values.ToList())
        {
            if (LastHeard.TryGetValue(output.Speaker, out var heard) && now - heard > ForgetSeconds && !output.IsPlaying)
            {
                output.Destroy();
                Outputs.Remove(output.Speaker);
                continue;
            }

            if (!output.IsPlaying && output.Stream.Queued > 0 && now - heard > StaleSeconds)
            {
                output.Stream.Clear();
            }

            try
            {
                var position = output.Speaker == local ? null : GhostPosition(output.Speaker);
                output.Update(root, _group, Sound, FullVolumeDistance, HearDistance, Volume * PlayerVolume(output.Speaker), position, listener);
            }
            catch (Exception exception)
            {
                Report("output" + output.Speaker, $"Playing the voice of {Name(output.Speaker)} failed", exception);
                output.Destroy();
                Outputs.Remove(output.Speaker);
            }
        }
    }

    private static void ExpireActivity(double now)
    {
        foreach (var speaker in Activity.Expire(now))
        {
            RaiseSpeaking(speaker, false);
        }
    }

    private static void MarkHeard(ulong speaker, double now)
    {
        if (Activity.Heard(speaker, now))
        {
            RaiseSpeaking(speaker, true);
        }
    }

    private static void RaiseSpeaking(ulong speaker, bool speaking)
    {
        GameEventStream.Publish(SpeakingChangedName, $"player={speaker} speaking={speaking}");
        SafeInvoker.Invoke(SpeakingChanged, speaker, speaking, SpeakingChangedName, _log);
    }

    private static void Forget(ulong speaker)
    {
        if (Outputs.Remove(speaker, out var output))
        {
            output.Destroy();
        }

        LastHeard.Remove(speaker);
        LastSequence.Remove(speaker);
        Rate.Forget(speaker);
        AnnouncedSpeakers.Remove(speaker);
        if (Activity.Forget(speaker))
        {
            RaiseSpeaking(speaker, false);
        }
    }

    private static void Reset()
    {
        foreach (var output in Outputs.Values)
        {
            output.Destroy();
        }

        Outputs.Clear();
        LastHeard.Clear();
        LastSequence.Clear();
        Rate.Clear();
        AnnouncedSpeakers.Clear();
        foreach (var speaker in Activity.Clear())
        {
            RaiseSpeaking(speaker, false);
        }

        _groupSearched = false;
    }

    private static ISteamVoice Steam()
    {
        if (_steam != null || _steamTried)
        {
            return _steam;
        }

        _steamTried = true;
        try
        {
            _steam = FlatSteamVoice.TryCreate(out var error);
            if (_steam == null)
            {
                _log.Info($"The flat Steam voice API is unavailable ({error}), using the interop API");
                _steam = new InteropSteamVoice();
            }

            LocalSteamId = SessionNetwork.SteamUserId();
            _log.Info($"Steam voice opened through the {_steam.Name} API, Steam prefers {_steam.OptimalSampleRate} Hz, voice plays at {_rate} Hz");
        }
        catch (Exception exception)
        {
            Report("open", "Opening Steam voice failed", exception);
            _steam = null;
        }

        return _steam;
    }

    private static Transform Root()
    {
        if (_root != null)
        {
            return _root.transform;
        }

        _root = new GameObject("CatLib_Voice");
        UnityEngine.Object.DontDestroyOnLoad(_root);
        return _root.transform;
    }

    private static Vector3? GhostPosition(ulong speaker)
    {
        try
        {
            return CatLib.Game.GamePlayers.TryGetGhost(speaker, out var ghost) ? ghost.transform.position : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Vector3? ListenerPosition()
    {
        try
        {
            var camera = Camera.main;
            return camera == null ? null : camera.transform.position;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static int SafeOutputRate()
    {
        try
        {
            return AudioSettings.outputSampleRate;
        }
        catch (Exception)
        {
            return VoicePcm.MaxSampleRate;
        }
    }

    private static string Name(ulong steamId)
    {
        if (steamId == LocalId)
        {
            return "you";
        }

        var name = CatNetwork.PlayerName(steamId);
        return string.IsNullOrEmpty(name) ? steamId.ToString() : $"{name} ({steamId})";
    }

    private static float ClampVolume(float volume) => float.IsNaN(volume) ? 1f : Math.Max(0f, Math.Min(MaxVolume, volume));

    private static void Report(string key, string message, Exception exception = null)
    {
        if (!ReportedProblems.Add(key))
        {
            return;
        }

        if (exception == null)
        {
            _log.Warning(message);
        }
        else
        {
            _log.Error(message, exception);
        }
    }
}
