using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatLib.Config;

public abstract class MenuItem
{
    protected MenuItem(CatSettings owner, string section, string key, string description)
    {
        Owner = owner;
        Section = section;
        Key = key;
        Description = description;
    }

    public CatSettings Owner { get; }

    public string Section { get; }

    public string Key { get; }

    public string Id => Owner.OwnerId + "/" + Section + "/" + Key;

    public string Description { get; }

    public Func<string, string> Label { get; set; }

    public Func<string, string> Hint { get; set; }

    public int Revision { get; private set; }

    public bool IsHidden { get; private set; }

    public void SetHidden(bool hidden)
    {
        if (IsHidden != hidden)
        {
            IsHidden = hidden;
            Changed();
        }
    }

    protected void Changed() => Revision++;
}

public sealed class MenuGallery : MenuItem
{
    private readonly List<Sprite> _images = new();

    internal MenuGallery(CatSettings owner, string section, string key, string description)
        : base(owner, section, key, description)
    {
    }

    public IReadOnlyList<Sprite> Images => _images;

    public string EmptyText { get; set; }

    public void SetImages(IEnumerable<Sprite> images)
    {
        _images.Clear();
        if (images != null)
        {
            foreach (var image in images)
            {
                if (image != null)
                {
                    _images.Add(image);
                }
            }
        }

        Changed();
    }
}

public sealed class MenuButton : MenuItem
{
    internal MenuButton(CatSettings owner, string section, string key, Action clicked, string description)
        : base(owner, section, key, description)
    {
        Clicked = clicked;
    }

    public Action Clicked { get; }

    public Func<string, string> Caption { get; set; }
}
