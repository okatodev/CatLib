namespace CatLib.Game.Events;

public sealed record GameEventRecord(string Name, string Arguments, long Frame, double Realtime);
