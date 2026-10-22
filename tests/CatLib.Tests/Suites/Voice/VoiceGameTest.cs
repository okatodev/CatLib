using System;
using System.Collections.Generic;
using System.Reflection;
using CatLib.Core;
using CatLib.Tests.Framework;
using CatLib.Voice;
using UnityEngine;

namespace CatLib.Tests.Suites.Voice;

public sealed class VoiceGameTest : TestCase
{
    public override string Suite => "Voice";

    public override IEnumerable<TestStep> Run(TestContext context)
    {
        if (!CatLibRuntime.IsInitialized)
        {
            context.Note("Playing voice through Unity can only be checked in the game");
            yield break;
        }

        context.Note($"Voice: owner {CatVoice.OwnerId ?? "none"}, plays at {CatVoice.SampleRate} Hz, Steam API {CatVoice.SteamApiName ?? "not opened yet"}");
        var type = typeof(CatVoice).Assembly.GetType("CatLib.Voice.VoiceOutput");
        var output = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { 1UL, CatVoice.SampleRate, CatLibRuntime.Log.Scope("VoiceTest") }, null);
        var stream = (VoiceStream)type.GetProperty("Stream").GetValue(output);
        var update = type.GetMethod("Update");
        var tone = new float[stream.SampleRate];
        for (var index = 0; index < tone.Length; index++)
        {
            tone[index] = 0.05f * Mathf.Sin(2f * Mathf.PI * 440f * index / stream.SampleRate);
        }

        var pushed = 0;
        var root = new GameObject("CatLib_VoiceTest");
        try
        {
            var started = false;
            for (var frame = 0; frame < 600; frame++)
            {
                if (pushed < tone.Length / 2)
                {
                    var chunk = Math.Min(tone.Length / 2 - pushed, Math.Max(1, (int)(stream.SampleRate * Math.Min(Time.unscaledDeltaTime, 0.05f))));
                    var piece = new float[chunk];
                    Array.Copy(tone, pushed, piece, 0, chunk);
                    stream.Push(piece, chunk);
                    pushed += chunk;
                }

                update.Invoke(output, new object[] { root.transform, null, VoiceSound.Flat, 3f, 25f, 0.2f, null, null });
                started |= stream.IsPlaying;
                if (stream.Queued == 0 && started && pushed >= tone.Length / 2)
                {
                    break;
                }

                yield return Wait.NextFrame();
            }

            Assert.True(started, "Half a second of tone, given frame by frame, starts the voice source");
            Assert.Equal(0, stream.Queued, "The tone is played out to the end");
            Assert.Equal(0, stream.Hitches, "No frame skipped past the written voice");
            Assert.Equal(0, stream.Dropped, "Voice that comes as fast as it plays is never dropped");
            var source = root.GetComponentInChildren<AudioSource>();
            Assert.NotNull(source, "The voice has an audio source");
            Assert.True(source.clip != null && source.clip.frequency == CatVoice.SampleRate, "Its clip plays at the voice rate");
            context.Note($"Played half a second of tone: gaps {stream.Gaps}, dropped {stream.Dropped}");
        }
        finally
        {
            type.GetMethod("Destroy").Invoke(output, null);
            UnityEngine.Object.Destroy(root);
        }
    }
}
