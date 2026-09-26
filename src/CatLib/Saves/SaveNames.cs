using System;
using System.IO;
using System.Linq;

namespace CatLib.Saves;

public static class SaveNames
{
    public static string FromFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(fileName.Trim());
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim('.', ' ');
        return cleaned.Length == 0 ? null : cleaned;
    }

    public static string ForFile(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Id must not be empty.", nameof(id));
        }

        var invalid = Path.GetInvalidFileNameChars();
        return new string(id.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
    }
}
