namespace CatLib.Net;

public enum ModMark : byte
{
    Same = 1,
    Missing = 2,
    OtherVersion = 3,
    NotOnHost = 4,
    Local = 5
}
