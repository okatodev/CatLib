namespace CatLib.Config;

public sealed record SettingValueProblem(string OwnerId, string Section, string Key, string RawValue, string EffectiveValue, string Reason, ISetting Setting);
