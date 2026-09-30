namespace CatLib.Localization;

public readonly struct LocalText
{
    public LocalText(TextCatalog catalog, string key)
    {
        Catalog = catalog;
        Key = key;
    }

    public TextCatalog Catalog { get; }

    public string Key { get; }

    public string Value => In(CatLanguage.Current);

    public string In(string language) => Catalog == null ? Key : Catalog.Get(Key, language);

    public string Format(params object[] arguments) => Catalog == null ? Key : Catalog.Format(Key, arguments);

    public override string ToString() => Value;
}
