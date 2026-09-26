using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CatLib.Saves;

public sealed class SaveDocument
{
    public const int CurrentFormat = 1;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public SaveDocument(string modId, string saveName)
    {
        ModId = modId;
        SaveName = saveName;
    }

    public int Format { get; private set; } = CurrentFormat;

    public string ModId { get; private set; }

    public string ModVersion { get; set; }

    public int DataVersion { get; set; }

    public string SaveName { get; private set; }

    public DateTime? WrittenAt { get; private set; }

    public JsonObject Values { get; private set; } = new();

    public string ToJson(DateTime writtenAt)
    {
        var root = new JsonObject
        {
            ["format"] = Format,
            ["mod"] = ModId,
            ["modVersion"] = ModVersion ?? string.Empty,
            ["dataVersion"] = DataVersion,
            ["save"] = SaveName,
            ["writtenAt"] = writtenAt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            ["values"] = JsonNode.Parse(Values.ToJsonString())
        };
        return root.ToJsonString(Options);
    }

    public static SaveDocument FromJson(string json)
    {
        JsonNode parsed;
        try
        {
            parsed = JsonNode.Parse(json ?? string.Empty);
        }
        catch (JsonException exception)
        {
            throw new FormatException("The file is not valid JSON: " + exception.Message, exception);
        }

        if (parsed is not JsonObject root)
        {
            throw new FormatException("The file is not a JSON object");
        }

        var format = Read(root, "format", 0);
        if (format <= 0)
        {
            throw new FormatException("The file has no format version");
        }

        var document = new SaveDocument(ReadText(root, "mod"), ReadText(root, "save"))
        {
            Format = format,
            ModVersion = ReadText(root, "modVersion"),
            DataVersion = Read(root, "dataVersion", 0)
        };

        if (DateTime.TryParse(ReadText(root, "writtenAt"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var writtenAt))
        {
            document.WrittenAt = writtenAt;
        }

        var values = root["values"];
        if (values != null && values is not JsonObject)
        {
            throw new FormatException("The values of the file are not a JSON object");
        }

        document.Values = values == null ? new JsonObject() : (JsonObject)JsonNode.Parse(values.ToJsonString());
        return document;
    }

    public SaveDocument Clone()
    {
        var copy = FromJson(ToJson(WrittenAt ?? DateTime.UtcNow));
        copy.WrittenAt = WrittenAt;
        return copy;
    }

    public IReadOnlyList<string> Keys() => Values.Select(pair => pair.Key).ToList();

    private static int Read(JsonObject root, string name, int fallback)
    {
        try
        {
            return root[name]?.GetValue<int>() ?? fallback;
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    private static string ReadText(JsonObject root, string name)
    {
        try
        {
            return root[name]?.GetValue<string>();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
