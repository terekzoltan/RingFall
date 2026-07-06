using System.Text.Json;
using Ringfall.Core.Snapshots;
using Ringfall.Core.State;

namespace Ringfall.Core.Scenarios;

public static class InitialStateLoader
{
    private static readonly string[] RequiredAsterSystems = ["R2", "R5", "R6", "R10"];
    private static readonly string[] RequiredActorIds = ["A1", "A2", "A4", "A6", "A9"];
    private static readonly string[] RequiredCrewIds = ["crew_aster_repair_02"];
    private static readonly string[] RequiredStubSectors = ["Vireo", "Morrow", "BlackSeam"];

    public static WorldState LoadFromJson(string json)
    {
        try
        {
            var worldState = WorldStateJsonSerializer.Deserialize(json);
            Validate(worldState);
            return worldState;
        }
        catch (InitialStateValidationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            throw new InitialStateValidationException("WorldState JSON is invalid.", exception);
        }
    }

    public static void Validate(WorldState? worldState)
    {
        if (worldState is null)
        {
            throw new InitialStateValidationException("WorldState is required.");
        }

        RequireNonEmpty(worldState.SchemaVersion, "schemaVersion is required.");
        RequireNonEmpty(worldState.ScenarioId, "scenarioId is required.");
        if (worldState.SimulationSeed <= 0)
        {
            throw new InitialStateValidationException("simulationSeed must be present and greater than zero.");
        }

        RequireCollection(worldState.Sectors, "sectors is required.");
        RequireCollection(worldState.Actors, "actors is required.");
        RequireCollection(worldState.Crews, "crews is required.");

        if (worldState.Sectors.Count == 0)
        {
            throw new InitialStateValidationException("at least one sector is required.");
        }

        foreach (var sector in worldState.Sectors)
        {
            RequireElement(sector, "sector entry is required.");
            RequireCollection(sector.Systems, $"sector {sector.SectorId} systems are required.");
            foreach (var system in sector.Systems)
            {
                RequireElement(system, $"sector {sector.SectorId} system entry is required.");
                RequireCollection(system.Metrics, $"system {system.SystemId} metrics are required.");
                foreach (var metric in system.Metrics)
                {
                    RequireElement(metric, $"system {system.SystemId} metric entry is required.");
                }
            }
        }

        foreach (var actor in worldState.Actors)
        {
            RequireElement(actor, "actor entry is required.");
            RequireCollection(actor.SystemRefs, $"actor {actor.ActorId} systemRefs are required.");
        }

        foreach (var crew in worldState.Crews)
        {
            RequireElement(crew, "crew entry is required.");
            RequireCollection(crew.SystemRefs, $"crew {crew.CrewId} systemRefs are required.");
        }

        foreach (var sector in worldState.Sectors)
        {
            RequireNonEmpty(sector.SectorId, "sector id is required.");
            RequireNonEmpty(sector.DisplayName, $"sector {sector.SectorId} displayName is required.");
            foreach (var system in sector.Systems)
            {
                RequireNonEmpty(system.SystemId, "system id is required.");
                RequireNonEmpty(system.DisplayName, $"system {system.SystemId} displayName is required.");
                RequireNonEmpty(system.Category, $"system {system.SystemId} category is required.");
                foreach (var metric in system.Metrics)
                {
                    RequireNonEmpty(metric.Name, $"system {system.SystemId} metric name is required.");
                    if (!Enum.IsDefined(metric.Visibility))
                    {
                        throw new InitialStateValidationException($"system {system.SystemId} metric {metric.Name} visibility is invalid.");
                    }

                    if (!double.IsFinite(metric.Value))
                    {
                        throw new InitialStateValidationException($"system {system.SystemId} metric {metric.Name} value must be finite.");
                    }
                }
            }
        }

        foreach (var actor in worldState.Actors)
        {
            RequireNonEmpty(actor.ActorId, "actor id is required.");
            RequireNonEmpty(actor.DisplayName, $"actor {actor.ActorId} displayName is required.");
            RequireNonEmpty(actor.Role, $"actor {actor.ActorId} role is required.");
            RequireNonEmpty(actor.Layer, $"actor {actor.ActorId} layer is required.");
            RequireNonEmpty(actor.HomeSectorId, $"actor {actor.ActorId} homeSectorId is required.");
        }

        foreach (var crew in worldState.Crews)
        {
            RequireNonEmpty(crew.CrewId, "crew id is required.");
            RequireNonEmpty(crew.DisplayName, $"crew {crew.CrewId} displayName is required.");
            RequireNonEmpty(crew.HomeSectorId, $"crew {crew.CrewId} homeSectorId is required.");
            RequireNonEmpty(crew.Status, $"crew {crew.CrewId} status is required.");
            RequireNonEmpty(crew.AssignedActorId, $"crew {crew.CrewId} assignedActorId is required.");
        }

        EnsureUnique(worldState.Sectors.Select(sector => sector.SectorId), "sector ids must be unique.");
        EnsureUnique(
            worldState.Sectors.SelectMany(sector => sector.Systems.Select(system => system.SystemId)),
            "system ids must be globally unique.");
        EnsureUnique(worldState.Actors.Select(actor => actor.ActorId), "actor ids must be unique.");
        EnsureUnique(worldState.Crews.Select(crew => crew.CrewId), "crew ids must be unique.");

        var sectorIds = worldState.Sectors.Select(sector => sector.SectorId).ToHashSet(StringComparer.Ordinal);
        var systemIds = worldState.Sectors.SelectMany(sector => sector.Systems.Select(system => system.SystemId)).ToHashSet(StringComparer.Ordinal);
        var actorIds = worldState.Actors.Select(actor => actor.ActorId).ToHashSet(StringComparer.Ordinal);
        var crewIds = worldState.Crews.Select(crew => crew.CrewId).ToHashSet(StringComparer.Ordinal);

        RequireKnownIds(sectorIds, RequiredStubSectors, "sector");
        RequireKnownIds(actorIds, RequiredActorIds, "actor");
        RequireKnownIds(crewIds, RequiredCrewIds, "crew");

        foreach (var actor in worldState.Actors)
        {
            RequireKnownSector(sectorIds, actor.HomeSectorId, $"actor {actor.ActorId} home sector must exist.");
            RequireKnownSystems(systemIds, actor.SystemRefs, $"actor {actor.ActorId}");
        }

        foreach (var crew in worldState.Crews)
        {
            RequireKnownSector(sectorIds, crew.HomeSectorId, $"crew {crew.CrewId} home sector must exist.");
            RequireKnownSystems(systemIds, crew.SystemRefs, $"crew {crew.CrewId}");
            RequireKnownActor(actorIds, crew.AssignedActorId, $"crew {crew.CrewId} assigned actor must exist.");
        }

        var aster = worldState.Sectors.FirstOrDefault(sector => sector.SectorId == "Aster")
            ?? throw new InitialStateValidationException("Aster sector is required.");
        var asterSystemIds = aster.Systems.Select(system => system.SystemId).ToHashSet(StringComparer.Ordinal);

        foreach (var systemId in RequiredAsterSystems)
        {
            if (!asterSystemIds.Contains(systemId))
            {
                throw new InitialStateValidationException($"Aster system {systemId} is required.");
            }
        }
    }

