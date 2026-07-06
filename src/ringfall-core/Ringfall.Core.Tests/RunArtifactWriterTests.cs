using System.Text.Json;
using Ringfall.Core.Artifacts;
using Ringfall.Core.Scenarios;
using Ringfall.Core.Simulation;

namespace Ringfall.Core.Tests;

[TestClass]
public sealed class RunArtifactWriterTests
{
    private static readonly DateTimeOffset FixedCreatedAtUtc = new(2026, 7, 7, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Write_creates_exact_t0_artifact_tree()
    {
        using var directory = TemporaryDirectory.Create();
        var result = T0TickRunner.Run(LoadFixture());

        var bundle = RunArtifactWriter.Write(directory.Path, "run-t000-aster", FixedCreatedAtUtc, result);

        AssertFile(bundle.ManifestPath, directory.Path, ArtifactPaths.Manifest);
        AssertFile(bundle.InitialSnapshotPath, directory.Path, ArtifactPaths.InitialSnapshot);
        AssertFile(bundle.FinalSnapshotPath, directory.Path, ArtifactPaths.FinalSnapshot);
        AssertFile(bundle.EventLogPath, directory.Path, ArtifactPaths.EventLog);
        AssertFile(bundle.StateDiffPath, directory.Path, ArtifactPaths.StateDiff);
        InitialStateLoader.LoadFromJson(File.ReadAllText(bundle.InitialSnapshotPath));
        InitialStateLoader.LoadFromJson(File.ReadAllText(bundle.FinalSnapshotPath));
    }

    [TestMethod]
    public void Write_emits_schema_shaped_manifest_with_safe_artifact_refs_only()
    {
        using var directory = TemporaryDirectory.Create();
        var result = T0TickRunner.Run(LoadFixture());
        var bundle = RunArtifactWriter.Write(directory.Path, "run-t000-aster", FixedCreatedAtUtc, result);

        using var manifest = JsonDocument.Parse(File.ReadAllText(bundle.ManifestPath));
        var root = manifest.RootElement;
        var refs = root.GetProperty("tracks")[0].GetProperty("artifact_refs").EnumerateArray().ToArray();
        var refTypes = refs.Select(item => item.GetProperty("ref_type").GetString()).ToArray();

        Assert.AreEqual("run-t000-aster", root.GetProperty("run_id").GetString());
        Assert.AreEqual("0.1", root.GetProperty("schema_version").GetString());
        Assert.AreEqual("2026-07-07T00:00:00Z", root.GetProperty("created_at_utc").GetString());
        Assert.AreEqual("fixture-aster-state-subset", root.GetProperty("scenario_id").GetString());
        Assert.AreEqual("dev", root.GetProperty("run_mode").GetString());
        Assert.AreEqual(42, root.GetProperty("simulation_seed").GetInt32());
        Assert.AreEqual("0.1", root.GetProperty("contract_version").GetString());
        Assert.AreEqual("completed", root.GetProperty("status").GetString());
        CollectionAssert.AreEqual(new[] { "run_manifest", "state_diff" }, refTypes);
        CollectionAssert.DoesNotContain(refTypes, "snapshot");
        CollectionAssert.DoesNotContain(refTypes, "event_log");
    }

    [TestMethod]
    public void Write_emits_state_diff_with_t0_placeholder_and_required_fields()
    {
        using var directory = TemporaryDirectory.Create();
        var result = T0TickRunner.Run(LoadFixture());
        var bundle = RunArtifactWriter.Write(directory.Path, "run-t000-aster", FixedCreatedAtUtc, result);

        using var stateDiff = JsonDocument.Parse(File.ReadAllText(bundle.StateDiffPath));
        var root = stateDiff.RootElement;
        var changes = root.GetProperty("changes").EnumerateArray().ToArray();

        Assert.AreEqual("StateDiff", root.GetProperty("diff_type").GetString());
        Assert.AreEqual("0.1", root.GetProperty("schema_version").GetString());
        Assert.AreEqual(T0TickRunner.StateDiffId, root.GetProperty("diff_id").GetString());
        Assert.AreEqual(0, root.GetProperty("tick").GetInt32());
        Assert.AreEqual(T0TickRunner.SourceActionTraceId, root.GetProperty("source_action_trace_id").GetString());
        Assert.AreEqual(42, root.GetProperty("deterministic_seed").GetInt32());
        Assert.HasCount(2, changes);
        Assert.AreEqual(T0TickRunner.GridLoadPath, changes[0].GetProperty("path").GetString());
        Assert.AreEqual(0.68, changes[0].GetProperty("old").GetDouble());
        Assert.AreEqual(0.73, changes[0].GetProperty("new").GetDouble());
        Assert.AreEqual("public_observable", changes[0].GetProperty("visibility").GetString());
        Assert.AreEqual(T0TickRunner.ThermalDebtPath, changes[1].GetProperty("path").GetString());
        Assert.AreEqual(0.41, changes[1].GetProperty("old").GetDouble());
        Assert.AreEqual(0.46, changes[1].GetProperty("new").GetDouble());
        Assert.AreEqual("hidden", changes[1].GetProperty("visibility").GetString());
    }

    [TestMethod]
    public void Write_emits_internal_deterministic_event_log_without_hidden_numeric_truth_in_message()
    {
        using var directory = TemporaryDirectory.Create();
        var result = T0TickRunner.Run(LoadFixture());
        var bundle = RunArtifactWriter.Write(directory.Path, "run-t000-aster", FixedCreatedAtUtc, result);

        using var eventLog = JsonDocument.Parse(File.ReadAllText(bundle.EventLogPath));
        var root = eventLog.RootElement;
        var firstEvent = root.GetProperty("events")[0];
        var message = firstEvent.GetProperty("message").GetString() ?? string.Empty;

        Assert.AreEqual("0.1-internal", root.GetProperty("schema_version").GetString());
        Assert.AreEqual("fixture-aster-state-subset", root.GetProperty("scenario_id").GetString());
        Assert.AreEqual(42, root.GetProperty("deterministic_seed").GetInt32());
        Assert.AreEqual(T0TickRunner.EventId, firstEvent.GetProperty("event_id").GetString());
        Assert.AreEqual("AsterHeatAlarm", firstEvent.GetProperty("event_type").GetString());
        Assert.IsFalse(message.Contains("0.41", StringComparison.Ordinal));
        Assert.IsFalse(message.Contains("0.46", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Write_is_byte_identical_for_same_run_id_clock_and_input()
    {
        using var firstDirectory = TemporaryDirectory.Create();
        using var secondDirectory = TemporaryDirectory.Create();
        var first = T0TickRunner.Run(LoadFixture());
        var second = T0TickRunner.Run(LoadFixture());

        RunArtifactWriter.Write(firstDirectory.Path, "run-t000-aster", FixedCreatedAtUtc, first);
        RunArtifactWriter.Write(secondDirectory.Path, "run-t000-aster", FixedCreatedAtUtc, second);

        foreach (var relativePath in ExpectedArtifactPaths())
        {
            CollectionAssert.AreEqual(
                File.ReadAllBytes(Combine(firstDirectory.Path, relativePath)),
                File.ReadAllBytes(Combine(secondDirectory.Path, relativePath)),
                relativePath);
        }
    }

    [TestMethod]
    public void Write_rejects_invalid_arguments()
    {
        using var directory = TemporaryDirectory.Create();
        var result = T0TickRunner.Run(LoadFixture());

        Assert.ThrowsExactly<ArgumentException>(() => RunArtifactWriter.Write(" ", "run-t000-aster", FixedCreatedAtUtc, result));
        Assert.ThrowsExactly<ArgumentException>(() => RunArtifactWriter.Write(directory.Path, " ", FixedCreatedAtUtc, result));
        Assert.ThrowsExactly<ArgumentNullException>(() => RunArtifactWriter.Write(directory.Path, "run-t000-aster", FixedCreatedAtUtc, null!));
    }

    [TestMethod]
    public void Write_reuses_existing_output_directory_for_same_run()
    {
        using var directory = TemporaryDirectory.Create();
        var first = T0TickRunner.Run(LoadFixture());
        var second = T0TickRunner.Run(LoadFixture());

        RunArtifactWriter.Write(directory.Path, "run-t000-aster", FixedCreatedAtUtc, first);
        var bundle = RunArtifactWriter.Write(directory.Path, "run-t000-aster", FixedCreatedAtUtc, second);

        foreach (var path in new[] { bundle.ManifestPath, bundle.InitialSnapshotPath, bundle.FinalSnapshotPath, bundle.EventLogPath, bundle.StateDiffPath })
        {
            Assert.IsTrue(File.Exists(path), path);
        }
    }

    private static Core.State.WorldState LoadFixture()
    {
        return InitialStateLoader.LoadFromJson(ReadFixture("aster-minimal-world-state.json"));
    }

    private static string ReadFixture(string fileName)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
    }

    private static string[] ExpectedArtifactPaths()
    {
        return
        [
            ArtifactPaths.Manifest,
            ArtifactPaths.InitialSnapshot,
            ArtifactPaths.FinalSnapshot,
            ArtifactPaths.EventLog,
            ArtifactPaths.StateDiff
        ];
    }

    private static void AssertFile(string actualPath, string root, string expectedRelativePath)
    {
        Assert.AreEqual(Combine(root, expectedRelativePath), actualPath);
        Assert.IsTrue(File.Exists(actualPath), actualPath);
    }

    private static string Combine(string root, string relativePath)
    {
        return Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ringfall-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
