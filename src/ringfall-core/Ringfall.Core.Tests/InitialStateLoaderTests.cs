using Ringfall.Core.Scenarios;
using Ringfall.Core.State;

namespace Ringfall.Core.Tests;

[TestClass]
public sealed class InitialStateLoaderTests
{
    [TestMethod]
    public void Loads_minimal_world_state_fixture()
    {
        var worldState = InitialStateLoader.LoadFromJson(ReadFixture("aster-minimal-world-state.json"));

        var aster = worldState.Sectors.Single(sector => sector.SectorId == "Aster");
        var asterSystems = aster.Systems.Select(system => system.SystemId).ToArray();
        var actorIds = worldState.Actors.Select(actor => actor.ActorId).ToArray();
        var sectorIds = worldState.Sectors.Select(sector => sector.SectorId).ToArray();

        CollectionAssert.IsSubsetOf(new[] { "R2", "R5", "R6", "R10" }, asterSystems);
        CollectionAssert.IsSubsetOf(new[] { "A1", "A2", "A4", "A6", "A9" }, actorIds);
        CollectionAssert.IsSubsetOf(new[] { "Vireo", "Morrow", "BlackSeam" }, sectorIds);
        Assert.IsTrue(worldState.Crews.Any(crew => crew.CrewId == "crew_aster_repair_02"));
    }

    [TestMethod]
    public void Missing_required_aster_system_fails_deterministically()
    {
        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(ReadFixture("invalid-missing-aster-r2.json")));

        Assert.AreEqual("Aster system R2 is required.", exception.Message);
    }

    [TestMethod]
    public void Duplicate_system_ids_fail_deterministically()
    {
        var json = ReadFixture("aster-minimal-world-state.json")
            .Replace("\"systemId\": \"VIREO-STUB\"", "\"systemId\": \"R2\"");

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("system ids must be globally unique.", exception.Message);
    }

    [TestMethod]
    public void Unknown_crew_assigned_actor_fails_deterministically()
    {
        var json = ReadFixture("aster-minimal-world-state.json")
            .Replace("\"assignedActorId\": \"A1\"", "\"assignedActorId\": \"UNKNOWN_ACTOR\"");

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("crew crew_aster_repair_02 assigned actor must exist.", exception.Message);
    }

    [TestMethod]
    public void Missing_required_actor_fails_deterministically()
    {
        var json = ReadFixture("aster-minimal-world-state.json")
            .Replace("\"actorId\": \"A1\"", "\"actorId\": \"MISSING_A1\"");

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("actor A1 is required.", exception.Message);
    }

    [TestMethod]
    public void Missing_required_crew_fails_deterministically()
    {
        var json = ReadFixture("aster-minimal-world-state.json")
            .Replace("\"crewId\": \"crew_aster_repair_02\"", "\"crewId\": \"missing_crew\"");

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("crew crew_aster_repair_02 is required.", exception.Message);
    }

    [TestMethod]
    public void Missing_required_stub_sector_fails_deterministically()
    {
        var json = ReadFixture("aster-minimal-world-state.json")
            .Replace("\"sectorId\": \"Vireo\"", "\"sectorId\": \"MissingVireo\"");

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("sector Vireo is required.", exception.Message);
    }

    [TestMethod]
    public void Default_simulation_seed_fails_deterministically()
    {
        var json = ReadFixture("aster-minimal-world-state.json")
            .Replace("\"simulationSeed\": 42", "\"simulationSeed\": 0");

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("simulationSeed must be present and greater than zero.", exception.Message);
    }

    [TestMethod]
    public void Missing_simulation_seed_fails_at_loader_boundary()
    {
        const string json = """
        {
          "schemaVersion": "0.1",
          "scenarioId": "invalid-missing-seed",
          "sectors": [],
          "actors": [],
          "crews": []
        }
        """;

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("WorldState JSON is invalid.", exception.Message);
        Assert.IsNotNull(exception.InnerException);
    }

    [TestMethod]
    public void Null_top_level_collection_fails_deterministically()
    {
        const string json = """
        {
          "schemaVersion": "0.1",
          "scenarioId": "invalid-null-sectors",
          "simulationSeed": 42,
          "sectors": null,
          "actors": [],
          "crews": []
        }
        """;

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("sectors is required.", exception.Message);
    }

    [TestMethod]
    public void Null_nested_collection_fails_deterministically()
    {
        const string json = """
        {
          "schemaVersion": "0.1",
          "scenarioId": "invalid-null-systems",
          "simulationSeed": 42,
          "sectors": [
            { "sectorId": "Aster", "displayName": "Aster", "systems": null }
          ],
          "actors": [],
          "crews": []
        }
        """;

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("sector Aster systems are required.", exception.Message);
    }

    [TestMethod]
    public void Malformed_json_fails_at_loader_boundary()
    {
        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson("{ not-json"));

        Assert.AreEqual("WorldState JSON is invalid.", exception.Message);
        Assert.IsNotNull(exception.InnerException);
    }

    [TestMethod]
    public void Numeric_metric_visibility_json_fails_at_loader_boundary()
    {
        var json = ReadFixture("aster-minimal-world-state.json")
            .Replace("\"visibility\": \"observable\"", "\"visibility\": 999");

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("WorldState JSON is invalid.", exception.Message);
        Assert.IsNotNull(exception.InnerException);
    }

    [TestMethod]
    public void Undefined_metric_visibility_fails_deterministically()
    {
        var source = InitialStateLoader.LoadFromJson(ReadFixture("aster-minimal-world-state.json"));
        var metric = source.Sectors[0].Systems[0].Metrics[0];
        var invalid = ReplaceFirstMetric(source, metric with { Visibility = (MetricVisibility)999 });

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.Validate(invalid));

        Assert.AreEqual("system R2 metric gridLoad visibility is invalid.", exception.Message);
    }

    [TestMethod]
    public void Non_finite_metric_value_fails_deterministically()
    {
        var source = InitialStateLoader.LoadFromJson(ReadFixture("aster-minimal-world-state.json"));
        var metric = source.Sectors[0].Systems[0].Metrics[0];
        var invalid = ReplaceFirstMetric(source, metric with { Value = double.NaN });

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.Validate(invalid));

        Assert.AreEqual("system R2 metric gridLoad value must be finite.", exception.Message);
    }

    private static string ReadFixture(string fileName)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
    }

    private static WorldState ReplaceFirstMetric(WorldState source, SystemMetric metric)
    {
        var sectors = source.Sectors.ToArray();
        var systems = sectors[0].Systems.ToArray();
        var metrics = systems[0].Metrics.ToArray();

        metrics[0] = metric;
        systems[0] = systems[0] with { Metrics = metrics };
        sectors[0] = sectors[0] with { Systems = systems };

        return source with { Sectors = sectors };
    }
}
