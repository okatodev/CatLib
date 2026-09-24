namespace CatLib.Config;

internal interface ISettingNode : ISetting
{
    void Refresh();

    void Detach();
}
