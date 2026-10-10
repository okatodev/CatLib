using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using CatLib.Config;
using CatLib.Localization;
using CatLib.Logging;
using CatLib.Net;
using CatLib.UI;

namespace CatLib.Assets;

public static class ContentPacks
{
    public const string PackSection = "Pack";
    public const string EnabledKey = "Enabled";
    public const string FolderKey = "Folder";
    public const string BuildKey = "Build";
    public const string IconFile = "icon.png";
    public const string DefaultVersion = "1.0.0";
    public const int GeneratedIconGap = 12;

    private static readonly Dictionary<ContentPackKind, List<ContentPack>> Loaded = new();
    private static readonly object Sync = new();

    public static string PluginsFolder => Paths.PluginPath;

    public static string PackagesFolder => Path.Combine(Paths.BepInExRootPath, "CatLib", "Packages");

    public static string IconCacheFolder => Path.Combine(Paths.CachePath, "CatLib", "PackIcons");

    internal static CatLogger Log { get; set; }

    public static IReadOnlyList<ContentPack> Of(ContentPackKind kind)
    {
        lock (Sync)
        {
            return Loaded.TryGetValue(kind, out var packs) ? packs.ToList() : new List<ContentPack>();
        }
    }

    public static IReadOnlyList<string> FindFiles(ContentPackKind kind, string root)
    {
        if (kind == null || string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            return Array.Empty<string>();
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = Math.Max(0, kind.MaxDepth),
            MatchCasing = MatchCasing.CaseInsensitive,
            IgnoreInaccessible = true
        };
        return Directory.EnumerateFiles(root, kind.FileName, options)
            .Where(path => string.Equals(Path.GetFileName(path), kind.FileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static ContentPack Describe(ContentPackKind kind, string filePath)
    {
        var file = PackFile.Read(filePath);
        var folderName = Path.GetFileName(Path.GetDirectoryName(filePath)) ?? string.Empty;
        var name = file.Get(PackFile.NameKey, folderName);
        var package = ThunderstorePackage.PackageName(file.Get("package")) is { Length: > 0 } explicitName
            ? explicitName
            : ThunderstorePackage.PackageName(name) is { Length: > 0 } fromName ? fromName : ThunderstorePackage.PackageName(folderName);
        var id = kind.IdPrefix + "." + (package.Length > 0 ? package.ToLowerInvariant() : "pack" + Hash(name));
        var version = file.Get(PackFile.VersionKey, DefaultVersion);
        var pack = new ContentPack(kind, filePath, file, name, package, id, version);
        if (!ThunderstorePackage.IsValidVersion(version))
        {
            pack.AddProblem($"the version \"{version}\" is not Major.Minor.Patch, for example 1.0.0");
        }

        return pack;
    }

    public static IReadOnlyList<ContentPack> Load(ContentPackKind kind, CatLogger log = null)
    {
        if (kind == null)
        {
            throw new ArgumentNullException(nameof(kind));
        }

        log ??= Log;
        Unload(kind);
        var packs = new List<ContentPack>();
        var ids = new Dictionary<string, ContentPack>(StringComparer.Ordinal);
        foreach (var path in FindFiles(kind, PluginsFolder))
        {
            ContentPack pack;
            try
            {
                pack = Describe(kind, path);
            }
            catch (Exception exception)
            {
                log?.Warning($"[Packs] {path} could not be read: {exception.Message}");
                continue;
            }

            if (ids.TryGetValue(pack.Id, out var first))
            {
                log?.Warning($"[Packs] {pack.Folder} is the same pack as {first.Folder} ({pack.Id}), only the first one is used");
                continue;
            }

            ids[pack.Id] = pack;
            try
            {
                CreateCard(pack);
            }
            catch (Exception exception)
            {
                log?.Error($"[Packs] The Mods tab card of {pack.Name} could not be made", exception);
            }

            Declare(pack);
            packs.Add(pack);
            log?.Info($"[Packs] {pack.Name} {pack.Version} from {pack.Folder}, id {pack.Id}{(pack.IsEnabled ? string.Empty : ", turned off")}" +
                      (pack.Problems.Count == 0 ? string.Empty : "; " + string.Join("; ", pack.Problems)));
        }

        lock (Sync)
        {
            Loaded[kind] = packs;
        }

        log?.Info($"[Packs] {packs.Count} pack(s) with {kind.FileName} for {kind.OwnerId} in {PluginsFolder}");
        return packs;
    }

    public static void Unload(ContentPackKind kind)
    {
        List<ContentPack> previous;
        lock (Sync)
        {
            if (!Loaded.TryGetValue(kind, out previous))
            {
                return;
            }

            Loaded.Remove(kind);
        }

        foreach (var pack in previous)
        {
            CatNetwork.Undeclare(pack.Id);
            try
            {
                pack.Settings?.Dispose();
            }
            catch (Exception exception)
            {
                Log?.Warning($"[Packs] Releasing the card of {pack.Name} failed: {exception.Message}");
            }
        }
    }

    public static string Create(ContentPackKind kind, string name, string root = null)
    {
        root ??= PluginsFolder;
        var package = ThunderstorePackage.PackageName(name);
        if (package.Length == 0)
        {
            package = "MyPack";
        }

        var folder = Path.Combine(root, package);
        for (var number = 2; Directory.Exists(folder); number++)
        {
            folder = Path.Combine(root, package + "_" + number);
        }

        Directory.CreateDirectory(folder);
        foreach (var subfolder in kind.Folders)
        {
            Directory.CreateDirectory(Path.Combine(folder, subfolder));
        }

        var template = kind.Template ?? "name: {name}\nversion: 1.0.0\nauthor:\ndescription:\n";
        File.WriteAllText(Path.Combine(folder, kind.FileName), template.Replace("{name}", name ?? package), new UTF8Encoding(false));
        Log?.Info($"[Packs] Created a new pack {name} in {folder}");
        return folder;
    }

    public static PackageResult Build(ContentPack pack)
    {
        var language = UiText.LanguageCode;
        var result = new PackageResult { PackageName = pack.PackageName };
        if (!ThunderstorePackage.IsValidName(pack.PackageName))
        {
            result.Problems.Add(UiText.Get(UiText.PackBadName, language));
        }

        if (!ThunderstorePackage.IsValidVersion(pack.Version))
        {
            result.Problems.Add(UiText.Get(UiText.PackBadVersion, language));
        }

        if (string.IsNullOrWhiteSpace(pack.Description))
        {
            result.Problems.Add(UiText.Get(UiText.PackNoDescription, language));
        }

        var icon = IconBytes(pack);
        if (icon == null)
        {
            result.Problems.Add(UiText.Get(UiText.PackNoIcon, language));
        }

        if (!result.Succeeded)
        {
            return result;
        }

        var request = new PackageRequest
        {
            Name = pack.PackageName,
            Version = pack.Version,
            Description = pack.Description,
            WebsiteUrl = pack.WebsiteUrl ?? string.Empty,
            Readme = Readme(pack),
            IconPng = icon,
            SourceFolder = pack.Folder,
            OutputPath = Path.Combine(PackagesFolder, pack.PackageName + "-" + pack.Version + ".zip")
        };
        foreach (var dependency in pack.Kind.Dependencies)
        {
            request.Dependencies.Add(dependency);
        }

        return ThunderstorePackage.Build(request);
    }

    public static string Readme(ContentPack pack)
    {
        var builder = new StringBuilder();
        builder.Append("# ").Append(pack.Name).Append("\n\n");
        if (!string.IsNullOrWhiteSpace(pack.Description))
        {
            builder.Append(pack.Description.Trim()).Append("\n\n");
        }

        var extra = pack.Kind.Readme?.Invoke(pack);
        if (!string.IsNullOrWhiteSpace(extra))
        {
            builder.Append(extra.Trim()).Append("\n\n");
        }

        if (!string.IsNullOrWhiteSpace(pack.Author))
        {
            builder.Append("Made by ").Append(pack.Author.Trim()).Append(".\n");
        }

        return builder.ToString().TrimEnd() + "\n";
    }

    public static void OpenFolder(string folder)
    {
        try
        {
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            Log?.Warning($"[Packs] Opening {folder} failed: {exception.Message}");
        }
    }

    public static void RevealFile(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = false });
        }
        catch (Exception exception)
        {
            Log?.Warning($"[Packs] Showing {path} failed: {exception.Message}");
            OpenFolder(Path.GetDirectoryName(path));
        }
    }