    private static void RequireNonEmpty(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InitialStateValidationException(message);
        }
    }

    private static void RequireCollection<T>(IReadOnlyList<T>? values, string message)
    {
        if (values is null)
        {
            throw new InitialStateValidationException(message);
        }
    }

    private static void RequireElement<T>(T? value, string message)
        where T : class
    {
        if (value is null)
        {
            throw new InitialStateValidationException(message);
        }
    }

    private static void EnsureUnique(IEnumerable<string> values, string message)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            RequireNonEmpty(value, message);
            if (!seen.Add(value))
            {
                throw new InitialStateValidationException(message);
            }
        }
    }

    private static void RequireKnownSector(IReadOnlySet<string> sectorIds, string sectorId, string message)
    {
        if (!sectorIds.Contains(sectorId))
        {
            throw new InitialStateValidationException(message);
        }
    }

    private static void RequireKnownSystems(IReadOnlySet<string> systemIds, IReadOnlyList<string> refs, string owner)
    {
        foreach (var systemRef in refs)
        {
            RequireNonEmpty(systemRef, $"{owner} references an empty system id.");
            if (!systemIds.Contains(systemRef))
            {
                throw new InitialStateValidationException($"{owner} references unknown system {systemRef}.");
            }
        }
    }

    private static void RequireKnownActor(IReadOnlySet<string> actorIds, string actorId, string message)
    {
        if (!actorIds.Contains(actorId))
        {
            throw new InitialStateValidationException(message);
        }
    }

    private static void RequireKnownIds(IReadOnlySet<string> knownIds, IEnumerable<string> requiredIds, string itemName)
    {
        foreach (var requiredId in requiredIds)
        {
            if (!knownIds.Contains(requiredId))
            {
                throw new InitialStateValidationException($"{itemName} {requiredId} is required.");
            }
        }
    }
}
