namespace CatLib.Net;

public enum VersionRule : byte
{
    Exact = 1,
    SameMinor = 2,
    SameMajor = 3,
    Any = 4
}
