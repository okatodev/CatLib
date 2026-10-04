using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CatLib.Logging;
using HarmonyLib;

namespace CatLib.Patching;

public sealed class CatPatches
{
    public const int LoggedErrorsPerHandler = 3;
    public const int ErrorsBeforeTurningOff = 50;

    private readonly CatLogger _log;
    private readonly List<Planned> _planned = new();
    private readonly List<string> _problems = new();
    private readonly Dictionary<string, int> _errors = new(StringComparer.Ordinal);
    private Harmony _harmony;
    private int _totalErrors;

    public CatPatches(string ownerId, CatLogger log)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("A patch group needs the id of its mod", nameof(ownerId));
        }

        OwnerId = ownerId;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public event Action TurnedOff;

    public string OwnerId { get; }

    public bool IsActive { get; private set; }

    public bool IsApplied => _harmony != null;

    public int Count => _planned.Count;

    public int TotalErrors => _totalErrors;

    public IReadOnlyList<string> Problems => _problems;

    public CatPatches Prefix(Type type, string method, Type[] parameters, Type handlerType, string handler) =>
        Add(PatchKind.Prefix, type, method, parameters, handlerType, handler);

    public CatPatches Postfix(Type type, string method, Type[] parameters, Type handlerType, string handler) =>
        Add(PatchKind.Postfix, type, method, parameters, handlerType, handler);

    public CatPatches Add(PatchKind kind, Type type, string method, Type[] parameters, Type handlerType, string handler)
    {
        if (IsApplied)
        {
            throw new InvalidOperationException("Patches cannot be added after Apply");
        }

        _planned.Add(new Planned(kind, type, method, parameters, handlerType, handler));
        return this;
    }

    public bool Apply()
    {
        if (IsApplied)
        {
            return IsActive;
        }

        _problems.Clear();
        _errors.Clear();
        _totalErrors = 0;
        foreach (var planned in _planned)
        {
            try
            {
                planned.Resolve(_problems);
            }
            catch (Exception exception)
            {
                _problems.Add($"{planned.Describe()}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        if (_planned.Count == 0)
        {
            _problems.Add("no patches were added");
        }

        if (_problems.Count > 0)
        {
            foreach (var problem in _problems)
            {
                _log.Error($"[Patches] {OwnerId}: {problem}");
            }

            _log.Error($"[Patches] {OwnerId}: nothing was patched, the game version may differ from the one the mod was made for");
            return false;
        }

        _harmony = new Harmony(OwnerId);
        var done = 0;
        try
        {
            foreach (var planned in _planned)
            {
                var handler = new HarmonyMethod(planned.HandlerMethod);
                _harmony.Patch(planned.TargetMethod,
                    prefix: planned.Kind == PatchKind.Prefix ? handler : null,
                    postfix: planned.Kind == PatchKind.Postfix ? handler : null);
                done++;
            }
        }
        catch (Exception exception)
        {
            _problems.Add($"patching {_planned[done].Describe()} failed: {exception.Message}");
            _log.Error($"[Patches] {OwnerId}: patching {_planned[done].Describe()} failed, all patches of the mod are removed again", exception);
            Unpatch();
            return false;
        }

        IsActive = true;
        _log.Info($"[Patches] {OwnerId}: {done} patch(es) installed: {string.Join(", ", _planned.Select(planned => planned.Describe()))}");
        return true;
    }

    public void Remove()
    {
        if (!IsApplied)
        {
            return;
        }

        Unpatch();
        _log.Info($"[Patches] {OwnerId}: patches removed");
    }

    public void Run(string handler, Action action)
    {
        if (!IsActive || action == null)
        {
            return;
        }

        try
        {
            action();
        }
        catch (Exception exception)
        {
            Fault(handler, exception);
        }
    }

    public T Run<T>(string handler, Func<T> action, T fallback)
    {
        if (!IsActive || action == null)
        {
            return fallback;
        }

        try
        {
            return action();
        }
        catch (Exception exception)
        {
            Fault(handler, exception);
            return fallback;
        }
    }

    public void Fault(string handler, Exception exception)
    {
        var key = string.IsNullOrEmpty(handler) ? "handler" : handler;
        _errors[key] = _errors.TryGetValue(key, out var count) ? count + 1 : 1;
        _totalErrors++;
        if (_errors[key] <= LoggedErrorsPerHandler)
        {
            _log.Error($"[Patches] {OwnerId}: {key} failed, the game goes on as without the mod for this call" +
                       (_errors[key] == LoggedErrorsPerHandler ? "; further errors of it are counted, not written" : string.Empty), exception);
        }

        if (IsActive && _totalErrors >= ErrorsBeforeTurningOff)
        {
            IsActive = false;
            _log.Error(string.Format(CultureInfo.InvariantCulture,
                "[Patches] {0}: turned off after {1} errors ({2}); the game works as without the mod until it restarts",
                OwnerId, _totalErrors, string.Join(", ", _errors.Select(pair => pair.Key + " " + pair.Value))));
            try
            {
                TurnedOff?.Invoke();
            }
            catch (Exception callback)
            {
                _log.Error($"[Patches] {OwnerId}: a handler of TurnedOff failed", callback);
            }
        }
    }

    private void Unpatch()
    {
        try
        {
            _harmony?.UnpatchSelf();
        }
        catch (Exception exception)
        {
            _log.Error($"[Patches] {OwnerId}: removing the patches failed", exception);
        }

        _harmony = null;
        IsActive = false;
    }

    private sealed class Planned
    {
        public Planned(PatchKind kind, Type type, string method, Type[] parameters, Type handlerType, string handler)
        {
            Kind = kind;
            Type = type;
            Method = method;
            Parameters = parameters;
            HandlerType = handlerType;
            Handler = handler;
        }

        public PatchKind Kind { get; }

        public Type Type { get; }

        public string Method { get; }

        public Type[] Parameters { get; }

        public Type HandlerType { get; }

        public string Handler { get; }

        public MethodInfo TargetMethod { get; private set; }

        public MethodInfo HandlerMethod { get; private set; }

        public string Describe() =>
            $"{Kind.ToString().ToLowerInvariant()} of {Type?.Name ?? "?"}.{Method}({(Parameters == null ? "..." : string.Join(", ", Parameters.Select(parameter => parameter.Name)))})";

        public void Resolve(List<string> problems)
        {
            if (Type == null || string.IsNullOrEmpty(Method))
            {
                problems.Add("a patch has no type or method");
                return;
            }

            TargetMethod = Find(Type, Method, Parameters);
            if (TargetMethod == null)
            {
                problems.Add($"{Describe()}: the game has no such method");
            }

            HandlerMethod = HandlerType == null || string.IsNullOrEmpty(Handler) ? null : AccessTools.Method(HandlerType, Handler);
            if (HandlerMethod == null)
            {
                problems.Add($"{Describe()}: handler {HandlerType?.Name ?? "?"}.{Handler} was not found");
            }
            else if (!HandlerMethod.IsStatic)
            {
                problems.Add($"{Describe()}: handler {HandlerType.Name}.{Handler} must be static");
            }
            else if (Kind == PatchKind.Prefix && HandlerMethod.ReturnType != typeof(void) && HandlerMethod.ReturnType != typeof(bool))
            {
                problems.Add($"{Describe()}: a prefix returns nothing or bool");
            }
        }

        private static MethodInfo Find(Type type, string method, Type[] parameters)
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var candidates = type.GetMethods(all).Where(candidate => candidate.Name == method).ToList();
            if (parameters == null)
            {
                return candidates.Count == 1 ? candidates[0] : null;
            }

            return candidates.FirstOrDefault(candidate => candidate.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameters));
        }
    }
}
