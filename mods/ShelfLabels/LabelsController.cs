using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Game.Events;
using CatLib.Localization;
using CatLib.Logging;
using CatLib.Net;
using CatLib.UI;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace ShelfLabels;

public sealed class LabelsController
{
    public const float ScanIntervalSeconds = 2f;
    public const float LookDistance = 6f;
    public const float NoticeSeconds = 3f;
    public const int MaxParentSteps = 8;

    private readonly Dictionary<IntPtr, LabelView> _views = new();
    private readonly Dictionary<IntPtr, (LabelView View, int Slot)> _actions = new();
    private readonly Dictionary<IntPtr, LabelView> _roots = new();
    private readonly CatLogger _log;
    private readonly ModChannel _channel;
    private readonly LabelBoard _board;
    private readonly PlacementBoard _placements;
    private readonly StandBoard _stands;
    private readonly HashSet<string> _reportedStands = new(StringComparer.Ordinal);
    private readonly TextCatalog _texts;
    private float _nextScan;
    private bool _layoutDirty = true;
    private SessionRole _lastRole = SessionRole.Offline;
    private int _shownSlots = -1;
    private bool _inLevel;
    private int _noticeLabel;
    private float _noticeUntil;
    private bool _noticeStand;

    public LabelsController(CatLogger log, ModChannel channel, LabelBoard board, PlacementBoard placements, StandBoard stands, TextCatalog texts)
    {
        _log = log;
        _channel = channel;
        _board = board;
        _placements = placements;
        _stands = stands;
        _texts = texts;
        _board.Changed += OnBoardChanged;
        _placements.Changed += ids => OnLayoutChanged(ids, false);
        _stands.Changed += ids => OnLayoutChanged(ids, true);
        BootstrapEvents.LevelLoadStarted += Forget;
        BootstrapEvents.GameRestartStarted += Forget;
        BootstrapEvents.LevelLoadFinalized += () =>
        {
            _inLevel = true;
            _nextScan = 0;
        };
    }

    public LabelSettings Settings { get; set; }

    public LabelSync Sync { get; set; }

    public void RequestLayout()
    {
        _layoutDirty = true;
        _nextScan = 0;
    }

    public int SpriteCount(int labelId) => _views.Values.FirstOrDefault(view => view.LabelId == labelId && view.IsAlive)?.SpriteCount ?? 0;

    public bool HandleClick(IntPtr action, int step, bool fromNetwork)
    {
        if (!_actions.TryGetValue(action, out var target))
        {
            return false;
        }

        if (fromNetwork)
        {
            return true;
        }

        var slot = new LabelSlot(target.View.LabelId, target.Slot);
        if (!(Sync?.Click(slot, step) ?? false))
        {
            _log.Info($"The click on extra label {slot} was not sent: {(CatNetwork.Role == SessionRole.Client ? "the host does not have Shelf Labels" : "the mod is not ready")}");
        }

        return true;
    }

    public void Update()
    {
        if (Settings == null)
        {
            return;
        }

        var role = CatNetwork.Role;
        if (role != _lastRole)
        {
            if (role == SessionRole.Client)
            {
                _board.Clear();
                _placements.Clear();
                _stands.Clear();
            }

            _lastRole = role;
            _nextScan = 0;
        }

        if (!_inLevel)
        {
            return;
        }

        HandleHotkey();
        if (Time.unscaledTime < _nextScan)
        {
            return;
        }

        _nextScan = Time.unscaledTime + ScanIntervalSeconds;
        var slots = VisibleSlots();
        if (slots != _shownSlots)
        {
            _log.Info(slots == Settings.Slots.Value
                ? $"Showing {slots} extra label(s) per shelf label"
                : "Extra labels are hidden: the host does not have Shelf Labels");
            _shownSlots = slots;
        }

        Scan();
        var relayout = _layoutDirty;
        _layoutDirty = false;
        foreach (var view in _views.Values.ToList())
        {
            if (!view.IsAlive)
            {
                Drop(view);
                continue;
            }

            if (relayout)
            {
                view.RemoveAll();
            }

            Refresh(view, slots);
        }

        RebuildLookups();
    }

    private int VisibleSlots() => CatNetwork.Role == SessionRole.Client && !_channel.CanSendToHost ? 0 : Settings.Slots.Value;

    private Placement Effective(int labelId) => _placements.Get(labelId, Settings.Placement.Value);

    private bool StandHidden(int labelId) => _stands.IsHidden(labelId, Settings.HideStands.Value);

    private void Refresh(LabelView view, int slots)
    {
        view.Sync(slots, Effective(view.LabelId), StandHidden(view.LabelId), Settings.Spacing.Value, _log);
        view.Show(_board.Get);
        var summary = view.StandSummary;
        if (summary != null && _reportedStands.Add(TypeName(view.Holder.name)))
        {
            _log.Info("Stand of extra labels on " + summary);
        }
    }

    private static string TypeName(string name)
    {
        var cut = name.LastIndexOf(" (", StringComparison.Ordinal);
        return cut > 0 && name.EndsWith(")", StringComparison.Ordinal) ? name.Substring(0, cut) : name;
    }

