using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CatLib.Assets;
using CatLib.Logging;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace CatLib.Tests.Diagnostics;

internal sealed class ConstraintStampExporter
{
    public static readonly string[] Keywords = { "curse", "cursed", "corrupt", "purple" };

    private readonly string _root;
    private readonly CatLogger _log;
    private readonly HashSet<IntPtr> _writtenTextures = new();
    private readonly HashSet<IntPtr> _seenStamps = new();
    private readonly Dictionary<IntPtr, ImageData> _pictures = new();
    private StringBuilder _report;
    private string _directory;
    private int _written;
    private int _failed;

    public ConstraintStampExporter(string root, CatLogger log)
    {
        _root = root;
        _log = log;
    }

    public static bool MatchesKeyword(string name) =>
        !string.IsNullOrEmpty(name) && Keywords.Any(keyword => name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);

    public string Export()
    {
        if (!Singleton<EntityPropertiesManager>.HasInstance())
        {
            return "only in a level";
        }

        _directory = Path.Combine(_root, "ConstraintStamps_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(_directory);
        _writtenTextures.Clear();
        _seenStamps.Clear();
        _pictures.Clear();
        _written = 0;
        _failed = 0;
        _report = new StringBuilder();
        _report.AppendLine("CatLib constraint stamp export");
        _report.AppendLine();

        var manager = Singleton<EntityPropertiesManager>.Instance;
        Section("Behavior constraints of EntityPropertiesManager");
        if (manager.BehaviorConstraints != null)
        {
            foreach (var settings in manager.BehaviorConstraints)
            {
                if (settings == null)
                {
                    continue;
                }

                Line($"{settings.Constraint}: unlocked {Flag(settings.IsUnlocked)}, unlocked now {Flag(settings.IsCurrentlyUnlocked)}, " +
                     $"deteriorates in {Number(settings.TimeToDeteriorate)} s, from boat {Number(settings.ProbabilityFromBoat)}, from customer {Number(settings.ProbabilityFromCustomer)}");
                Stamp("behavior_" + settings.Constraint, settings.Stamp);
            }
        }

        Section("Storage constraints of EntityPropertiesManager");
        if (manager.Constraints != null)
        {
            foreach (var settings in manager.Constraints)
            {
                if (settings == null)
                {
                    continue;
                }

                Line($"{settings.Constraint}: unlocked {Flag(settings.IsUnlocked)}, unlocked now {Flag(settings.IsCurrentlyUnlocked)}");
                Stamp("storage_" + settings.Constraint, settings.Stamp);
            }
        }

        Section("Stamps the game puts on a cursed parcel (GetStamps(None, Corrupted))");
        try
        {
            var stamps = manager.GetStamps(StorageConstraint.None, BehaviorConstraint.Corrupted);
            if (stamps == null || stamps.Count == 0)
            {
                Line("none");
            }
            else
            {
                for (var index = 0; index < stamps.Count; index++)
                {
                    Stamp("cursed_" + index.ToString(CultureInfo.InvariantCulture), stamps[index]);
                }
            }
        }
        catch (Exception exception)
        {
            Line("failed: " + exception.Message);
        }

        Section("Constraint stamps of GameManager");
        if (Singleton<GameManager>.HasInstance() && Singleton<GameManager>.Instance.ConstraintStamps != null)
        {
            var index = 0;
            foreach (var stamp in Singleton<GameManager>.Instance.ConstraintStamps)
            {
                Stamp("game_" + index.ToString(CultureInfo.InvariantCulture), stamp);
                index++;
            }
        }
        else
        {
            Line("none");
        }

        Section("Loaded sprites and textures named like a curse (" + string.Join(", ", Keywords) + ")");
        var found = 0;
        foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Sprite>()))
        {
            var sprite = item.TryCast<Sprite>();
            if (sprite != null && MatchesKeyword(sprite.name))
            {
                found++;
                Line($"sprite {sprite.name} on {(sprite.texture == null ? "no texture" : sprite.texture.name)}");
                WriteSprite("found_" + sprite.name, sprite);
            }
        }

        foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Texture2D>()))
        {
            var texture = item.TryCast<Texture2D>();
            if (texture != null && MatchesKeyword(texture.name))
            {
                found++;
                Line($"texture {texture.name} {texture.width}x{texture.height}");
                WriteTexture("found_" + texture.name, texture);
            }
        }

        foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Material>()))
        {
            var material = item.TryCast<Material>();
            if (material != null && MatchesKeyword(material.name))
            {
                found++;
                Line($"material {material.name}");
                Material("found_" + material.name, material, "  ");
            }
        }

        if (found == 0)
        {
            Line("none");
        }

        File.WriteAllText(Path.Combine(_directory, "index.txt"), _report.ToString(), Encoding.UTF8);
        _log.Message($"Exported {_written} picture(s) of constraint stamps to {_directory}, {_failed} failed, {_seenStamps.Count} stamp(s) described in index.txt");
        return $"{_written} picture(s) in {_directory}";
    }

    private void Stamp(string prefix, StampData stamp)
    {
        if (stamp == null)
        {
            Line("  no stamp");
            return;
        }

        if (!_seenStamps.Add(stamp.Pointer))
        {
            Line($"  stamp {stamp.name}, described above");
            return;
        }

        var details = new List<string> { "type " + stamp.StampType };
        var behavior = stamp.TryCast<StampDataBehaviorConstraint>();
        if (behavior != null)
        {
            details.Add("behavior " + behavior.Constraint);
        }

        var storage = stamp.TryCast<StampDataStorageConstraint>();
        if (storage != null)
        {
            details.Add("storage " + storage.Constraint);
        }

        Line($"  stamp {stamp.name}: {string.Join(", ", details)}");
        var preview = stamp.StampPreviewSprite;
        if (preview == null)
        {
            Line("    preview sprite: none");
        }
        else
        {
            Rect rect;
            try
            {
                rect = preview.textureRect;
            }
            catch (Exception)
            {
                rect = preview.rect;
            }

            Line($"    preview sprite {preview.name} on {(preview.texture == null ? "no texture" : preview.texture.name)} at {Number(rect.x)},{Number(rect.y)} {Number(rect.width)}x{Number(rect.height)}" +
                 $" -> {WriteSprite(prefix + "_preview", preview)}");
        }

        var helper = stamp.StampPrefab;
        if (helper == null)
        {
            Line("    prefab: none");
            return;
        }

        Line($"    prefab {helper.name}: shown by day {Flag(helper.ShownDuringDay)}, by night {Flag(helper.ShownDuringNight)}");
        Material(prefix + "_start", helper.StartMaterial, "    start ");
        Material(prefix + "_feedback", helper.FeedbackMaterial, "    feedback ");
        if (helper.MainRenderer != null)
        {
            Material(prefix + "_renderer", helper.MainRenderer.sharedMaterial, "    renderer ");
            var filter = helper.MainRenderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                var bounds = filter.sharedMesh.bounds;
                Line($"    mesh {filter.sharedMesh.name}, {filter.sharedMesh.vertexCount} vertices, size {Number(bounds.size.x)} x {Number(bounds.size.y)} x {Number(bounds.size.z)}");
                Mark(prefix + "_mark", filter.sharedMesh, helper.MainRenderer.sharedMaterial != null ? helper.MainRenderer.sharedMaterial : helper.StartMaterial);
            }
        }
    }

    private void Mark(string prefix, Mesh mesh, Material material)
    {
        if (material == null || !material.HasProperty("_BaseMap"))
        {
            Line("    mark: the material has no _BaseMap");
            return;
        }

        var texture = material.GetTexture("_BaseMap");
        if (texture == null)
        {
            Line("    mark: no picture in _BaseMap");
            return;
        }

        if (!MeshPictureArea.TryRead(mesh, out var min, out var max, out var problem))
        {
            Line("    mark: " + problem);
            return;
        }

        var scale = material.GetTextureScale("_BaseMap");
        var offset = material.GetTextureOffset("_BaseMap");
        min = Vector2.Scale(min, scale) + offset;
        max = Vector2.Scale(max, scale) + offset;
        try
        {
            if (!_pictures.TryGetValue(texture.Pointer, out var whole))
            {
                whole = GameImages.Read(texture);
                _pictures[texture.Pointer] = whole;
            }

            var left = Math.Clamp((int)Math.Floor(min.x * whole.Width), 0, whole.Width - 1);
            var right = Math.Clamp((int)Math.Ceiling(max.x * whole.Width), left + 1, whole.Width);
            var top = Math.Clamp((int)Math.Floor((1f - max.y) * whole.Height), 0, whole.Height - 1);
            var bottom = Math.Clamp((int)Math.Ceiling((1f - min.y) * whole.Height), top + 1, whole.Height);
            var file = Safe(prefix) + ".png";
            File.WriteAllBytes(Path.Combine(_directory, file), whole.Cropped(new ImageArea(left, top, right - left, bottom - top)).ToPng());
            _written++;
            Line($"    mark: picture area {Number(min.x)}..{Number(max.x)} x {Number(min.y)}..{Number(max.y)} of {texture.name}, pixels {left},{top} {right - left}x{bottom - top} from the top left -> {file}");
        }
        catch (Exception exception)
        {
            _failed++;
            Line("    mark: failed, " + exception.Message);
        }
    }

    private void Material(string prefix, Material material, string indent)
    {
        if (material == null)
        {
            Line(indent + "material: none");
            return;
        }

        Line($"{indent}material {material.name}, shader {(material.shader == null ? "none" : material.shader.name)}");
        string[] names;
        try
        {
            names = material.GetTexturePropertyNames().ToArray();
        }
        catch (Exception exception)
        {
            Line($"{indent}  textures: failed, {exception.Message}");
            return;
        }

        foreach (var name in names)
        {
            Texture texture;
            try
            {
                texture = material.GetTexture(name);
            }
            catch (Exception)
            {
                continue;
            }

            if (texture == null)
            {
                continue;
            }

            Line($"{indent}  {name}: {texture.name} {texture.width}x{texture.height} -> {WriteTexture(prefix + name, texture)}");
        }

        foreach (var color in new[] { "_BaseColor", "_Color", "_EmissionColor" })
        {
            if (material.HasProperty(color))
            {
                var value = material.GetColor(color);
                Line($"{indent}  {color}: {Number(value.r)} {Number(value.g)} {Number(value.b)} {Number(value.a)}");
            }
        }
    }

    private string WriteSprite(string prefix, Sprite sprite)
    {
        try
        {
            var file = Safe(prefix) + ".png";
            File.WriteAllBytes(Path.Combine(_directory, file), GameImages.Read(sprite).ToPng());
            _written++;
            return file;
        }
        catch (Exception exception)
        {
            _failed++;
            return "failed: " + exception.Message;
        }
    }

    private string WriteTexture(string prefix, Texture texture)
    {
        var file = Safe(prefix + "_" + texture.name) + ".png";
        if (!_writtenTextures.Add(texture.Pointer))
        {
            return "written before";
        }

        try
        {
            File.WriteAllBytes(Path.Combine(_directory, file), GameImages.Read(texture).ToPng());
            _written++;
            return file;
        }
        catch (Exception exception)
        {
            _failed++;
            return "failed: " + exception.Message;
        }
    }

    private void Section(string title)
    {
        _report.AppendLine();
        _report.AppendLine("== " + title);
    }

    private void Line(string text) => _report.AppendLine(text);

    private static string Flag(bool value) => value ? "yes" : "no";

    private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Safe(string name)
    {
        var builder = new StringBuilder();
        foreach (var character in name ?? "unnamed")
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_');
        }

        return builder.ToString();
    }
}
