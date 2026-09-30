using System.Collections.Generic;

namespace CatLib.Localization;

public sealed class TranslationLoad
{
    public TranslationLoad(int texts, IReadOnlyList<string> languages, IReadOnlyList<string> errors)
    {
        Texts = texts;
        Languages = languages;
        Errors = errors;
    }

    public int Texts { get; }

    public IReadOnlyList<string> Languages { get; }

    public IReadOnlyList<string> Errors { get; }
}
