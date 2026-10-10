using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CatLib.Assets;
using CatLib.Localization;
using CatLib.Logging;
using CatLib.Net;
using CatLib.Threading;

namespace CustomStamps.Packs;

public sealed class StampLibrary
{
    public const string StampsSection = "Stamps";
    public const string DecorativeKey = "Decorative";
    public const string WeightKey = "Weight";
    public const string PackTextsPrefix = "pack.";
    public const int MaxSide = 512;
    public const float Margin = 0.02f;
    public const float OutlineShare = 0.04f;
    public const string OutlineKey = "outline";
    public const int IconGap = 12;
    public const string Duplicate = "Duplicate";
    public const string Unreadable = "Unreadable";

    public const string SampleAtlas = "sp_UI_Elements_01";
    public const float MinClearShare = 0.04f;
    public const int IconStamps = 4;

    private static readonly Sample[] Samples =
    {
        new(StampKind.Decorative, "cat.png", "sp_Cat_Intro_01", 13, 14, 378, 302, "cat.png"),
        new(StampKind.Weight, "cat_weight.png", "sp_Cat_Intro_02", 425, 14, 378, 304, "parcel.png")
    };

    private readonly CatLogger _log;
    private readonly TextCatalog _texts;
    private readonly Dictionary<string, CustomStamp> _byKey = new(StringComparer.Ordinal);
    private readonly List<CustomStamp> _stamps = new();
    private readonly Dictionary<ContentPack, (MenuGalleryPair Galleries, int Count)> _cards = new();
    private int _loading;

    public StampLibrary(CatLogger log, TextCatalog texts, string version)
    {
        _log = log;
        _texts = texts;
        Kind = new ContentPackKind(PluginMeta.Guid, StampFiles.PackFile, "customstamps")
        {
            Policy = SessionPolicy.RequiredOnAll,
            VersionRule = VersionRule.SameMinor
        };
        Kind.Dependencies.Add("CatLib-CustomStamps-" + version);
        Kind.Folders.Add(StampFiles.DecorativeFolder);
        Kind.Folders.Add(StampFiles.WeightFolder);
        Kind.Readme = Readme;
        Kind.PackChanged += _ => RaiseChanged();
    }

    public ContentPackKind Kind { get; }

    public int Generation { get; private set; }

    public bool IsLoading => _loading > 0;

    public IReadOnlyList<ContentPack> Packs => ContentPacks.Of(Kind);

    public IReadOnlyList<CustomStamp> Stamps => _stamps;

    public event Action Changed;

    public bool TryGet(string key, out CustomStamp stamp) => _byKey.TryGetValue(key ?? string.Empty, out stamp);

    public IEnumerable<CustomStamp> Active(StampKind kind) => _stamps.Where(stamp => stamp.Kind == kind && stamp.IsReady && stamp.IsActive);

    public void Load()
    {
        var generation = ++Generation;
        _stamps.Clear();
        _byKey.Clear();
        _cards.Clear();
        var packs = ContentPacks.Load(Kind, _log);
        foreach (var pack in packs)
        {
            try
            {
                Prepare(pack, generation);
            }
            catch (Exception exception)
            {
                _log.Error($"Reading the stamp pack {pack.Name} failed", exception);
            }
        }

        RaiseChanged();
    }

    public string CreatePack()
    {
        var language = CatLanguage.Current;
        var name = _texts.Find("stamps.NewPackName", language) ?? "My Stamps";
        var description = _texts.Find("stamps.NewPackDescription", language) ?? "My stamps for Cat Mail Co.";
        Kind.Template = _texts.FormatFor(language, "stamps.Template", name, description);
        var folder = ContentPacks.Create(Kind, name);
        foreach (var sample in Samples)
        {
            WriteSample(sample, Path.Combine(folder, StampFiles.FolderOf(sample.Kind), sample.File));
        }

        return folder;
    }

