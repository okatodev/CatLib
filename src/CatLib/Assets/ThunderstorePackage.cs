using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CatLib.Assets;

public sealed class PackageRequest
{
    public string Name { get; set; }

    public string Version { get; set; }

    public string Description { get; set; }

    public string WebsiteUrl { get; set; } = string.Empty;

    public IList<string> Dependencies { get; } = new List<string>();

    public string Readme { get; set; }

    public byte[] IconPng { get; set; }

    public string SourceFolder { get; set; }

    public string OutputPath { get; set; }
}

public sealed class PackageResult
{
    public bool Succeeded => Problems.Count == 0;

    public string Path { get; internal set; }

    public string PackageName { get; internal set; }

    public int Files { get; internal set; }

    public List<string> Problems { get; } = new();

    public List<string> Warnings { get; } = new();
}

public static class ThunderstorePackage
{
    public const int IconSide = 256;
    public const int MaxDescription = 250;
    public const int MaxName = 128;
    public const string ManifestFile = "manifest.json";
    public const string ReadmeFile = "README.md";
    public const string IconFile = "icon.png";

    private static readonly Regex NamePattern = new("^[A-Za-z0-9_]{1," + MaxName + "}$");
    private static readonly Regex VersionPattern = new("^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$");
    private static readonly HashSet<string> SkippedFiles = new(StringComparer.OrdinalIgnoreCase) { "Thumbs.db", "desktop.ini", ".DS_Store" };

    public static bool IsValidName(string name) => name != null && NamePattern.IsMatch(name);

    public static bool IsValidVersion(string version) => version != null && VersionPattern.IsMatch(version);

    public static string PackageName(string name)
    {
        var builder = new StringBuilder();
        var underscore = false;
        foreach (var character in (name ?? string.Empty).Trim())
        {
            if (character < 128 && char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                underscore = false;
            }
            else if (!underscore && builder.Length > 0)
            {
                builder.Append('_');
                underscore = true;
            }
        }

        var result = builder.ToString().TrimEnd('_');
        return result.Length > MaxName ? result.Substring(0, MaxName).TrimEnd('_') : result;
    }

    public static string FitDescription(string description)
    {
        var text = Regex.Replace((description ?? string.Empty).Trim(), "\\s+", " ");
        return text.Length <= MaxDescription ? text : text.Substring(0, MaxDescription - 1).TrimEnd() + "…";
    }

    public static string Manifest(string name, string version, string websiteUrl, string description, IEnumerable<string> dependencies)
    {
        var builder = new StringBuilder();
        builder.Append("{\n");
        builder.Append("  \"name\": ").Append(Quote(name)).Append(",\n");
        builder.Append("  \"version_number\": ").Append(Quote(version)).Append(",\n");
        builder.Append("  \"website_url\": ").Append(Quote(websiteUrl ?? string.Empty)).Append(",\n");
        builder.Append("  \"description\": ").Append(Quote(FitDescription(description))).Append(",\n");
        builder.Append("  \"dependencies\": [");
        var list = (dependencies ?? Enumerable.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        for (var index = 0; index < list.Count; index++)
        {
            builder.Append(index == 0 ? "\n    " : ",\n    ").Append(Quote(list[index]));
        }

        builder.Append(list.Count == 0 ? "]\n" : "\n  ]\n");
        builder.Append("}\n");
        return builder.ToString();
    }

    public static PackageResult Check(PackageRequest request)
    {
        var result = new PackageResult { PackageName = request.Name };
        if (!IsValidName(request.Name))
        {
            result.Problems.Add($"the package name \"{request.Name}\" may only have letters a-z and A-Z, digits and _, at most {MaxName} characters");
        }

        if (!IsValidVersion(request.Version))
        {
            result.Problems.Add($"the version \"{request.Version}\" must be Major.Minor.Patch, for example 1.0.0");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            result.Problems.Add("the description is empty");
        }
        else if (Regex.Replace(request.Description.Trim(), "\\s+", " ").Length > MaxDescription)
        {
            result.Warnings.Add($"the description is longer than {MaxDescription} characters and is cut in the manifest");
        }

        if (request.IconPng == null || request.IconPng.Length == 0)
        {
            result.Problems.Add("there is no icon");
        }
        else if (!ImageData.TryDecode(request.IconPng, out var icon) || icon.Width != IconSide || icon.Height != IconSide)
        {
            result.Problems.Add($"the icon must be a {IconSide}x{IconSide} PNG");
        }

        if (string.IsNullOrWhiteSpace(request.SourceFolder) || !Directory.Exists(request.SourceFolder))
        {
            result.Problems.Add("the folder of the pack does not exist");
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            result.Problems.Add("no place for the package was given");
        }

        return result;
    }

    public static PackageResult Build(PackageRequest request)
    {
        var result = Check(request);
        if (!result.Succeeded)
        {
            return result;
        }

        var output = Path.GetFullPath(request.OutputPath);
        var source = Path.GetFullPath(request.SourceFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
        var temporary = output + ".part";
        if (File.Exists(temporary))
        {
            File.Delete(temporary);
        }

        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            Add(zip, ManifestFile, Encoding.UTF8.GetBytes(Manifest(request.Name, request.Version, request.WebsiteUrl, request.Description, request.Dependencies)));
            Add(zip, ReadmeFile, Encoding.UTF8.GetBytes(request.Readme ?? "# " + request.Name + "\n\n" + request.Description + "\n"));
            Add(zip, IconFile, request.IconPng);
            result.Files = 3;
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(source, file).Replace('\\', '/');
                if (Skip(relative, output, file))
                {
                    continue;
                }

                Add(zip, relative, File.ReadAllBytes(file));
                result.Files++;
            }
        }

        if (File.Exists(output))
        {
            File.Delete(output);
        }

        File.Move(temporary, output);
        result.Path = output;
        return result;
    }

    private static bool Skip(string relative, string output, string file)
    {
        if (string.Equals(Path.GetFullPath(file), output, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var name = Path.GetFileName(relative);
        if (SkippedFiles.Contains(name) || name.StartsWith(".", StringComparison.Ordinal) || relative.Split('/').Any(part => part.StartsWith(".", StringComparison.Ordinal)))
        {
            return true;
        }

        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !relative.Contains('/') &&
               (string.Equals(name, ManifestFile, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, ReadmeFile, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, IconFile, StringComparison.OrdinalIgnoreCase));
    }

    private static void Add(ZipArchive zip, string path, byte[] data)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(data, 0, data.Length);
    }

    private static string Quote(string value)
    {
        var builder = new StringBuilder("\"");
        foreach (var character in value ?? string.Empty)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (character < 0x20)
                    {
                        builder.Append("\\u").Append(((int)character).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }
}
