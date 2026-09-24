using System;
using System.Collections.Generic;
using System.Diagnostics;
using CatLib.Core;
using CatLib.Logging;

namespace CatLib.Tests.Framework;

public sealed class TestRunner
{
    private const int MaxAdvancesPerFrame = 256;

    private readonly IReadOnlyList<TestCase> _tests;
    private readonly CatLogger _log;
    private RunState _run;

    public TestRunner(IReadOnlyList<TestCase> tests, CatLogger log)
    {
        _tests = tests;
        _log = log;
    }

    public event Action<TestRunSummary> Completed;

    public bool IsRunning => _run != null;

    public int TestCount => _tests.Count;

    public bool Start(string trigger)
    {
        if (IsRunning)
        {
            _log.Warning($"Test run requested by {trigger} ignored: a run is already in progress");
            return false;
        }

        _run = new RunState(trigger);
        _log.Message($"Test run started by {trigger}: {_tests.Count} tests");
        return true;
    }

    public void Update()
    {
        var advances = 0;
        while (_run != null && advances++ < MaxAdvancesPerFrame)
        {
            if (_run.Active == null && !BeginNextTest())
            {
                FinishRun();
                return;
            }

            if (!Advance(_run.Active))
            {
                return;
            }
        }
    }

    private bool BeginNextTest()
    {
        if (_run.NextIndex >= _tests.Count)
        {
            return false;
        }

        var test = _tests[_run.NextIndex++];
        var context = new TestContext(_log.Scope(test.FullName), _run.Trigger);
        IEnumerator<TestStep> steps;
        try
        {
            steps = test.Run(context).GetEnumerator();
        }
        catch (Exception exception)
        {
            _run.Active = new ActiveTest(test, context, null);
            Complete(TestStatus.Errored, exception.ToString());
            return true;
        }

        _run.Active = new ActiveTest(test, context, steps);
        return true;
    }

    private bool Advance(ActiveTest active)
    {
        if (active.Stopwatch.Elapsed > active.Test.Timeout)
        {
            Complete(TestStatus.TimedOut, $"Exceeded timeout of {active.Test.Timeout.TotalSeconds:0.##} s");
            return true;
        }

        try
        {
            if (active.Step != null && !active.Step.IsDone())
            {
                return false;
            }

            if (!active.Steps.MoveNext())
            {
                Complete(TestStatus.Passed, null);
                return true;
            }

            active.Step = active.Steps.Current ?? Wait.NextFrame();
            active.Step.Begin();
            return true;
        }
        catch (AssertionException exception)
        {
            Complete(TestStatus.Failed, exception.Message);
            return true;
        }
        catch (Exception exception)
        {
            Complete(TestStatus.Errored, exception.ToString());
            return true;
        }
    }

    private void Complete(TestStatus status, string message)
    {
        var active = _run.Active;
        _run.Active = null;

        try
        {
            active.Steps?.Dispose();
        }
        catch (Exception exception)
        {
            _log.Warning($"Disposing {active.Test.FullName} failed: {exception.Message}");
        }

        var result = new TestResult(
            active.Test.Suite,
            active.Test.Name,
            status,
            active.Stopwatch.Elapsed,
            FrameLoop.FrameCount - active.StartFrame,
            message,
            active.Context.Notes);
        _run.Results.Add(result);
        LogResult(result);
    }

    private void LogResult(TestResult result)
    {
        var line = $"{result.Status.ToString().ToUpperInvariant(),-8} {result.FullName} ({result.Duration.TotalMilliseconds:0} ms, {result.Frames} frames)";
        if (result.Status == TestStatus.Passed)
        {
            _log.Info(line);
        }
        else
        {
            _log.Error(line + ": " + result.Message);
        }

        foreach (var note in result.Notes)
        {
            _log.Info("    " + note);
        }
    }

    private void FinishRun()
    {
        var run = _run;
        _run = null;

        var summary = new TestRunSummary(run.Trigger, run.StartedAt, run.Stopwatch.Elapsed, run.Results);
        var line = $"Test run finished: {summary.Count(TestStatus.Passed)} passed, {summary.Count(TestStatus.Failed)} failed, " +
                   $"{summary.Count(TestStatus.Errored)} errored, {summary.Count(TestStatus.TimedOut)} timed out " +
                   $"in {summary.Duration.TotalSeconds:0.00} s";
        if (summary.IsSuccessful)
        {
            _log.Message(line);
        }
        else
        {
            _log.Error(line);
        }

        var handlers = Completed;
        if (handlers == null)
        {
            return;
        }

        try
        {
            handlers(summary);
        }
        catch (Exception exception)
        {
            _log.Error("Test run completion handler failed", exception);
        }
    }

    private sealed class RunState
    {
        public RunState(string trigger)
        {
            Trigger = trigger;
            StartedAt = DateTime.Now;
            Stopwatch = Stopwatch.StartNew();
        }

        public string Trigger { get; }

        public DateTime StartedAt { get; }

        public Stopwatch Stopwatch { get; }

        public int NextIndex { get; set; }

        public ActiveTest Active { get; set; }

        public List<TestResult> Results { get; } = new();
    }

    private sealed class ActiveTest
    {
        public ActiveTest(TestCase test, TestContext context, IEnumerator<TestStep> steps)
        {
            Test = test;
            Context = context;
            Steps = steps;
            StartFrame = FrameLoop.FrameCount;
            Stopwatch = Stopwatch.StartNew();
        }

        public TestCase Test { get; }

        public TestContext Context { get; }

        public IEnumerator<TestStep> Steps { get; }

        public TestStep Step { get; set; }

        public long StartFrame { get; }

        public Stopwatch Stopwatch { get; }
    }
}