    private void HandleHotkey()
    {
        var placement = Settings.PlacementHotkey.Value.IsDown();
        var stand = !placement && Settings.StandHotkey.Value.IsDown();
        if (!placement && !stand)
        {
            return;
        }

        var view = LookedAt();
        if (view == null)
        {
            Notifications.Show(_texts.Get("message.lookAtLabel"));
            return;
        }

        var sent = placement ? Sync?.CyclePlacement(view.LabelId) ?? false : Sync?.ToggleStand(view.LabelId) ?? false;
        if (!sent)
        {
            Notifications.Show(_texts.Get("message.noHost"));
            return;
        }

        _noticeLabel = view.LabelId;
        _noticeStand = stand;
        _noticeUntil = Time.unscaledTime + NoticeSeconds;
    }

    private LabelView LookedAt()
    {
        var camera = FindCamera();
        if (camera == null)
        {
            return null;
        }

        var hits = Physics.RaycastAll(camera.transform.position, camera.transform.forward, LookDistance, Physics.AllLayers, QueryTriggerInteraction.Collide);
        var ordered = new List<RaycastHit>();
        foreach (var hit in hits)
        {
            ordered.Add(hit);
        }

        foreach (var hit in ordered.OrderBy(hit => hit.distance))
        {
            var collider = hit.collider;
            if (collider == null || collider.GetComponentInParent<PlayerEntityBase>() != null)
            {
                continue;
            }

            var current = collider.transform;
            for (var step = 0; step < MaxParentSteps && current != null; step++)
            {
                if (_roots.TryGetValue(current.Pointer, out var view))
                {
                    return view;
                }

                current = current.parent;
            }

            if (!collider.isTrigger)
            {
                return null;
            }
        }

        return null;
    }

    private static Camera FindCamera()
    {
        var main = Camera.main;
        if (main != null)
        {
            return main;
        }

        Camera best = null;
        foreach (var camera in Camera.allCameras)
        {
            if (camera != null && camera.isActiveAndEnabled && camera.targetTexture == null && (best == null || camera.depth > best.depth))
            {
                best = camera;
            }
        }

        return best;
    }

    private void Scan()
    {
        var found = UnityEngine.Object.FindObjectsByType(Il2CppType.Of<EntityStorageLabel>(), FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var added = 0;
        foreach (var item in found)
        {
            var label = item.TryCast<EntityStorageLabel>();
            if (label == null || _views.ContainsKey(label.Pointer) || !label.enabled || label.transform.parent == null)
            {
                continue;
            }

            if (label.transform.parent.name.EndsWith(LabelView.CopySuffix, StringComparison.Ordinal))
            {
                continue;
            }

            var identifier = label.GetComponent<EntityPermanentIdentifier>();
            var id = identifier == null ? 0 : identifier.PermanentIdentifier;
            if (id <= 0)
            {
                continue;
            }

            _views[label.Pointer] = new LabelView(label, id);
            added++;
        }

        if (added > 0)
        {
            _log.Info($"Found {added} new shelf label(s), {_views.Count} in total");
        }
    }

    private void RebuildLookups()
    {
        _actions.Clear();
        _roots.Clear();
        foreach (var view in _views.Values)
        {
            foreach (var action in view.Actions)
            {
                if (view.TryGetSlot(action, out var slot))
                {
                    _actions[action] = (view, slot);
                }
            }

            foreach (var root in view.Roots)
            {
                _roots[root] = view;
            }
        }
    }

    private void OnBoardChanged(IReadOnlyList<LabelSlot> changed)
    {
        var ids = new HashSet<int>(changed.Select(slot => slot.LabelId));
        foreach (var view in _views.Values.Where(view => ids.Contains(view.LabelId)))
        {
            view.Show(_board.Get);
        }
    }

    private void OnLayoutChanged(IReadOnlyList<int> changed, bool stand)
    {
        if (Settings == null)
        {
            return;
        }

        var ids = new HashSet<int>(changed);
        if (_inLevel)
        {
            var slots = VisibleSlots();
            foreach (var view in _views.Values.Where(view => ids.Contains(view.LabelId) && view.IsAlive))
            {
                Refresh(view, slots);
            }

            RebuildLookups();
        }

        if (_noticeLabel == 0 || _noticeStand != stand || !ids.Contains(_noticeLabel) || Time.unscaledTime > _noticeUntil)
        {
            return;
        }

        if (!stand)
        {
            Notifications.Show(_texts.Format("message.placement", _texts.Get(SettingTexts.EnumKey(Effective(_noticeLabel)))));
        }
        else if (!StandHidden(_noticeLabel))
        {
            Notifications.Show(_texts.Get("message.standShown"));
        }
        else
        {
            var problem = _views.Values.FirstOrDefault(view => view.LabelId == _noticeLabel)?.StandProblem;
            if (problem != null)
            {
                _log.Warning($"The stand of label {_noticeLabel} cannot be removed: {problem}");
            }

            Notifications.Show(_texts.Get(problem == null ? "message.standHidden" : "message.standUnsupported"));
        }

        _noticeLabel = 0;
    }

    private void Drop(LabelView view)
    {
        view.RemoveAll();
        _views.Remove(view.Pointer);
    }

    private void Forget()
    {
        foreach (var view in _views.Values)
        {
            view.RemoveAll();
        }

        _views.Clear();
        _actions.Clear();
        _roots.Clear();
        _inLevel = false;
        _nextScan = 0;
    }
}
