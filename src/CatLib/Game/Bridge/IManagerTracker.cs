namespace CatLib.Game.Bridge;

internal interface IManagerTracker
{
    string Name { get; }

    bool IsAttached { get; }

    int BoundEventCount { get; }

    int ExpectedEventCount { get; }

    string LastError { get; }

    void Update();
}
