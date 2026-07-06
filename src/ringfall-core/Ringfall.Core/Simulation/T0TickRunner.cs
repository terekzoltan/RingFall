using Ringfall.Core.Artifacts;
using Ringfall.Core.Scenarios;
using Ringfall.Core.State;

namespace Ringfall.Core.Simulation;

public static class T0TickRunner
{
    public const int Tick = 0;
    // System T0 ticks have no action trace; this placeholder must not be treated as an action-trace claim.
    public const string SourceActionTraceId = "system-t0-tick";
    public const string EventId = "event-t000-aster-r2-heat-alarm";
    public const string StateDiffId = "state-diff-t000-aster-r2-heat-alarm";
    public const string GridLoadPath = "/sectors/Aster/systems/R2/metrics/gridLoad/value";
    public const string ThermalDebtPath = "/sectors/Aster/systems/R2/metrics/thermalDebt/value";

    private const double GridLoadTrigger = 0.65;
    private const double ThermalDebtTrigger = 0.40;
    private const double GridLoadTarget = 0.73;
    private const double ThermalDebtTarget = 0.46;

    public static T0TickResult Run(WorldState initialState)
    {
        InitialStateLoader.Validate(initialState);

        var aster = initialState.Sectors.Single(sector => sector.SectorId == "Aster");
        var r2 = aster.Systems.Single(system => system.SystemId == "R2");
        var gridLoad = r2.Metrics.Single(metric => metric.Name == "gridLoad");
        var thermalDebt = r2.Metrics.Single(metric => metric.Name == "thermalDebt");

        if (gridLoad.Value < GridLoadTrigger || thermalDebt.Value < ThermalDebtTrigger)
        {
            throw new InvalidOperationException("Aster R2 T0 heat alarm seed did not meet the deterministic trigger condition.");
        }

        var finalState = ReplaceR2Metrics(initialState, gridLoad with { Value = GridLoadTarget }, thermalDebt with { Value = ThermalDebtTarget });

        return new T0TickResult
        {
            InitialState = initialState,
            FinalState = finalState,
            Tick = Tick,
            DeterministicSeed = initialState.SimulationSeed,
            Events =
            [
                new SimulationEventArtifact
                {
                    EventType = "AsterHeatAlarm",
                    EventId = EventId,
                    Tick = Tick,
                    SectorId = "Aster",
                    SystemId = "R2",
                    Severity = "alarm",
                    Message = "Aster R2 heat alarm triggered during T0 system tick.",
                    DeterministicSeed = initialState.SimulationSeed
                }
            ],
            StateDiff = new StateDiffArtifact
            {
                DiffType = "StateDiff",
                SchemaVersion = "0.1",
                DiffId = StateDiffId,
                Tick = Tick,
                SourceActionTraceId = SourceActionTraceId,
                Changes =
                [
                    new StateDiffChange
                    {
                        Path = GridLoadPath,
                        Old = gridLoad.Value,
                        New = GridLoadTarget,
                        Visibility = ToStateDiffVisibility(gridLoad.Visibility)
                    },
                    new StateDiffChange
                    {
                        Path = ThermalDebtPath,
                        Old = thermalDebt.Value,
                        New = ThermalDebtTarget,
                        Visibility = ToStateDiffVisibility(thermalDebt.Visibility)
                    }
                ],
                DeterministicSeed = initialState.SimulationSeed
            }
        };
    }

    public static string ToStateDiffVisibility(MetricVisibility visibility)
    {
        return visibility switch
        {
            MetricVisibility.Hidden => "hidden",
            MetricVisibility.Observable => "public_observable",
            MetricVisibility.DevOnly => "dev_only",
            _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Unknown metric visibility.")
        };
    }

    private static WorldState ReplaceR2Metrics(WorldState source, SystemMetric gridLoad, SystemMetric thermalDebt)
    {
        var sectors = source.Sectors.ToArray();
        var asterIndex = Array.FindIndex(sectors, sector => sector.SectorId == "Aster");
        var systems = sectors[asterIndex].Systems.ToArray();
        var r2Index = Array.FindIndex(systems, system => system.SystemId == "R2");
        var metrics = systems[r2Index].Metrics.ToArray();

        metrics[Array.FindIndex(metrics, metric => metric.Name == "gridLoad")] = gridLoad;
        metrics[Array.FindIndex(metrics, metric => metric.Name == "thermalDebt")] = thermalDebt;
        systems[r2Index] = systems[r2Index] with { Metrics = metrics };
        sectors[asterIndex] = sectors[asterIndex] with { Systems = systems };

        return source with { Sectors = sectors };
    }
}
