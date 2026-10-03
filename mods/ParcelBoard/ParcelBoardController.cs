using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CatLib.Game;
using CatLib.Localization;
using CatLib.Logging;
using CatLib.UI;
using ParcelBoard.Logic;
using ParcelBoard.Scene;
using ParcelBoard.Settings;

namespace ParcelBoard;

public sealed class ParcelBoardController
{
    public const int RefreshFrames = 15;

    private readonly CatLogger _log;
    private readonly TextCatalog _texts;
    private readonly BoardView _view = new();
    private readonly HashSet<PackageSize> _seenSizes = new();
    private readonly Dictionary<PackageSize, ParcelFootprint> _footprints = new();
    private char? _times;
    private int _countdown;
    private bool _hidden;
    private bool? _open;
    private bool _logged;

    public ParcelBoardController(CatLogger log, TextCatalog texts)
    {
        _log = log;
        _texts = texts;
    }

    public BoardSettings Settings { get; set; }

    public BoardView View => _view;

    public bool IsOpen => _open ?? Settings.StartOpen.Value;

    public bool IsHidden => _hidden;

    public IReadOnlyList<BoardColumn> LastColumns { get; private set; } = Array.Empty<BoardColumn>();

    public void OnSettingsChanged() => _countdown = 0;

    public void Forget()
    {
        _view.Forget();
        _seenSizes.Clear();
        _footprints.Clear();
        _times = null;
        _open = null;
        _logged = false;
        LastColumns = Array.Empty<BoardColumn>();
    }

    public void Update()
    {
        try
        {
            if (!HudLayer.IsAvailable || !CatParcels.IsAvailable)
            {
                return;
            }

            HandleKeys();
            if (--_countdown > 0)
            {
                return;
            }

            _countdown = RefreshFrames;
            Refresh();
        }
        catch (Exception exception)
        {
            _countdown = RefreshFrames * 20;
            _log.Error("Updating the parcel board failed", exception);
        }
    }

    public void Refresh()
    {
        if (!Settings.Enabled.Value || _hidden)
        {
            _view.Hide();
            return;
        }

        var parcels = CatParcels.Read();
        foreach (var parcel in parcels)
        {
            _seenSizes.Add(parcel.Size);
            if (parcel.Footprint.IsKnown && !_footprints.ContainsKey(parcel.Size))
            {
                _footprints[parcel.Size] = parcel.Footprint;
            }
        }

        var options = Settings.Options();
        options.KnownSizes = _seenSizes;
        LastColumns = BoardCounter.Build(parcels, options);
        _view.Show(LastColumns, Look());
        if (!_logged && _view.IsAlive)
        {
            _logged = true;
            _log.Info($"Parcel board shown with {LastColumns.Count} list(s) for {parcels.Count} parcel(s)");
        }
    }

    public BoardLook Look() => new()
    {
        Open = IsOpen,
        Corner = Settings.Corner.Value,
        Scale = Settings.Scale.Value,
        Opacity = Settings.Opacity.Value,
        RegionNames = Settings.RegionNames.Value,
        Order = Settings.Order.Value,
        Title = Title,
        SizeName = SizeName,
        SizeTag = SizeTag
    };

    public string Title(BoardList list) => _texts.Get("list." + list);

    public string SizeName(PackageSize size) => _texts.Get("size." + size);

    public string SizeTag(PackageSize size) =>
        _footprints.TryGetValue(size, out var footprint) ? footprint.Text(Times()) : string.Empty;

    private char Times()
    {
        if (_times == null)
        {
            var font = HudLayer.Font;
            try
            {
                _times = font == null || font.HasCharacter('\u00D7', true, true) ? '\u00D7' : 'x';
            }
            catch (Exception)
            {
                _times = 'x';
            }
        }

        return _times.Value;
    }

    public void ToggleOpen()
    {
        _open = !IsOpen;
        _countdown = 0;
    }

    public void ToggleHidden()
    {
        _hidden = !_hidden;
        _countdown = 0;
    }

    public string Describe()
    {
        var parcels = CatParcels.Read();
        var builder = new StringBuilder();
        builder.Append($"{parcels.Count} parcel(s), board {(IsHidden ? "hidden" : "shown")}, lists {(IsOpen ? "open" : "closed")}");
        foreach (var group in parcels.GroupBy(parcel => parcel.Place).OrderBy(group => group.Key))
        {
            builder.Append($"; {group.Key}: {group.Count()}");
        }

        _log.Info(builder.ToString());
        foreach (var parcel in parcels)
        {
            _log.Info($"Parcel {parcel.NetworkId}: {parcel.Region}, {parcel.Size} {parcel.Footprint.Text('x')}, weight {parcel.Weight}, storage {parcel.Storage}, behavior {parcel.Behavior}" +
                      $"{(parcel.IsDamaged ? ", damaged" : string.Empty)}{(parcel.NeedsStamps ? ", missing stamps: " + parcel.MissingStamps : string.Empty)}, {parcel.Place}");
        }

        foreach (var column in BoardCounter.Build(parcels, Settings.Options()))
        {
            _log.Info($"List {column.List}: {column.Total} — {string.Join(", ", column.Rows.Select(row => (row.IsSize ? row.Size.ToString() : row.Region.ToString()) + " " + row.Count))}");
        }

        return "see the log";
    }

    public string MeasureSizes()
    {
        if (!CatParcels.IsAvailable)
        {
            return "no parcels here";
        }

        var parcels = Singleton<ParcelManager>.Instance.GetAllParcels();
        var measured = new Dictionary<PackageSize, int>();
        var lines = new List<string>();
        for (var index = 0; parcels != null && index < parcels.Count; index++)
        {
            var parcel = parcels[index];
            var properties = parcel == null || parcel.IsDisposed ? null : parcel.Properties;
            if (properties == null)
            {
                continue;
            }

            var size = properties.PackageSize;
            measured[size] = measured.TryGetValue(size, out var count) ? count + 1 : 1;
            if (measured[size] > 1)
            {
                continue;
            }

            var pickable = parcel.Interactable == null ? null : parcel.Interactable.Pickable;
            var footprint = pickable == null ? default : pickable.Size;
            var collider = pickable == null ? null : pickable.MainCollider;
            var box = collider == null ? UnityEngine.Vector3.zero : UnityEngine.Vector3.Scale(collider.size, collider.transform.lossyScale);
            lines.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Size {0}: footprint {1}x{2} cells, box {3:0.###} x {4:0.###} x {5:0.###} m (width x height x depth), prefab {6}",
                size, footprint.x, footprint.y, box.x, box.y, box.z, parcel.name));
        }

        foreach (var line in lines.OrderBy(line => line, StringComparer.Ordinal))
        {
            _log.Info(line);
        }

        var missing = BoardCounter.SizeOrder.Where(size => !measured.ContainsKey(size)).ToList();
        _log.Info($"Measured {lines.Count} size(s) from {measured.Values.Sum()} parcel(s){(missing.Count == 0 ? string.Empty : "; not in this level: " + string.Join(", ", missing))}");
        return "see the log";
    }

    private void HandleKeys()
    {
        if (HudLayer.IsGameMenuOpen)
        {
            return;
        }

        if (Settings.ShowKey.Value.IsDown())
        {
            ToggleHidden();
        }

        if (Settings.OpenKey.Value.IsDown())
        {
            ToggleOpen();
        }
    }
}
