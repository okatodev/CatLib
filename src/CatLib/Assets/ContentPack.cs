using System;
using System.Collections.Generic;
using CatLib.Config;
using CatLib.Net;

namespace CatLib.Assets;

public sealed class ContentPackKind
{
    public ContentPackKind(string ownerId, string fileName, string idPrefix)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("The owner id must not be empty.", nameof(ownerId));
        }

        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(new[] { '/', '\\' }) >= 0)
        {
            throw new ArgumentException("The pack file must be a plain file name such as stamps.txt.", nameof(fileName));
        }

        OwnerId = ownerId;
        FileName = fileName;
        IdPrefix = string.IsNullOrWhiteSpace(idPrefix) ? ownerId : idPrefix;
    }

    public string OwnerId { get; }

    public string FileName { get; }

    public string IdPrefix { get; }

    public SessionPolicy Policy { get; set; } = SessionPolicy.RequiredOnAll;

    public VersionRule VersionRule { get; set; } = VersionRule.SameMinor;

    public IList<string> Dependencies { get; } = new List<string>();

    public IList<string> Folders { get; } = new List<string>();

    public string Template { get; set; }

    public int MaxDepth { get; set; } = 3;

    public string WebsiteUrl { get; set; } = string.Empty;

    public Func<ContentPack, string> Readme { get; set; }

    public event Action<ContentPack> PackChanged;

    internal void RaiseChanged(ContentPack pack) => PackChanged?.Invoke(pack);
}

public sealed class ContentPack
{
    private readonly List<string> _problems = new();

    internal ContentPack(ContentPackKind kind, string filePath, PackFile file, string name, string packageName, string id, string version)
    {
        Kind = kind;
        FilePath = filePath;
        Folder = System.IO.Path.GetDirectoryName(filePath);
        File = file;
        Name = name;
        PackageName = packageName;
        Id = id;
        Version = version;
        Author = file.Get(PackFile.AuthorKey, string.Empty);
        Description = file.Get(PackFile.DescriptionKey, string.Empty);
        WebsiteUrl = file.Get(PackFile.WebsiteKey, kind.WebsiteUrl ?? string.Empty);
        _problems.AddRange(file.Problems);
    }

    public ContentPackKind Kind { get; }

    public string FilePath { get; }

    public string Folder { get; }

    public PackFile File { get; }

    public string Name { get; }

    public string PackageName { get; }

    public string Id { get; }

    public string Version { get; }

    public string Author { get; }

    public string Description { get; }

    public string WebsiteUrl { get; }

    public CatSettings Settings { get; internal set; }

    public string IconPath => Settings?.IconPath;

    public ImageData GeneratedIcon { get; internal set; }

    public bool IsEnabled => EnabledSetting == null || EnabledSetting.Value;

    public bool IsActive => IsEnabled && CatNetwork.IsActive(Id);

    public IReadOnlyList<string> Problems => _problems;

    internal Setting<bool> EnabledSetting { get; set; }

    internal string BaseDescription { get; set; }

    public void AddProblem(string problem)
    {
        if (!string.IsNullOrWhiteSpace(problem))
        {
            _problems.Add(problem);
            ContentPacks.RefreshDescription(this);
        }
    }

    public void UseGeneratedIcon(ImageData icon) => ContentPacks.SetGeneratedIcon(this, icon);

    public override string ToString() => $"{Name} {Version} ({Id}) in {Folder}";
}
