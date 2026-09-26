using System;
using UnityEngine;

namespace CatLib.Core;

public sealed class CatLibBehaviour : MonoBehaviour
{
    public CatLibBehaviour(IntPtr pointer) : base(pointer)
    {
    }

    public void Update()
    {
        CatLibRuntime.Tick();
    }

    public void OnGUI()
    {
        CatLib.DevTools.DevMenu.Draw();
    }
}
