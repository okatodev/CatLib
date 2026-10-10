using CatLib.Assets;
using UnityEngine;

namespace CustomStamps.Packs;

public enum StampKind
{
    Decorative,
    Weight
}

public sealed class CustomStamp
{
    public CustomStamp(ContentPack pack, StampKind kind, string path, string key, ImageData image)
    {
        Pack = pack;
        Kind = kind;
        Path = path;
        Key = key;
        Image = image;
    }

    public ContentPack Pack { get; }

    public StampKind Kind { get; }

    public string Path { get; }

    public string Key { get; }

    public ImageData Image { get; }

    public Texture2D Texture { get; internal set; }

    public Sprite Preview { get; internal set; }

    public bool IsReady => Texture != null && !Texture.WasCollected && Preview != null && !Preview.WasCollected;

    public bool IsActive => Pack.IsActive;

    public override string ToString() => Key;
}
