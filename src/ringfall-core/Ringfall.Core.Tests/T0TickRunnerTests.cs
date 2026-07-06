using Ringfall.Core.Scenarios;
using Ringfall.Core.Simulation;
using Ringfall.Core.Snapshots;
using Ringfall.Core.State;

namespace Ringfall.Core.Tests;

[TestClass]
public sealed class T0TickRunnerTests
{
    [TestMethod]
    public void Run_emits_deterministic_aster_heat_alarm_event_and_state_diff()
    {
        var initialState = LoadFixture();

        var result = T0TickRunner.Run(initialState);

        var finalR2 = GetSystem(result.FinalState, "R2");
        var gridLoad = finalR2.Metrics.Single(metric => metric.Name == "gridLoad");
        var thermalDebt = finalR2.Metrics.Single(metric => metric.Name == "thermalDebt");

        Assert.AreSame(initialState, result.InitialState);
        Assert.AreEqual(0, result.Tick);
        Assert.AreEqual(42, result.DeterministicSeed);
        Assert.AreEqual(0.73, gridLoad.Value);
        Assert.AreEqual(0.46, thermalDebt.Value);
        Assert.HasCount(1, result.Events);
        Assert.AreEqual(T0TickRunner.EventId, result.Events[0].EventId);
        Assert.AreEqual("AsterHeatAlarm", result.Events[0].EventType);
        Assert.AreEqual("Aster", result.Events[0].SectorId);
        Assert.AreEqual("R2", result.Events[0].SystemId);
        Assert.IsFalse(result.Events[0].Message.Contains("0.41", StringComparison.Ordinal));
        Assert.IsFalse(result.Events[0].Message.Contains("0.46", StringComparison.Ordinal));
        Assert.AreEqual(T0TickRunner.StateDiffId, result.StateDiff.DiffId);
        Assert.AreEqual(T0TickRunner.SourceActionTraceId, result.StateDiff.SourceActionTraceId);
        Assert.HasCount(2, result.StateDiff.Changes);
        AssertChange(result.StateDiff.Changes[0], T0TickRunner.GridLoadPath, 0.68, 0.73, "public_observable");
        AssertChange(result.StateDiff.Changes[1], T0TickRunner.ThermalDebtPath, 0.41, 0.46, "hidden");
    }

    [TestMethod]
    public void Run_does_not_mutate_input_state()
    {
        var initialState = LoadFixture();
        var before = WorldStateJsonSerializer.Serialize(initialState);

        _ = T0TickRunner.Run(initialState);

        Assert.AreEqual(before, WorldStateJsonSerializer.Serialize(initialState));
    }

    [TestMethod]
    public void Run_is_deterministic_for_same_input()
    {
        var first = T0TickRunner.Run(LoadFixture());
        var second = T0TickRunner.Run(LoadFixture());

        Assert.AreEqual(
            WorldStateJsonSerializer.Serialize(first.FinalState),
            WorldStateJsonSerializer.Serialize(second.FinalState));
        Assert.AreEqual(first.Events[0], second.Events[0]);
        CollectionAssert.AreEqual(first.StateDiff.Changes.ToArray(), second.StateDiff.Changes.ToArray());
    }

    [TestMethod]
    public void Run_rejects_seed_values_below_threshold()
    {
        var initialState = WithR2MetricValues(gridLoad: 0.64, thermalDebt: 0.40);

        Assert.ThrowsExactly<InvalidOperationException>(() => T0TickRunner.Run(initialState));
    }

    [TestMethod]
    public void Run_accepts_seed_values_at_threshold()
    {
        var result = T0TickRunner.Run(WithR2MetricValues(gridLoad: 0.65, thermalDebt: 0.40));

        Assert.HasCount(1, result.Events);
        AssertChange(result.StateDiff.Changes[0], T0TickRunner.GridLoadPath, 0.65, 0.73, "public_observable");
        AssertChange(result.StateDiff.Changes[1], T0TickRunner.ThermalDebtPath, 0.40, 0.46, "hidden");
    }

    [TestMethod]
    public void Run_accepts_above_threshold_noncanonical_seed_values()
    {
        var result = T0TickRunner.Run(WithR2MetricValues(gridLoad: 0.69, thermalDebt: 0.42));

        Assert.HasCount(1, result.Events);
        AssertChange(result.StateDiff.Changes[0], T0TickRunner.GridLoadPath, 0.69, 0.73, "public_observable");
        AssertChange(result.StateDiff.Changes[1], T0TickRunner.ThermalDebtPath, 0.42, 0.46, "hidden");
    }

    [TestMethod]
    public void Visibility_mapping_matches_state_diff_contract_values()
    {
        Assert.AreEqual("hidden", T0TickRunner.ToStateDiffVisibility(MetricVisibility.Hidden));
        Assert.AreEqual("public_observable", T0TickRunner.ToStateDiffVisibility(MetricVisibility.Observable));
        Assert.AreEqual("dev_only", T0TickRunner.ToStateDiffVisibility(MetricVisibility.DevOnly));
    }

    private static WorldState LoadFixture()
    {
        return InitialStateLoader.LoadFromJson(ReadFixture("aster-minimal-world-state.json"));
    }

    private static SystemState GetSystem(WorldState state, string systemId)
    {
        return state.Sectors.SelectMany(sector => sector.Systems).Single(system => system.SystemId == systemId);
    }

    private static WorldState WithR2MetricValues(double gridLoad, double thermalDebt)
    {
        var source = LoadFixture();
        var sectors = source.Sectors.ToArray();
        var asterIndex = Array.FindIndex(sectors, sector => sector.SectorId == "Aster");
        var systems = sectors[asterIndex].Systems.ToArray();
        var r2Index = Array.FindIndex(systems, system => system.SystemId == "R2");
        var metrics = systems[r2Index].Metrics.ToArray();

        metrics[Array.FindIndex(metrics, metric => metric.Name == "gridLoad")] =
            metrics.Single(metric => metric.Name == "gridLoad") with { Value = gridLoad };
        metrics[Array.FindIndex(metrics, metric => metric.Name == "thermalDebt")] =
            metrics.Single(metric => metric.Name == "thermalDebt") with { Value = thermalDebt };
        systems[r2Index] = systems[r2Index] with { Metrics = metrics };
        sectors[asterIndex] = sectors[asterIndex] with { Systems = systems };

        return source with { Sectors = sectors };
    }

    private static string ReadFixture(string fileName)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
    }

    private static void AssertChange(
        Core.Artifacts.StateDiffChange change,
        string path,
        double oldValue,
        double newValue,
        string visibility)
    {
        Assert.AreEqual(path, change.Path);
        Assert.AreEqual(oldValue, change.Old);
        Assert.AreEqual(newValue, change.New);
        Assert.AreEqual(visibility, change.Visibility);
    }
}
