using System;
using System.Collections.Generic;
using CatLib.Core;
using CatLib.DevTools;
using CatLib.Game.Events;
using CatLib.Logging;
using UnityEngine;

namespace CatLib.Tests.Diagnostics;

public sealed class UiHider
{
    public const string Group = "UI";
    public const string HideLabel = "Hide the game's UI";
    public const string NotificationsLabel = "Hide notifications too";

    private readonly CatLogger _log;
    private readonly DevMenuModel _menu;
    private readonly Dictionary<IntPtr, KeptGroup> _kept = new();
    private bool _hidden;
    private bool _notificationsShown;
    private InterfaceManager _applied;
    private IntPtr _appliedPointer;
    private float _savedAlpha = 1f;
    private bool _reportedNoGroup;

    public UiHider(CatLogger log, DevMenuModel menu)
    {
        _log = log;
        _menu = menu ?? throw new ArgumentNullException(nameof(menu));
        Item = DevItem.Toggle(Group, HideLabel, () => Hidden, value => Hidden = value,
            "Hides the game's interface for screenshots and videos; the pause menu, settings, results and stamp choice still show. " +
            "While it is on, a toggle for notifications shows below it. Turns off in the main menu.");
        NotificationsItem = DevItem.Toggle(Group, NotificationsLabel, () => HideNotifications, value => HideNotifications = value,
            "On: the game's notifications are hidden with the rest of the interface. Off: they still show while the interface is hidden.");
    }

    public DevItem Item { get; }

    public DevItem NotificationsItem { get; }

    public bool HideNotifications { get; set; } = true;

    public bool Hidden
    {
        get => _hidden;
        set
        {
            if (_hidden == value)
            {
                return;
            }

            _hidden = value;
            if (value && !_notificationsShown)
            {
                _menu.Add(NotificationsItem);
                _notificationsShown = true;
            }
            else if (!value && _notificationsShown)
            {
                _menu.Remove(NotificationsItem);
                _notificationsShown = false;
            }
        }
    }

    public void Attach()
    {
        FrameLoop.Update += Update;
        BootstrapEvents.MainMenuLoaded += OnMainMenuLoaded;
    }

    private void OnMainMenuLoaded()
    {
        if (!Hidden)
        {
            return;
        }

        Hidden = false;
        Update();
        _log.Info("The game's UI shows again, the main menu loaded");
    }

    private void Update()
    {
        try
        {
            var manager = Singleton<InterfaceManager>.HasInstance() ? Singleton<InterfaceManager>.Instance : null;
            if (!Hidden)
            {
                if (_applied != null)
                {
                    Restore();
                }

                return;
            }

            if (manager == null)
            {
                Forget();
                return;
            }

            if (_applied == null || _appliedPointer != manager.Pointer)
            {
                Restore();
                Apply(manager);
            }

            Enforce(manager);
        }
        catch (Exception exception)
        {
            Hidden = false;
            Forget();
            _log.Error("Hiding the game's UI failed, it shows again", exception);
        }
    }

    private void Apply(InterfaceManager manager)
    {
        var group = manager.CanvasGroup;
        if (group == null)
        {
            if (!_reportedNoGroup)
            {
                _reportedNoGroup = true;
                _log.Warning("The game's interface has no canvas group to hide");
            }

            return;
        }

        _applied = manager;
        _appliedPointer = manager.Pointer;
        _savedAlpha = group.alpha;
        _log.Info($"The game's UI is hidden, notifications {(HideNotifications ? "too" : "still show")}");
    }

    private void Enforce(InterfaceManager manager)
    {
        if (_applied == null)
        {
            return;
        }

        var group = manager.CanvasGroup;
        if (group != null && group.alpha != 0f)
        {
            group.alpha = 0f;
        }

        Keep(manager.PauseInterface);
        Keep(manager.SettingsInterface);
        Keep(manager.RecapScreenInterface);
        Keep(manager.StampSelectionInterface);
        Keep(manager.CheatInterface);
        Keep(manager.SpawnerInterface);
        Keep(manager.CreditsInterface);
        Keep(manager.DemoEndInterface);
        if (HideNotifications)
        {
            Release(manager.NotificationInterface);
        }
        else
        {
            Keep(manager.NotificationInterface);
        }
    }

    private void Keep(InterfaceBase ui)
    {
        if (ui == null || _kept.ContainsKey(ui.Pointer))
        {
            return;
        }

        var group = ui._canvasGroup;
        if (group == null)
        {
            group = ui.GetComponent<CanvasGroup>();
        }

        var added = false;
        if (group == null)
        {
            group = ui.gameObject.AddComponent<CanvasGroup>();
            added = true;
        }

        _kept[ui.Pointer] = new KeptGroup(group, group.ignoreParentGroups, added);
        group.ignoreParentGroups = true;
    }

    private void Release(InterfaceBase ui)
    {
        if (ui == null || !_kept.TryGetValue(ui.Pointer, out var kept))
        {
            return;
        }

        _kept.Remove(ui.Pointer);
        kept.Restore();
    }

    private void Restore()
    {
        if (_applied != null && Singleton<InterfaceManager>.HasInstance() && Singleton<InterfaceManager>.Instance.Pointer == _appliedPointer)
        {
            var group = _applied.CanvasGroup;
            if (group != null)
            {
                group.alpha = _savedAlpha;
            }

            foreach (var kept in _kept.Values)
            {
                kept.Restore();
            }

            _log.Info("The game's UI shows again");
        }

        Forget();
    }

    private void Forget()
    {
        _applied = null;
        _appliedPointer = IntPtr.Zero;
        _kept.Clear();
    }

    private sealed class KeptGroup
    {
        private readonly CanvasGroup _group;
        private readonly bool _ignoredBefore;
        private readonly bool _added;

        public KeptGroup(CanvasGroup group, bool ignoredBefore, bool added)
        {
            _group = group;
            _ignoredBefore = ignoredBefore;
            _added = added;
        }

        public void Restore()
        {
            if (_group == null)
            {
                return;
            }

            if (_added)
            {
                UnityEngine.Object.Destroy(_group);
            }
            else
            {
                _group.ignoreParentGroups = _ignoredBefore;
            }
        }
    }
}
