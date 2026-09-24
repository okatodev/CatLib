namespace CatLib.Net;

public sealed record SessionSettingValue(string OwnerId, string Section, string Key, string Value)
{
    public string Id => OwnerId + "/" + Section + "/" + Key;
}
