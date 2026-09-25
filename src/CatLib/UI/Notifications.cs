namespace CatLib.UI;

public static class Notifications
{
    public static void Show(string text, string brief = null) => PlayerMessages.Post(text, brief);
}
