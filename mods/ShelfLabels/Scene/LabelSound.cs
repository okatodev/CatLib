using System;
using CatLib.Logging;
using UnityEngine;

namespace ShelfLabels.Scene;

public sealed class LabelSound
{
    private readonly CatLogger _log;
    private bool _reported;

    public LabelSound(CatLogger log)
    {
        _log = log;
    }

    public void Play(IntPtr actionPointer, int step)
    {
        try
        {
            if (actionPointer == IntPtr.Zero || !Singleton<AudioManager>.HasInstance())
            {
                return;
            }

            var action = new EntityInteractableAction(actionPointer);
            var asset = step < 0 ? action.SecondaryInteractionAudioAsset ?? action.PrimaryInteractionAudioAsset : action.PrimaryInteractionAudioAsset;
            if (asset == null)
            {
                return;
            }

            Singleton<AudioManager>.Instance.PlayOneShot(asset, action.transform.position, 1f, 1f, -1);
        }
        catch (Exception exception)
        {
            if (!_reported)
            {
                _reported = true;
                _log.Warning($"Playing the click sound of an extra label failed, extra labels stay silent: {exception.Message}");
            }
        }
    }
}
