namespace Ringfall.Core.Artifacts;

public sealed record RunArtifactBundle
{
    public required string ManifestPath { get; init; }
    public required string InitialSnapshotPath { get; init; }
    public required string FinalSnapshotPath { get; init; }
    public required string EventLogPath { get; init; }
    public required string StateDiffPath { get; init; }
}
