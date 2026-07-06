using System.Globalization;
using Ringfall.Core.Simulation;
using Ringfall.Core.Snapshots;

namespace Ringfall.Core.Artifacts;

public static class RunArtifactWriter
{
    public static RunArtifactBundle Write(string outputDirectory, string runId, DateTimeOffset createdAtUtc, T0TickResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(result);

        Directory.CreateDirectory(Path.Combine(outputDirectory, "snapshots"));
        Directory.CreateDirectory(Path.Combine(outputDirectory, "events"));
        Directory.CreateDirectory(Path.Combine(outputDirectory, "state-diffs"));

        var bundle = new RunArtifactBundle
        {
            ManifestPath = Path.Combine(outputDirectory, ArtifactPaths.Manifest),
            InitialSnapshotPath = Path.Combine(outputDirectory, NormalizeSeparators(ArtifactPaths.InitialSnapshot)),
            FinalSnapshotPath = Path.Combine(outputDirectory, NormalizeSeparators(ArtifactPaths.FinalSnapshot)),
            EventLogPath = Path.Combine(outputDirectory, NormalizeSeparators(ArtifactPaths.EventLog)),
            StateDiffPath = Path.Combine(outputDirectory, NormalizeSeparators(ArtifactPaths.StateDiff))
        };

        File.WriteAllText(bundle.InitialSnapshotPath, WorldStateJsonSerializer.Serialize(result.InitialState));
        File.WriteAllText(bundle.FinalSnapshotPath, WorldStateJsonSerializer.Serialize(result.FinalState));
        File.WriteAllText(bundle.EventLogPath, ArtifactJsonSerializer.Serialize(CreateEventLog(result)));
        File.WriteAllText(bundle.StateDiffPath, ArtifactJsonSerializer.Serialize(result.StateDiff));
        File.WriteAllText(bundle.ManifestPath, ArtifactJsonSerializer.Serialize(CreateManifest(runId, createdAtUtc, result)));

        return bundle;
    }

    private static EventLogArtifact CreateEventLog(T0TickResult result)
    {
        return new EventLogArtifact
        {
            SchemaVersion = "0.1-internal",
            ScenarioId = result.FinalState.ScenarioId,
            DeterministicSeed = result.DeterministicSeed,
            Events = result.Events
        };
    }

    private static RunManifestArtifact CreateManifest(string runId, DateTimeOffset createdAtUtc, T0TickResult result)
    {
        return new RunManifestArtifact
        {
            RunId = runId,
            SchemaVersion = "0.1",
            CreatedAtUtc = createdAtUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ScenarioId = result.FinalState.ScenarioId,
            RunMode = "dev",
            SimulationSeed = result.DeterministicSeed,
            ContractVersion = "0.1",
            Tracks =
            [
                new RunManifestTrack
                {
                    TrackId = "track-b-core",
                    Role = "deterministic_core_artifacts",
                    ArtifactRefs =
                    [
                        new SourceRef
                        {
                            RefId = "artifact-run-manifest-t000",
                            RefType = "run_manifest",
                            ArtifactUri = ArtifactPaths.Manifest
                        },
                        new SourceRef
                        {
                            RefId = T0TickRunner.StateDiffId,
                            RefType = "state_diff",
                            ArtifactUri = ArtifactPaths.StateDiff
                        }
                    ]
                }
            ],
            Status = "completed"
        };
    }

    private static string NormalizeSeparators(string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar);
    }
}
