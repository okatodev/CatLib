using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CustomStamps.Game;

internal static class StampLook
{
    public static readonly string[] KnownTextureNames =
    {
        "_BaseMap", "_MainTex", "_BaseColorMap", "_Albedo", "_AlbedoMap", "_MainTexture", "_Texture", "_Tex", "_StampTexture", "_Stamp", "_Image", "_Sprite", "_Decal", "_DecalTex"
    };

    public static bool Apply(StampHelper helper, StampData baseData, Texture2D texture, List<string> report, out Texture original)
    {
        original = null;
        var renderer = helper._MainRenderer_k__BackingField ?? helper.GetComponentInChildren<MeshRenderer>(true);
        if (renderer == null)
        {
            report.Add("the stamp prefab has no mesh renderer");
            return false;
        }

        var start = helper._StartMaterial_k__BackingField ?? renderer.sharedMaterial;
        if (start == null)
        {
            report.Add("the stamp prefab has no material");
            return false;
        }

        original = FindImageTexture(start, baseData, report);
        if (original == null)
        {
            report.Add($"no texture of the stamp image was found in {Describe(start)}");
            return false;
        }

        var newStart = Replace(start, original, texture, report, "start material");
        if (ReferenceEquals(newStart, start))
        {
            return false;
        }

        helper._StartMaterial_k__BackingField = newStart;
        foreach (var each in helper.GetComponentsInChildren<Renderer>(true))
        {
            var shared = each.sharedMaterials;
            var changed = false;
            for (var index = 0; index < shared.Length; index++)
            {
                var material = shared[index];
                if (material == null)
                {
                    continue;
                }

                var replaced = material.Pointer == start.Pointer ? newStart : Replace(material, original, texture, report, "material of " + each.name);
                if (!ReferenceEquals(replaced, material))
                {
                    shared[index] = replaced;
                    changed = true;
                }
            }

            if (changed)
            {
                each.sharedMaterials = shared;
            }
        }

        var feedback = helper._FeedbackMaterial_k__BackingField;
        if (feedback != null)
        {
            helper._FeedbackMaterial_k__BackingField = Replace(feedback, original, texture, report, "feedback material");
        }

        return true;
    }

    public static Texture FindImageTexture(Material material, StampData baseData, List<string> report)
    {
        var preview = SafeName(baseData?._StampPreviewSprite_k__BackingField?.texture);
        Texture main = null;
        try
        {
            main = material.mainTexture;
        }
        catch (Exception)
        {
        }

        Texture best = null;
        var bestScore = -1;
        var bestArea = -1;
        foreach (var (name, texture) in Textures(material))
        {
            var score = 0;
            if (preview != null && SafeName(texture) == preview)
            {
                score += 4;
            }

            if (main != null && texture.Pointer == main.Pointer)
            {
                score += 2;
            }

            if (name.IndexOf("Base", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Albedo", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Stamp", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 1;
            }

            var area = texture.width * texture.height;
            if (score > bestScore || (score == bestScore && area > bestArea))
            {
                best = texture;
                bestScore = score;
                bestArea = area;
            }
        }

        if (best == null && main != null)
        {
            best = main;
        }

        return best;
    }

    public static string Describe(Material material)
    {
        if (material == null)
        {
            return "no material";
        }

        string shader;
        try
        {
            shader = material.shader == null ? "?" : material.shader.name;
        }
        catch (Exception)
        {
            shader = "?";
        }

        var textures = Textures(material).Select(pair => $"{pair.Name}={SafeName(pair.Texture)} {pair.Texture.width}x{pair.Texture.height}").ToList();
        return $"material {material.name} with shader {shader}, textures: {(textures.Count == 0 ? "none" : string.Join(", ", textures))}";
    }

    public static IEnumerable<(string Name, Texture Texture)> Textures(Material material)
    {
        var names = PropertyNames(material);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (!seen.Add(name))
            {
                continue;
            }

            Texture texture = null;
            try
            {
                if (material.HasTexture(name))
                {
                    texture = material.GetTexture(name);
                }
            }
            catch (Exception)
            {
                texture = null;
            }

            if (texture != null)
            {
                yield return (name, texture);
            }
        }
    }

    private static Material Replace(Material material, Texture original, Texture2D texture, List<string> report, string what)
    {
        var replaced = new List<string>();
        var clone = new Material(material);
        foreach (var (name, current) in Textures(material))
        {
            if (current.Pointer == original.Pointer)
            {
                clone.SetTexture(name, texture);
                replaced.Add(name);
            }
        }

        if (replaced.Count == 0)
        {
            try
            {
                if (material.mainTexture != null && material.mainTexture.Pointer == original.Pointer)
                {
                    clone.mainTexture = texture;
                    replaced.Add("mainTexture");
                }
            }
            catch (Exception)
            {
            }
        }

        if (replaced.Count == 0)
        {
            UnityEngine.Object.Destroy(clone);
            return material;
        }

        clone.name = material.name + " (custom stamp)";
        clone.hideFlags = HideFlags.DontUnloadUnusedAsset;
        report.Add($"{what}: {string.Join(", ", replaced)}");
        return clone;
    }

    private static List<string> PropertyNames(Material material)
    {
        var names = new List<string>();
        try
        {
            var found = material.GetTexturePropertyNames();
            if (found != null)
            {
                foreach (var name in found)
                {
                    names.Add(name);
                }
            }
        }
        catch (Exception)
        {
        }

        names.AddRange(KnownTextureNames);
        return names;
    }

    private static string SafeName(UnityEngine.Object value)
    {
        try
        {
            return value == null ? null : value.name;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
