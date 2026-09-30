using TMPro;
using UnityEngine;

namespace CatLib.UI;

public sealed class FoldoutStyle
{
    public TMP_Text TextTemplate { get; set; }

    public Sprite HeaderSprite { get; set; }

    public Sprite BodySprite { get; set; }

    public Sprite ArrowSprite { get; set; }

    public Sprite TapeSprite { get; set; }

    public Sprite RowSprite { get; set; }

    public float HeaderWidth { get; set; } = 640f;

    public float HeaderHeight { get; set; } = 60f;

    public float BodyWidth { get; set; } = 900f;

    public float MaxBodyHeight { get; set; } = 430f;

    public float BodyGap { get; set; } = 8f;

    public float Padding { get; set; } = 26f;

    public float SectionHeight { get; set; } = 44f;

    public float RowHeight { get; set; } = 34f;

    public float SectionGap { get; set; } = 10f;

    public float RowIndent { get; set; } = 40f;

    public float TitleSize { get; set; } = 27f;

    public float SummarySize { get; set; } = 21f;

    public float SectionSize { get; set; } = 23f;

    public float RowSize { get; set; } = 20f;

    public Color Text { get; set; } = new(0.325f, 0.247f, 0.2f, 1f);

    public Color Good { get; set; } = new(0.25f, 0.42f, 0.2f, 1f);

    public Color Warning { get; set; } = new(0.68f, 0.43f, 0.1f, 1f);

    public Color Bad { get; set; } = new(0.66f, 0.2f, 0.15f, 1f);

    public Color Muted { get; set; } = new(0.325f, 0.247f, 0.2f, 0.55f);

    public Color SectionTint { get; set; } = new(0.325f, 0.247f, 0.2f, 0.09f);

    public Color ClickTint { get; set; } = new(0.325f, 0.247f, 0.2f, 0.05f);

    public Color ToneColor(FoldoutTone tone) => tone switch
    {
        FoldoutTone.Good => Good,
        FoldoutTone.Warning => Warning,
        FoldoutTone.Bad => Bad,
        FoldoutTone.Muted => Muted,
        _ => Text
    };
}
