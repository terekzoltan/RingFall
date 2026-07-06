using Ringfall.Core.Scenarios;
using Ringfall.Core.Snapshots;
using Ringfall.Core.State;

namespace Ringfall.Core.Tests;

[TestClass]
public sealed class WorldStateJsonSerializerTests
{
    [TestMethod]
    public void Round_trip_preserves_minimal_world_state()
    {
        var source = InitialStateLoader.LoadFromJson(ReadFixture("aster-minimal-world-state.json"));
        var json = WorldStateJsonSerializer.Serialize(source);
        var roundTripped = InitialStateLoader.LoadFromJson(json);

        Assert.AreEqual(source.SchemaVersion, roundTripped.SchemaVersion);
        Assert.AreEqual(source.ScenarioId, roundTripped.ScenarioId);
        Assert.AreEqual(source.SimulationSeed, roundTripped.SimulationSeed);
        AssertSectorsEqual(source.Sectors, roundTripped.Sectors);
        AssertActorsEqual(source.Actors, roundTripped.Actors);
        AssertCrewsEqual(source.Crews, roundTripped.Crews);
    }

    [TestMethod]
    public void Serialize_is_deterministic_for_same_state()
    {
        var source = InitialStateLoader.LoadFromJson(ReadFixture("aster-minimal-world-state.json"));

        var first = WorldStateJsonSerializer.Serialize(source);
        var second = WorldStateJsonSerializer.Serialize(source);

        Assert.AreEqual(first, second);
    }

    private static string ReadFixture(string fileName)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
    }

    private static void AssertSectorsEqual(IReadOnlyList<SectorState> expected, IReadOnlyList<SectorState> actual)
    {
        Assert.HasCount(expected.Count, actual);
        for (var sectorIndex = 0; sectorIndex < expected.Count; sectorIndex++)
        {
            Assert.AreEqual(expected[sectorIndex].SectorId, actual[sectorIndex].SectorId);
            Assert.AreEqual(expected[sectorIndex].DisplayName, actual[sectorIndex].DisplayName);
            Assert.HasCount(expected[sectorIndex].Systems.Count, actual[sectorIndex].Systems);

            for (var systemIndex = 0; systemIndex < expected[sectorIndex].Systems.Count; systemIndex++)
            {
                var expectedSystem = expected[sectorIndex].Systems[systemIndex];
                var actualSystem = actual[sectorIndex].Systems[systemIndex];
                Assert.AreEqual(expectedSystem.SystemId, actualSystem.SystemId);
                Assert.AreEqual(expectedSystem.DisplayName, actualSystem.DisplayName);
                Assert.AreEqual(expectedSystem.Category, actualSystem.Category);
                Assert.HasCount(expectedSystem.Metrics.Count, actualSystem.Metrics);

                for (var metricIndex = 0; metricIndex < expectedSystem.Metrics.Count; metricIndex++)
                {
                    Assert.AreEqual(expectedSystem.Metrics[metricIndex].Name, actualSystem.Metrics[metricIndex].Name);
                    Assert.AreEqual(expectedSystem.Metrics[metricIndex].Value, actualSystem.Metrics[metricIndex].Value);
                    Assert.AreEqual(expectedSystem.Metrics[metricIndex].Visibility, actualSystem.Metrics[metricIndex].Visibility);
                }
            }
        }
    }

    private static void AssertActorsEqual(IReadOnlyList<ActorState> expected, IReadOnlyList<ActorState> actual)
    {
        Assert.HasCount(expected.Count, actual);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.AreEqual(expected[index].ActorId, actual[index].ActorId);
            Assert.AreEqual(expected[index].DisplayName, actual[index].DisplayName);
            Assert.AreEqual(expected[index].Role, actual[index].Role);
            Assert.AreEqual(expected[index].Layer, actual[index].Layer);
            Assert.AreEqual(expected[index].HomeSectorId, actual[index].HomeSectorId);
            CollectionAssert.AreEqual(expected[index].SystemRefs.ToArray(), actual[index].SystemRefs.ToArray());
        }
    }

    private static void AssertCrewsEqual(IReadOnlyList<CrewState> expected, IReadOnlyList<CrewState> actual)
    {
        Assert.HasCount(expected.Count, actual);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.AreEqual(expected[index].CrewId, actual[index].CrewId);
            Assert.AreEqual(expected[index].DisplayName, actual[index].DisplayName);
            Assert.AreEqual(expected[index].HomeSectorId, actual[index].HomeSectorId);
            Assert.AreEqual(expected[index].Status, actual[index].Status);
            Assert.AreEqual(expected[index].AssignedActorId, actual[index].AssignedActorId);
            CollectionAssert.AreEqual(expected[index].SystemRefs.ToArray(), actual[index].SystemRefs.ToArray());
        }
    }
}
