using System.Numerics;

namespace StackIt.Logic;

public readonly record struct HeldState(Vector3 Then, Vector3 Now, bool Present);
