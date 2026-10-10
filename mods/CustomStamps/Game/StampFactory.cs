using System;
using System.Collections.Generic;
using CatLib.Logging;
using CustomStamps.Packs;
using UnityEngine;

namespace CustomStamps.Game;

public sealed class StampFactory
{
    public const string HolderName = "CatLib Custom Stamps";
    public const float AspectTolerance = 0.02f;

    private readonly CatLogger _log;
    private readonly Dictionary<string, StampData> _built = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);
    private readonly HashSet<StampKind> _described = new();
    private GameObject _holder;
    private int _generation = -1;

    public StampFactory(CatLogger log)
    {
        _log = log;
    }

    public int Built => _built.Count;

    public bool IsCustom(StampData data) => data != null && StampFiles.IsCustomKey(SafeName(data));

    public StampData Get(CustomStamp stamp, int generation)
    {
        if (generation != _generation)
        {
            _built.Clear();
            _failed.Clear();
            _generation = generation;
        }

        if (_built.TryGetValue(stamp.Key, out var cached) && cached != null && !cached.WasCollected && cached._StampPrefab_k__BackingField != null)
        {
            return cached;
        }

        if (_failed.Contains(stamp.Key) || !stamp.IsReady)
        {
            return null;
        }

        var data = Build(stamp);
        if (data == null)
        {
            _failed.Add(stamp.Key);
            return null;
        }

        _built[stamp.Key] = data;
        return data;
    }

    public static StampData BaseFor(StampKind kind)
    {
        if (!Singleton<GameManager>.HasInstance())
        {
            return null;
        }

        var manager = Singleton<GameManager>.Instance;
        var stamps = kind == StampKind.Weight ? manager.WeightStamps : manager.DecorativeStamps;
        if (stamps == null)
        {
            return null;
        }

        foreach (var data in stamps)
        {
            if (data != null && data._StampPrefab_k__BackingField != null)
            {
                return data;
            }
        }

        return null;
    }

    public string DescribeBase(StampKind kind)
    {
        var data = BaseFor(kind);
        if (data == null)
        {
            return $"{kind}: the game has no such stamps loaded, open a level first";
        }

        var helper = data._StampPrefab_k__BackingField;
        var renderer = helper._MainRenderer_k__BackingField;
        var lines = new List<string>
        {
            $"{kind}: {data.name} ({data.GetIl2CppType().Name}, type {data.StampType}), prefab {helper.name}, renderer {(renderer == null ? "none" : renderer.name)}",
            "  start " + StampLook.Describe(helper._StartMaterial_k__BackingField),
            "  feedback " + StampLook.Describe(helper._FeedbackMaterial_k__BackingField),
            "  shared " + (renderer == null ? "none" : StampLook.Describe(renderer.sharedMaterial))
        };
        foreach (var filter in helper.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh;
            lines.Add(mesh == null
                ? $"  mesh filter {filter.name} without a mesh"
                : $"  mesh {mesh.name} on {filter.name}: {mesh.vertexCount} vertices, {mesh.subMeshCount} part(s), bounds {mesh.bounds.center} size {mesh.bounds.size}, {(mesh.isReadable ? "readable" : "not readable")}, scale {filter.transform.localScale}");
        }

        var preview = data._StampPreviewSprite_k__BackingField;
        lines.Add(preview == null ? "  no preview sprite" : $"  preview sprite {preview.name} {preview.rect.width}x{preview.rect.height} of {preview.texture?.name}");
        var image = StampLook.FindImageTexture(helper._StartMaterial_k__BackingField ?? renderer?.sharedMaterial, data, new List<string>());
        lines.Add(image == null ? "  no stamp image texture found" : $"  stamp image texture {image.name} {image.width}x{image.height}");
        return string.Join("\n", lines);
    }

    private StampData Build(CustomStamp stamp)
    {
        var baseData = BaseFor(stamp.Kind);
        if (baseData == null)
        {
            _log.Warning($"{stamp.Key} waits: the game has no {stamp.Kind.ToString().ToLowerInvariant()} stamps loaded yet");
            return null;
        }

        var report = new List<string>();
        GameObject template = null;
        try
        {
            EnsureHolder();
            template = UnityEngine.Object.Instantiate(baseData._StampPrefab_k__BackingField.gameObject, _holder.transform, false);
            template.name = stamp.Key;
            var helper = template.GetComponent<StampHelper>();
            if (helper == null)
            {
                throw new InvalidOperationException("the copy of the stamp prefab has no StampHelper");
            }

            var texture = stamp.Texture;
            if (!StampLook.Apply(helper, baseData, texture, report, out var original))
            {
                throw new InvalidOperationException(string.Join("; ", report));
            }

            var baseAspect = original.height > 0 ? (float)original.width / original.height : 1f;
            if (Math.Abs(baseAspect - 1f) > AspectTolerance)
            {
                var padded = stamp.Image.PaddedToAspect(baseAspect).ToTexture("Custom stamp " + stamp.Key + " " + baseAspect.ToString("0.00"));
                report.Clear();
                UnityEngine.Object.DestroyImmediate(template);
                template = UnityEngine.Object.Instantiate(baseData._StampPrefab_k__BackingField.gameObject, _holder.transform, false);
                template.name = stamp.Key;
                helper = template.GetComponent<StampHelper>();
                if (!StampLook.Apply(helper, baseData, padded, report, out _))
                {
                    throw new InvalidOperationException(string.Join("; ", report));
                }
            }

            if (StampMesh.Fit(helper, report) == 0)
            {
                report.Add("no mesh to show the picture on");
            }

            var data = UnityEngine.Object.Instantiate(baseData);
            data.name = stamp.Key;
            data.hideFlags = HideFlags.DontUnloadUnusedAsset;
            data._StampPrefab_k__BackingField = helper;
            data._StampPreviewSprite_k__BackingField = stamp.Preview;
            if (_described.Add(stamp.Kind))
            {
                _log.Info($"Custom {stamp.Kind.ToString().ToLowerInvariant()} stamps copy {baseData.name}: {string.Join("; ", report)}");
            }

            return data;
        }
        catch (Exception exception)
        {
            if (template != null)
            {
                UnityEngine.Object.Destroy(template);
            }

            _log.Warning($"The stamp {stamp.Key} could not be made from {baseData.name}: {exception.Message}");
            return null;
        }
    }

    private void EnsureHolder()
    {
        if (_holder != null && !_holder.WasCollected)
        {
            return;
        }

        _holder = new GameObject(HolderName);
        _holder.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(_holder);
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
