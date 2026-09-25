using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace BoatTweaks;

public sealed class PatternLibrary
{
    public const string Extension = ".txt";

    public PatternLibrary(string directory)
    {
        Directory = directory;
    }

    public string Directory { get; }

    public IReadOnlyList<(string Name, DeckPattern Pattern)> Load(Action<string> warn)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var layouts = new List<(string Name, DeckPattern Pattern)>();
        foreach (var file in System.IO.Directory.GetFiles(Directory, "*" + Extension).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (IOException exception)
            {
                warn?.Invoke($"{Path.GetFileName(file)} could not be read: {exception.Message}");
                continue;
            }

            if (DeckPattern.TryParse(text, out var pattern, out var error))
            {
                layouts.Add((Path.GetFileNameWithoutExtension(file), pattern));
            }
            else
            {
                warn?.Invoke($"{Path.GetFileName(file)} is skipped: {error}");
            }
        }

        return layouts;
    }

    public string Save(DeckPattern pattern, IEnumerable<string> notes, DateTime now)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var baseName = "deck_" + now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var name = baseName;
        for (var suffix = 2; File.Exists(Path.Combine(Directory, name + Extension)); suffix++)
        {
            name = baseName + "_" + suffix.ToString(CultureInfo.InvariantCulture);
        }

        var lines = (notes ?? Enumerable.Empty<string>()).Where(note => !DeckPattern.IsGridLine(note)).Concat(pattern.RowTexts());
        File.WriteAllLines(Path.Combine(Directory, name + Extension), lines);
        return name;
    }
}