    internal static void SetGeneratedIcon(ContentPack pack, ImageData icon)
    {
        pack.GeneratedIcon = icon;
        if (pack.Settings == null || icon == null || HasOwnIcon(pack))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(IconCacheFolder);
            var prefix = Safe(pack.Id) + "-";
            foreach (var old in Directory.EnumerateFiles(IconCacheFolder, prefix + "*.png"))
            {
                File.Delete(old);
            }

            var path = Path.Combine(IconCacheFolder, prefix + DateTime.UtcNow.Ticks + ".png");
            File.WriteAllBytes(path, icon.ToPng());
            pack.Settings.IconPath = path;
        }
        catch (Exception exception)
        {
            Log?.Warning($"[Packs] The icon of {pack.Name} could not be saved: {exception.Message}");
        }
    }

    internal static void RefreshDescription(ContentPack pack)
    {
        if (pack.Settings == null)
        {
            return;
        }

        var text = pack.BaseDescription ?? string.Empty;
        if (pack.Problems.Count > 0)
        {
            var problems = UiText.Format(UiText.PackProblems, UiText.LanguageCode, string.Join("; ", pack.Problems));
            text = string.IsNullOrWhiteSpace(text) ? problems : text.TrimEnd() + "\n" + problems;
        }

        pack.Settings.Description = text;
    }

    private static void CreateCard(ContentPack pack)
    {
        var configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, Safe(pack.Id) + ".cfg"), true);
        var settings = CatSettings.For(configFile, pack.Id, pack.Name, pack.Version);
        settings.ParentId = pack.Kind.OwnerId;
        settings.PluginDirectory = pack.Folder;
        settings.Author = string.IsNullOrWhiteSpace(pack.Author) ? null : pack.Author;
        settings.IconPath = HasOwnIcon(pack) ? Path.Combine(pack.Folder, IconFile) : null;
        pack.Settings = settings;
        pack.BaseDescription = pack.Description;
        RefreshDescription(pack);
        AddTexts(pack.Id);

        pack.EnabledSetting = settings.Local(PackSection, EnabledKey, true, "Use this pack. In a game with other players a pack works only when everyone has it on.");
        pack.EnabledSetting.Changed += (_, _) =>
        {
            Declare(pack);
            pack.Kind.RaiseChanged(pack);
        };
        settings.Button(PackSection, FolderKey, () => OpenFolder(pack.Folder));
        settings.Button(PackSection, BuildKey, () => BuildAndReport(pack));
    }

    private static void BuildAndReport(ContentPack pack)
    {
        var language = UiText.LanguageCode;
        PackageResult result;
        try
        {
            result = Build(pack);
        }
        catch (Exception exception)
        {
            Log?.Error($"[Packs] Building the package of {pack.Name} failed", exception);
            Notifications.Show(UiText.Format(UiText.PackBuildFailed, language, pack.Name, exception.Message));
            return;
        }

        if (!result.Succeeded)
        {
            var problems = string.Join("; ", result.Problems);
            Log?.Warning($"[Packs] The package of {pack.Name} was not built: {problems}");
            Notifications.Show(UiText.Format(UiText.PackBuildFailed, language, pack.Name, problems));
            return;
        }

        Log?.Info($"[Packs] Built {result.Path} with {result.Files} file(s)" + (result.Warnings.Count == 0 ? string.Empty : "; " + string.Join("; ", result.Warnings)));
        Notifications.Show(UiText.Format(UiText.PackBuilt, language, pack.Name, Path.GetFileName(result.Path)));
        RevealFile(result.Path);
    }

    private static void Declare(ContentPack pack)
    {
        if (pack.IsEnabled)
        {
            CatNetwork.Declare(pack.Id, pack.Name, pack.Version, pack.Kind.Policy, pack.Kind.VersionRule);
        }
        else
        {
            CatNetwork.Undeclare(pack.Id);
        }
    }

    private static byte[] IconBytes(ContentPack pack)
    {
        if (HasOwnIcon(pack) && ImageData.TryRead(Path.Combine(pack.Folder, IconFile), out var own, out _))
        {
            return own.Width == ThunderstorePackage.IconSide && own.Height == ThunderstorePackage.IconSide
                ? File.ReadAllBytes(Path.Combine(pack.Folder, IconFile))
                : own.Fitted(ThunderstorePackage.IconSide, ThunderstorePackage.IconSide).ToPng();
        }

        return pack.GeneratedIcon?.Fitted(ThunderstorePackage.IconSide, ThunderstorePackage.IconSide).ToPng();
    }

    private static bool HasOwnIcon(ContentPack pack) => File.Exists(Path.Combine(pack.Folder, IconFile));

    private static void AddTexts(string packId)
    {
        var source = UiText.Catalog;
        var target = CatLocalization.For(packId);
        foreach (var language in source.Languages)
        {
            Add(target, language, SettingTexts.SectionKey(PackSection), UiText.PackSection);
            Add(target, language, "setting." + PackSection + "." + EnabledKey, UiText.PackEnabled);
            Add(target, language, "setting." + PackSection + "." + EnabledKey + ".description", UiText.PackEnabledHint);
            Add(target, language, "setting." + PackSection + "." + FolderKey, UiText.PackFolder);
            Add(target, language, "setting." + PackSection + "." + FolderKey + ".description", UiText.PackFolderHint);
            Add(target, language, "setting." + PackSection + "." + FolderKey + ".button", UiText.PackFolderButton);
            Add(target, language, "setting." + PackSection + "." + BuildKey, UiText.PackBuild);
            Add(target, language, "setting." + PackSection + "." + BuildKey + ".description", UiText.PackBuildHint);
            Add(target, language, "setting." + PackSection + "." + BuildKey + ".button", UiText.PackBuildButton);
        }
    }

    private static void Add(TextCatalog target, string language, string key, string uiKey)
    {
        var text = UiText.Catalog.FindExact(UiText.KeyPrefix + uiKey, language);
        if (text != null)
        {
            target.Add(language, key, text);
        }
    }

    public static void CopyTexts(TextCatalog source, string prefix, ContentPack pack)
    {
        if (source == null || pack == null)
        {
            return;
        }

        var target = CatLocalization.For(pack.Id);
        foreach (var language in source.Languages)
        {
            foreach (var key in source.Keys(language))
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    target.Add(language, key.Substring(prefix.Length), source.FindExact(key, language));
                }
            }
        }
    }

    private static string Safe(string id)
    {
        var builder = new StringBuilder(id.Length);
        foreach (var character in id)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '.' || character == '_' || character == '-' ? character : '_');
        }

        return builder.ToString();
    }

    private static string Hash(string text)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in text ?? string.Empty)
            {
                hash = (hash ^ character) * 16777619u;
            }

            return hash.ToString("x8");
        }
    }
}
