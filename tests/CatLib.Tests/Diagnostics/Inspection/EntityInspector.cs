using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CatLib.Logging;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatLib.Tests.Diagnostics.Inspection;

public sealed class EntityInspector
{
    public const float RayDistance = 40f;
    public const int MaxInstancesPerType = 12;
    public const int MaxComponentsPerTarget = 60;
    public const int MaxTextAssetLength = 6000;
    public const string DefaultFocusTypes = "EntityInteractableStore,EntityRepairWorkstation,EntityStorageLabel,ParcelDeliverer,ParcelDeliveryManager";
    public const int MaxReferencedComponents = 20;
    public const int MaxHierarchyLines = 200;

    private readonly string _directory;
    private readonly Func<string> _focusTypes;
    private readonly CatLogger _log;

    public EntityInspector(string directory, Func<string> focusTypes, CatLogger log)
    {
        _directory = directory;
        _focusTypes = focusTypes;
        _log = log;
    }

    public static System.Reflection.Assembly GameAssembly => typeof(Entity).Assembly;

    public string Dump()
    {
        var inspector = new InteropInspector(GameAssembly);
        var builder = new StringBuilder();
        var camera = FindCamera();
        var origin = camera == null ? Vector3.zero : camera.transform.position;
        var scene = SceneManager.GetActiveScene().name;

        builder.AppendLine("CatLib entity inspection");
        builder.AppendLine("Scene: " + scene);
        builder.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        builder.AppendLine("Camera: " + (camera == null ? "none" : camera.name + " at " + inspector.Format(origin, 0) + " looking " + inspector.Format(camera.transform.forward, 0)));
        builder.AppendLine("All cameras: " + DescribeCameras());
        builder.AppendLine();

        builder.AppendLine("== TARGET (what the camera looks at)");
        var target = camera == null ? null : FindTarget(camera, out _);
        if (target == null)
        {
            builder.AppendLine("Nothing within " + InspectionText.Scalar(RayDistance) + " m");
        }
        else
        {
            WriteTarget(builder, inspector, target, origin);
        }

        builder.AppendLine();
        builder.AppendLine("== ENTITIES IN THE SCENE");
        WriteEntityCounts(builder);

        builder.AppendLine();
        builder.AppendLine("== STORAGES THAT ARE NOT PARCELS");
        WriteStorageTable(builder, inspector, origin);

        foreach (var typeName in (_focusTypes() ?? DefaultFocusTypes).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(name => name.Trim()))
        {
            builder.AppendLine();
            WriteFocusType(builder, inspector, typeName, origin);
        }

        var referenced = inspector.ReferencedComponents.Take(MaxReferencedComponents).ToList();
        if (referenced.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("== REFERENCED COMPONENTS (fields above point to these)");
            foreach (var component in referenced)
            {
                var managedType = GameAssembly.GetType(component.GetIl2CppType().FullName);
                builder.AppendLine();
                builder.AppendLine($"[{managedType?.Name ?? component.GetIl2CppType().Name}] on {PathOf(component.transform)}{(component.gameObject.activeInHierarchy ? string.Empty : " (inactive)")}");
                inspector.WriteFields(builder, inspector.Wrap(component, managedType), 1);
            }
        }

        if (inspector.TextAssets.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("== TEXT ASSETS REFERENCED ABOVE");
            foreach (var asset in inspector.TextAssets)
            {
                var text = asset.text ?? string.Empty;
                builder.AppendLine($"-- \"{asset.name}\" ({text.Length} chars)");
                builder.AppendLine(text.Length > MaxTextAssetLength ? text.Substring(0, MaxTextAssetLength) + "\n...(clipped)" : text);
            }
        }

        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, $"entities_{Sanitize(scene)}_{DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.txt");
        File.WriteAllText(path, builder.ToString());
        _log.Message($"Entity inspection written to {path}{(target == null ? string.Empty : ", target " + target.name)}");
        return path;
    }

    public static Camera FindCamera()
    {
        var main = Camera.main;
        if (main != null)
        {
            return main;
        }

        Camera screen = null;
        Camera any = null;
        foreach (var camera in Camera.allCameras)
        {
            if (camera == null || !camera.isActiveAndEnabled)
            {
                continue;
            }

            if (any == null || camera.depth > any.depth)
            {
                any = camera;
            }

            if (camera.targetTexture == null && (screen == null || camera.depth > screen.depth))
            {
                screen = camera;
            }
        }

        return screen ?? any;
    }

    public static string DescribeCameras()
    {
        var parts = new List<string>();
        foreach (var camera in Camera.allCameras)
        {
            if (camera == null)
            {
                continue;
            }

            var target = camera.targetTexture == null ? "screen" : "texture " + camera.targetTexture.name;
            parts.Add($"{camera.name} (depth {InspectionText.Scalar(camera.depth)}, {(camera.isActiveAndEnabled ? "active" : "inactive")}, {target}{(camera.CompareTag("MainCamera") ? ", MainCamera" : string.Empty)})");
        }

        return parts.Count == 0 ? "none" : string.Join("; ", parts);
    }

    public static GameObject FindTarget(Camera camera, out float distance)
    {
        distance = 0f;
        var hits = Physics.RaycastAll(camera.transform.position, camera.transform.forward, RayDistance);
        GameObject fallback = null;
        var fallbackDistance = float.MaxValue;
        var bestDistance = float.MaxValue;
        GameObject best = null;

        foreach (var hit in hits)
        {
            var collider = hit.collider;
            if (collider == null)
            {
                continue;
            }

            var entity = collider.GetComponentInParent<Entity>(true);
            if (entity != null)
            {
                if (entity.TryCast<PlayerEntityBase>() != null || hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                best = entity.gameObject;
            }
            else if (hit.distance < fallbackDistance)
            {
                fallbackDistance = hit.distance;
                fallback = collider.gameObject;
            }
        }

        distance = best != null ? bestDistance : fallbackDistance;
        return best ?? fallback;
    }

    public static string PathOf(Transform transform)
    {
        var parts = new List<string>();
        for (var current = transform; current != null && parts.Count < 12; current = current.parent)
        {
            parts.Add(current.name);
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static void WriteTarget(StringBuilder builder, InteropInspector inspector, GameObject target, Vector3 origin)
    {
        builder.AppendLine("Object: " + PathOf(target.transform));
        builder.AppendLine("Distance: " + InspectionText.Scalar(Vector3.Distance(origin, target.transform.position)) + " m, position " + inspector.Format(target.transform.position, 0));

        var written = 0;
        var skipped = 0;
        foreach (var component in target.GetComponentsInChildren<Component>(true))
        {
            if (component == null)
            {
                continue;
            }

            var managedType = GameAssembly.GetType(component.GetIl2CppType().FullName);
            if (managedType == null)
            {
                continue;
            }

            if (written >= MaxComponentsPerTarget)
            {
                skipped++;
                continue;
            }

            written++;
            builder.AppendLine();
            builder.AppendLine($"[{managedType.Name}] on {PathOf(component.transform)}");
            inspector.WriteFields(builder, inspector.Wrap(component, managedType), 1);
        }

        if (skipped > 0)
        {
            builder.AppendLine($"...{skipped} more game components not written");
        }

        builder.AppendLine();
        builder.AppendLine("-- Hierarchy (local position, layer, components)");
        var lines = 0;
        WriteHierarchy(builder, inspector, target.transform, 0, ref lines);
        if (lines >= MaxHierarchyLines)
        {
            builder.AppendLine($"...clipped at {MaxHierarchyLines} objects");
        }
    }

    private static void WriteHierarchy(StringBuilder builder, InteropInspector inspector, Transform transform, int depth, ref int lines)
    {
        if (lines >= MaxHierarchyLines)
        {
            return;
        }

        lines++;
        var gameObject = transform.gameObject;
        var components = new List<string>();
        foreach (var component in gameObject.GetComponents<Component>())
        {
            if (component == null)
            {
                continue;
            }

            var name = component.GetIl2CppType().Name;
            if (name == "Transform" || name == "RectTransform")
            {
                continue;
            }

            var box = component.TryCast<BoxCollider>();
            var renderer = component.TryCast<Renderer>();
            if (box != null)
            {
                name += $"(size {inspector.Format(box.size, 0)}, center {inspector.Format(box.center, 0)})";
            }
            else if (renderer != null)
            {
                var material = renderer.sharedMaterial;
                name += $"({(renderer.enabled ? "on" : "off")}, {(material == null ? "no material" : material.name)})";
            }

            components.Add(name);
        }

        var layer = LayerMask.LayerToName(gameObject.layer);
        builder.Append(InspectionText.Indent(depth + 1))
            .Append(gameObject.name)
            .Append(gameObject.activeSelf ? string.Empty : " (inactive)")
            .Append("  ").Append(inspector.Format(transform.localPosition, 0))
            .Append(transform.localScale == Vector3.one ? string.Empty : " scale " + inspector.Format(transform.localScale, 0))
            .Append("  [").Append(string.IsNullOrEmpty(layer) ? gameObject.layer.ToString(CultureInfo.InvariantCulture) : layer).Append(']')
            .Append(components.Count == 0 ? string.Empty : "  " + string.Join(", ", components))
            .AppendLine();

        for (var index = 0; index < transform.childCount; index++)
        {
            WriteHierarchy(builder, inspector, transform.GetChild(index), depth + 1, ref lines);
        }
    }

    private static void WriteStorageTable(StringBuilder builder, InteropInspector inspector, Vector3 origin)
    {
        var stores = UnityEngine.Object.FindObjectsByType(Il2CppType.Of<EntityInteractableStore>(), FindObjectsInactive.Include, FindObjectsSortMode.None);
        var rows = new List<(float Distance, string Line)>();
        foreach (var item in stores)
        {
            var store = item.TryCast<EntityInteractableStore>();
            if (store == null || store.GetComponentInParent<EntityParcel>(true) != null)
            {
                continue;
            }

            var fields = InteropInspector.FieldProperties(typeof(EntityInteractableStore))
                .ToDictionary(property => InspectionText.MemberName(property.Name), property => property);
            string Read(string name) => fields.TryGetValue(name, out var property) ? inspector.Format(property.GetValue(store), InteropInspector.MaxDepth) : "?";

            var distance = Vector3.Distance(origin, store.transform.position);
            rows.Add((distance, $"{InspectionText.Scalar(distance),8} m  {PathOf(store.transform)}{(store.gameObject.activeInHierarchy ? string.Empty : " (inactive)")}\n" +
                $"            type {Read("StorageType")}, height {Read("Height")}, max {Read("StorageMaximumHeight")}, approved max {Read("StorageMaximumApprovedHeight")}, " +
                $"max parcels {Read("MaximumParcelAmount")}, cell {Read("_gridColumnSize")} x {Read("_gridRowSize")}, stored {Read("_storedEntities").Split(']')[0].TrimStart('[')}, prefilled {Read("PrefilledStorageTextAsset")}"));
        }

        foreach (var row in rows.OrderBy(row => row.Distance))
        {
            builder.AppendLine(row.Line);
        }

        builder.AppendLine($"Storages that are not parcels: {rows.Count}");
    }

    private static void WriteEntityCounts(StringBuilder builder)
    {
        var entities = UnityEngine.Object.FindObjectsByType(Il2CppType.Of<Entity>(), FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var entity in entities)
        {
            var name = entity.GetIl2CppType().Name;
            counts[name] = counts.TryGetValue(name, out var count) ? count + 1 : 1;
        }

        foreach (var pair in counts)
        {
            builder.AppendLine($"{pair.Value,5}  {pair.Key}");
        }

        builder.AppendLine($"Total active entities: {entities.Length}");
    }

    private static void WriteFocusType(StringBuilder builder, InteropInspector inspector, string typeName, Vector3 origin)
    {
        var managedType = GameAssembly.GetType(typeName);
        builder.AppendLine($"== {typeName}");
        if (managedType == null || !typeof(UnityEngine.Object).IsAssignableFrom(managedType))
        {
            builder.AppendLine("Unknown type or not a Unity object");
            return;
        }

        var il2CppType = Il2CppType.From(managedType);
        var found = typeof(Component).IsAssignableFrom(managedType)
            ? UnityEngine.Object.FindObjectsByType(il2CppType, FindObjectsInactive.Include, FindObjectsSortMode.None)
            : Resources.FindObjectsOfTypeAll(il2CppType);

        var instances = found
            .Select(item => (Item: item, Distance: DistanceOf(item, origin)))
            .OrderBy(pair => pair.Distance)
            .ToList();
        builder.AppendLine($"Instances: {instances.Count}, nearest {Math.Min(instances.Count, MaxInstancesPerType)} written");

        foreach (var (item, distance) in instances.Take(MaxInstancesPerType))
        {
            var component = item.TryCast<Component>();
            var where = component == null ? item.name : PathOf(component.transform) + (component.gameObject.activeInHierarchy ? string.Empty : " (inactive)");
            builder.AppendLine();
            builder.AppendLine($"- {where}, {(float.IsInfinity(distance) ? "no position" : InspectionText.Scalar(distance) + " m")}");
            inspector.WriteFields(builder, inspector.Wrap(item, managedType), 1);
        }
    }

    private static float DistanceOf(UnityEngine.Object item, Vector3 origin)
    {
        var component = item.TryCast<Component>();
        return component == null ? float.PositiveInfinity : Vector3.Distance(origin, component.transform.position);
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string((name ?? "scene").Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "scene" : cleaned;
    }
}
