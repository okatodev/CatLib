using System.Collections.Generic;

namespace StackIt.Logic;

public sealed class MarkGraph
{
    private readonly Dictionary<int, int> _parents = new();
    private readonly Dictionary<int, BehaviorConstraint> _marks = new();
    private readonly Dictionary<int, List<int>> _children = new();
    private readonly Dictionary<int, List<int>> _restingOn = new();

    public int Count => _parents.Count;

    public void Add(int node, int parent, BehaviorConstraint mark)
    {
        _parents[node] = parent;
        _marks[node] = mark;
        if (!_children.TryGetValue(parent, out var list))
        {
            _children[parent] = list = new List<int>();
        }

        list.Add(node);
    }

    public void AddBridge(int bridge, IEnumerable<int> supports)
    {
        foreach (var support in supports)
        {
            if (support == bridge)
            {
                continue;
            }

            if (!_restingOn.TryGetValue(support, out var list))
            {
                _restingOn[support] = list = new List<int>();
            }

            if (!list.Contains(bridge))
            {
                list.Add(bridge);
            }
        }
    }

    public bool BridgeRestsOn(int node) => _restingOn.TryGetValue(node, out var list) && list.Count > 0;

    public bool FragileBroken(int node) => Mark(node) == BehaviorConstraint.Fragile && BridgeRestsOn(node);

    public bool HeavyAbove(int node) => HeavyAbove(node, new HashSet<int>());

    public BehaviorConstraint Mark(int node) => _marks.TryGetValue(node, out var mark) ? mark : BehaviorConstraint.None;

    private bool HeavyAbove(int node, HashSet<int> visited)
    {
        if (!visited.Add(node))
        {
            return false;
        }

        if (_children.TryGetValue(node, out var children))
        {
            foreach (var child in children)
            {
                if (Mark(child) == BehaviorConstraint.Heavy || HeavyAbove(child, visited))
                {
                    return true;
                }
            }
        }

        if (_restingOn.TryGetValue(node, out var bridges))
        {
            foreach (var bridge in bridges)
            {
                if (Mark(bridge) == BehaviorConstraint.Heavy || HeavyAbove(bridge, visited))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
