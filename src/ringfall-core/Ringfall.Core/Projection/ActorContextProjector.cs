using Ringfall.Core.State;
using Ringfall.Core.Visibility;

namespace Ringfall.Core.Projection;

internal static class ActorContextProjector
{
    public static ActorContextProjection Project(WorldState state, string actorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        if (state.Actors is null)
        {
            throw new InvalidOperationException("Actor context cannot be projected from an invalid world state.");
        }

        var matches = state.Actors.Where(candidate => candidate is not null
            && string.Equals(candidate.ActorId, actorId, StringComparison.Ordinal)).ToArray();
        if (matches.Length == 0)
        {
            throw new KeyNotFoundException($"Actor {actorId} is not present in the loaded world state.");
        }
        if (matches.Length != 1)
        {
            throw new InvalidOperationException($"Actor {actorId} context cannot be projected from an ambiguous world state.");
        }

        var actor = matches[0];
        if (actor.LocalObservations is null
            || actor.CrewRefs is null
            || actor.ToolRefs is null
            || actor.SystemRefs is null
            || string.IsNullOrWhiteSpace(actor.ActorId)
            || string.IsNullOrWhiteSpace(actor.DisplayName)
            || string.IsNullOrWhiteSpace(actor.Role)
            || string.IsNullOrWhiteSpace(actor.Layer)
            || string.IsNullOrWhiteSpace(actor.HomeSectorId)
            || !HasUniqueNonEmptyValues(actor.CrewRefs)
            || !HasUniqueNonEmptyValues(actor.ToolRefs)
            || !HasKnownCrewReferences(state, actor.CrewRefs))
        {
            throw new InvalidOperationException($"Actor {actorId} context cannot be projected from an invalid world state.");
        }


        foreach (var value in ActorProjectedTextValues(actor))
        {
            var violation = ActorObservationVisibilityPolicy.EvaluateProjectedText(state, value);
            if (violation is not null)
            {
                throw new InvalidOperationException(ProjectedTextViolationMessage(violation.Value.Category));
            }
        }

        var observationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var observation in actor.LocalObservations)
        {
            if (observation is null
                || string.IsNullOrWhiteSpace(observation.ObservationId)
                || string.IsNullOrWhiteSpace(observation.Kind)
                || string.IsNullOrWhiteSpace(observation.Signal)
                || string.IsNullOrWhiteSpace(observation.SourceRef)
                || !observationIds.Add(observation.ObservationId))
            {
                throw new InvalidOperationException($"Actor {actorId} context cannot be projected from an invalid world state.");
            }
            if (ActorObservationVisibilityPolicy.Evaluate(state, actor, observation) is not null)
            {
                throw new InvalidOperationException("Actor context contains a non-observable local observation.");
            }
        }

        return new ActorContextProjection
        {
            ActorId = actor.ActorId,
            DisplayName = actor.DisplayName,
            Role = actor.Role,
            Layer = actor.Layer,
            Observations = actor.LocalObservations.ToArray(),
            CrewRefs = actor.CrewRefs.ToArray(),
            ToolRefs = actor.ToolRefs.ToArray()
        };
    }

    private static bool HasUniqueNonEmptyValues(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
            {
                return false;
            }
        }
        return true;
    }

    private static string ProjectedTextViolationMessage(ActorObservationViolationCategory category)
    {
        return category switch
        {
            ActorObservationViolationCategory.ProtectedMetricName
                or ActorObservationViolationCategory.ProtectedMetricValue
                => "Actor context contains protected world-state content.",
            ActorObservationViolationCategory.NonAsciiProjectedContent
                => "Actor context contains unsupported non-ASCII content.",
            _ => "Actor context cannot be projected from an invalid world state."
        };
    }

    private static bool HasKnownCrewReferences(WorldState state, IEnumerable<string> crewRefs)
    {
        if (state.Crews is null)
        {
            return false;
        }

        foreach (var crewRef in crewRefs)
        {
            if (state.Crews.Count(crew => crew is not null
                && string.Equals(crew.CrewId, crewRef, StringComparison.Ordinal)) != 1)
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> ActorProjectedTextValues(ActorState actor)
    {
        yield return actor.ActorId;
        yield return actor.DisplayName;
        yield return actor.Role;
        yield return actor.Layer;
        foreach (var crewRef in actor.CrewRefs)
        {
            yield return crewRef;
        }
        foreach (var toolRef in actor.ToolRefs)
        {
            yield return toolRef;
        }
    }
}