    private void WriteSample(Sample sample, string target)
    {
        var source = sample.Sprite;
        if (!GameImages.TryRead(sample.Sprite, out var image, out var problem))
        {
            source = $"{SampleAtlas} at {sample.X},{sample.Y}";
            if (!GameImages.TryReadArea(SampleAtlas, sample.X, sample.Y, sample.Width, sample.Height, out image, out var areaProblem))
            {
                _log.Info($"The sample stamp {sample.File} is the copy in the mod: {problem}; {areaProblem}");
                Copy(sample.Fallback, target);
                return;
            }
        }

        if (ClearShare(image) < MinClearShare)
        {
            _log.Info($"The sample stamp {sample.File} is the copy in the mod: the game's {source} is not a drawing on a clear background");
            Copy(sample.Fallback, target);
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? ".");
            File.WriteAllBytes(target, image.Trimmed().ToPng());
            _log.Info($"The sample stamp {sample.File} is the game's {source}, {image.Width}x{image.Height}");
        }
        catch (Exception exception)
        {
            _log.Warning($"The sample stamp {sample.File} could not be written: {exception.Message}");
        }
    }

    public string Summary(ContentPack pack, string language)
    {
        var count = _cards.TryGetValue(pack, out var card) ? card.Count : 0;
        return _texts.PluralFor(language, "stamps.Summary", count);
    }

    private void Prepare(ContentPack pack, int generation)
    {
        ContentPacks.CopyTexts(_texts, PackTextsPrefix, pack);
        var skipped = new List<(string File, string Reason)>();
        var files = StampFiles.Find(pack.Folder, skipped);
        var decorative = pack.Settings?.Gallery(StampsSection, DecorativeKey);
        var weight = pack.Settings?.Gallery(StampsSection, WeightKey);
        _cards[pack] = (new MenuGalleryPair(decorative, weight), files.Count);
        if (pack.Settings != null)
        {
            pack.Settings.Summary = language => Summary(pack, language);
        }

        foreach (var entry in skipped)
        {
            pack.AddProblem(Problem(entry.File, entry.Reason));
        }

        Interlocked(ref _loading, 1);
        var outline = HasOutline(pack.File.Get(OutlineKey));
        Task.Run(() => Decode(pack, files, outline)).ContinueWith(task => MainThread.Post(() =>
        {
            Interlocked(ref _loading, -1);
            if (generation != Generation)
            {
                return;
            }

            if (task.IsFaulted)
            {
                _log.Error($"Reading the images of {pack.Name} failed", task.Exception?.GetBaseException());
                return;
            }

            Finish(pack, task.Result);
        }));
    }

    public static bool HasOutline(string value) =>
        value == null || !(value.Equals("no", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                           value.Equals("off", StringComparison.OrdinalIgnoreCase) || value == "0");

    public static ImageData Prepare(ImageData image, bool outline)
    {
        var trimmed = image.Trimmed();
        var share = Margin + (outline ? OutlineShare : 0f);
        var scaled = trimmed.Scaled((int)(MaxSide * (1f - 2f * share)));
        var side = Math.Max(scaled.Width, scaled.Height) / (1f - 2f * share);
        var thickness = outline ? (int)Math.Round(side * OutlineShare) : 0;
        var margin = (int)Math.Ceiling(side * Margin);
        var outlined = thickness > 0 ? scaled.Outlined(thickness) : scaled;
        return outlined.Padded(outlined.Width + margin * 2, outlined.Height + margin * 2).PaddedToAspect(1f);
    }

    private static DecodedPack Decode(ContentPack pack, List<(StampKind Kind, string Path)> files, bool outline)
    {
        var result = new DecodedPack();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (kind, path) in files)
        {
            var key = StampFiles.Key(pack.Id, kind, path);
            var relative = Path.GetRelativePath(pack.Folder, path).Replace('\\', '/');
            if (!keys.Add(key))
            {
                result.Skipped.Add((relative, Duplicate, null));
                continue;
            }

            if (!ImageData.TryRead(path, out var image, out var problem))
            {
                result.Skipped.Add((relative, Unreadable, problem));
                continue;
            }

            result.Stamps.Add((kind, path, key, Prepare(image, outline)));
        }

        var images = result.Stamps.Select(stamp => stamp.Image).Take(IconStamps).ToList();
        result.Icon = images.Count == 0 ? null : ImageData.Collage(images, ThunderstorePackage.IconSide, IconGap);
        return result;
    }

    private void Finish(ContentPack pack, DecodedPack decoded)
    {
        foreach (var skipped in decoded.Skipped)
        {
            pack.AddProblem(Problem(skipped.File, skipped.Reason, skipped.Detail));
        }

        var made = new List<CustomStamp>();
        foreach (var (kind, path, key, image) in decoded.Stamps)
        {
            try
            {
                var stamp = new CustomStamp(pack, kind, path, key, image);
                stamp.Texture = image.ToTexture("Custom stamp " + key);
                stamp.Preview = ImageData.ToSprite(stamp.Texture);
                made.Add(stamp);
                _byKey[key] = stamp;
            }
            catch (Exception exception)
            {
                _log.Warning($"The stamp {key} could not be made: {exception.Message}");
            }
        }

        _stamps.AddRange(made);
        if (_cards.TryGetValue(pack, out var card))
        {
            _cards[pack] = (card.Galleries, made.Count);
            card.Galleries.Decorative?.SetImages(made.Where(stamp => stamp.Kind == StampKind.Decorative).Select(stamp => stamp.Preview));
            card.Galleries.Weight?.SetImages(made.Where(stamp => stamp.Kind == StampKind.Weight).Select(stamp => stamp.Preview));
        }

        if (decoded.Icon != null)
        {
            pack.UseGeneratedIcon(decoded.Icon);
        }

        _log.Info($"{pack.Name}: {made.Count(stamp => stamp.Kind == StampKind.Decorative)} decorative and {made.Count(stamp => stamp.Kind == StampKind.Weight)} weight stamp(s)" +
                  (decoded.Skipped.Count == 0 ? string.Empty : $", {decoded.Skipped.Count} file(s) left out"));
        RaiseChanged();
    }

    private string Problem(string file, string reason, string detail = null)
    {
        var language = CatLanguage.Current;
        var text = reason switch
        {
            StampFiles.NotSupported => _texts.FormatFor(language, "stamps.NotSupported", file),
            StampFiles.TooMany => _texts.FormatFor(language, "stamps.TooMany", file, StampFiles.MaxPerKind),
            Duplicate => _texts.FormatFor(language, "stamps.Duplicate", file),
            _ => _texts.FormatFor(language, "stamps.Unreadable", file, detail ?? string.Empty)
        };
        return text;
    }

    private string Readme(ContentPack pack)
    {
        var stamps = _stamps.Where(stamp => ReferenceEquals(stamp.Pack, pack)).ToList();
        var decorative = stamps.Count(stamp => stamp.Kind == StampKind.Decorative);
        var weight = stamps.Count(stamp => stamp.Kind == StampKind.Weight);
        return "A stamp pack for [Custom Stamps](https://thunderstore.io/c/cat-mail-co/p/CatLib/CustomStamps/): " +
               $"{decorative} decorative and {weight} weight stamp(s). Every player in a game needs the same pack to see its stamps.";
    }

    private void Copy(string resource, string target)
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CustomStamps.Template." + resource);
            if (stream == null)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? ".");
            using var file = File.Create(target);
            stream.CopyTo(file);
        }
        catch (Exception exception)
        {
            _log.Warning($"The sample stamp {resource} could not be copied: {exception.Message}");
        }
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception exception)
        {
            _log.Error("A handler of the stamp packs failed", exception);
        }
    }

    private static void Interlocked(ref int value, int delta) => System.Threading.Interlocked.Add(ref value, delta);

    public static float ClearShare(ImageData image)
    {
        var clear = 0;
        var rgba = image.Rgba;
        for (var index = 3; index < rgba.Length; index += 4)
        {
            if (rgba[index] < ImageData.TransparentBelow)
            {
                clear++;
            }
        }

        return (float)clear / (image.Width * image.Height);
    }

    private sealed record Sample(StampKind Kind, string File, string Sprite, int X, int Y, int Width, int Height, string Fallback);

    private sealed class DecodedPack
    {
        public List<(StampKind Kind, string Path, string Key, ImageData Image)> Stamps { get; } = new();

        public List<(string File, string Reason, string Detail)> Skipped { get; } = new();

        public ImageData Icon { get; set; }
    }

    private sealed class MenuGalleryPair
    {
        public MenuGalleryPair(CatLib.Config.MenuGallery decorative, CatLib.Config.MenuGallery weight)
        {
            Decorative = decorative;
            Weight = weight;
        }

        public CatLib.Config.MenuGallery Decorative { get; }

        public CatLib.Config.MenuGallery Weight { get; }
    }
}
