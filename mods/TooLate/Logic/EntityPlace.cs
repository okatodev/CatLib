using System.Collections.Generic;
using System.Linq;

namespace TooLate.Logic;

public readonly struct EntityPlace
{
    public EntityPlace(uint id, float x, float y, float z)
    {
        Id = id;
        X = x;
        Y = y;
        Z = z;
    }

    public uint Id { get; }

    public float X { get; }

    public float Y { get; }

    public float Z { get; }

    public override string ToString() => $"{Id} ({X:0.###}, {Y:0.###}, {Z:0.###})";
}

public static class EntityPlaces
{
    public const uint EndMark = uint.MaxValue;

    public static IReadOnlyList<EntityPlace> Merge(IEnumerable<EntityPlace> scene, IReadOnlyList<EntityPlace> saved)
    {
        var savedIds = new HashSet<uint>(saved.Select(place => place.Id));
        var result = new List<EntityPlace>();
        var sceneIds = new HashSet<uint>();
        foreach (var place in scene)
        {
            if (place.Id != 0 && place.Id != EndMark && !savedIds.Contains(place.Id) && sceneIds.Add(place.Id))
            {
                result.Add(place);
            }
        }

        var seen = new HashSet<uint>();
        foreach (var place in saved)
        {
            if (place.Id != 0 && place.Id != EndMark && seen.Add(place.Id))
            {
                result.Add(place);
            }
        }

        return result;
    }
}
