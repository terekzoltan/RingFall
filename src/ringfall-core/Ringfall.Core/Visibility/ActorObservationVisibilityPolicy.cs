using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ringfall.Core.State;

namespace Ringfall.Core.Visibility;

internal enum ActorObservationViolationCategory
{
    InvalidSourceSyntax,
    UnsupportedSource,
    ActorSourceMismatch,
    UnknownOrAmbiguousSource,
    NonObservableSource,
    ProtectedMetricName,
    ProtectedMetricValue,
    NonAsciiProjectedContent,
    InvalidWorldState
}

internal readonly record struct ActorObservationViolation(ActorObservationViolationCategory Category);

internal static class ActorObservationVisibilityPolicy
{
    private const string AsterHeatAlarmEvent = "event:event-t000-aster-r2-heat-alarm";
    private const string AsterHeatAlarmSensor = "sensor:aster_r2.heat-alarm";

    private static readonly Regex SourceRefPattern = new(
        "\\A(event|system|sensor):[A-Za-z0-9][A-Za-z0-9._-]*\\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly Regex NumericTokenPattern = new(
        @"(?:(?<![+-])(?<value>(?>[-+](?:(?:[0-9](?:_?[0-9])*)(?:\.(?:[0-9](?:_?[0-9])*))?|\.(?:[0-9](?:_?[0-9])*))(?:[eE][-+]?(?:[0-9](?:_?[0-9])*))?))|(?<![A-Za-z0-9+-])(?<value>(?>(?:(?:[0-9](?:_?[0-9])*)(?:\.(?:[0-9](?:_?[0-9])*))?|\.(?:[0-9](?:_?[0-9])*))(?:[eE][-+]?(?:[0-9](?:_?[0-9])*))?)))(?![A-Za-z0-9])",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    public static bool IsSourceSyntaxValid(string sourceRef)
    {
        return !string.IsNullOrEmpty(sourceRef) && SourceRefPattern.IsMatch(sourceRef);
    }

    public static ActorObservationViolation? Evaluate(
        WorldState state,
        ActorState actor,
        ActorLocalObservation observation)
    {
        if (state is null
            || actor is null
            || observation is null
            || string.IsNullOrWhiteSpace(actor.ActorId)
            || string.IsNullOrWhiteSpace(actor.HomeSectorId)
            || actor.SystemRefs is null
            || string.IsNullOrWhiteSpace(observation.Signal))
        {
            return new(ActorObservationViolationCategory.InvalidWorldState);
        }

        if (!IsSourceSyntaxValid(observation.SourceRef))
        {
            return new(ActorObservationViolationCategory.InvalidSourceSyntax);
        }

        var protectedMetrics = CollectProtectedMetrics(state);
        if (protectedMetrics is null)
        {
            return new(ActorObservationViolationCategory.InvalidWorldState);
        }

        var sourceViolation = EvaluateSource(state, actor, observation.SourceRef);
        if (sourceViolation is not null)
        {
            return sourceViolation;
        }

        foreach (var value in new[]
        {
            observation.ObservationId,
            observation.Kind,
            observation.Signal,
            observation.SourceRef
        })
        {
            var contentViolation = EvaluateProtectedContent(protectedMetrics.Value, value);
            if (contentViolation is not null)
            {
                return contentViolation;
            }
        }

        return null;
    }

    public static ActorObservationViolation? EvaluateProjectedText(WorldState state, string value)
    {
        var protectedMetrics = CollectProtectedMetrics(state);
        return protectedMetrics is null
            ? new(ActorObservationViolationCategory.InvalidWorldState)
            : EvaluateProtectedContent(protectedMetrics.Value, value);
    }

    private static ActorObservationViolation? EvaluateSource(WorldState state, ActorState actor, string sourceRef)
    {
        if (sourceRef.StartsWith("event:", StringComparison.Ordinal))
        {
            return EvaluateActorBoundSource(actor.ActorId, sourceRef, AsterHeatAlarmEvent);
        }

        if (sourceRef.StartsWith("sensor:", StringComparison.Ordinal))
        {
            return EvaluateActorBoundSource(actor.ActorId, sourceRef, AsterHeatAlarmSensor);
        }

        var parts = sourceRef["system:".Length..].Split('.');
        if (parts.Length != 3)
        {
            return new(ActorObservationViolationCategory.UnknownOrAmbiguousSource);
        }

        if (!string.Equals(parts[0], actor.HomeSectorId, StringComparison.Ordinal)
            || actor.SystemRefs is null
            || !actor.SystemRefs.Contains(parts[1], StringComparer.Ordinal))
        {
            return new(ActorObservationViolationCategory.ActorSourceMismatch);
        }

        var metric = ResolveMetric(state, parts[0], parts[1], parts[2]);
        if (metric.Status != ResolutionStatus.Resolved)
        {
            return new(metric.Status == ResolutionStatus.InvalidWorldState
                ? ActorObservationViolationCategory.InvalidWorldState
                : ActorObservationViolationCategory.UnknownOrAmbiguousSource);
        }

        return metric.Metric!.Visibility == MetricVisibility.Observable
            ? null
            : new(ActorObservationViolationCategory.NonObservableSource);
    }

    private static ActorObservationViolation? EvaluateActorBoundSource(
        string actorId,
        string sourceRef,
        string allowedSource)
    {
        if (!string.Equals(sourceRef, allowedSource, StringComparison.Ordinal))
        {
            return new(ActorObservationViolationCategory.UnsupportedSource);
        }

        return string.Equals(actorId, "A1", StringComparison.Ordinal)
            ? null
            : new(ActorObservationViolationCategory.ActorSourceMismatch);
    }

    private static MetricResolution ResolveMetric(WorldState state, string sectorId, string systemId, string metricName)
    {
        if (state.Sectors is null || state.Sectors.Count == 0)
        {
            return new(ResolutionStatus.InvalidWorldState, null);
        }

        var matchingSectors = state.Sectors.Where(sector => sector is not null
            && string.Equals(sector.SectorId, sectorId, StringComparison.Ordinal)).ToArray();
        if (matchingSectors.Length != 1 || matchingSectors[0].Systems is null)
        {
            return new(ResolutionStatus.UnknownOrAmbiguous, null);
        }

        var matchingSystems = matchingSectors[0].Systems.Where(system => system is not null
            && string.Equals(system.SystemId, systemId, StringComparison.Ordinal)).ToArray();
        if (matchingSystems.Length != 1 || matchingSystems[0].Metrics is null)
        {
            return new(ResolutionStatus.UnknownOrAmbiguous, null);
        }

        var matchingMetrics = matchingSystems[0].Metrics.Where(metric => metric is not null
            && string.Equals(metric.Name, metricName, StringComparison.Ordinal)).ToArray();
        return matchingMetrics.Length == 1
            ? new(ResolutionStatus.Resolved, matchingMetrics[0])
            : new(ResolutionStatus.UnknownOrAmbiguous, null);
    }

    private static ProtectedMetrics? CollectProtectedMetrics(WorldState state)
    {
        if (state.Sectors is null || state.Sectors.Count == 0)
        {
            return null;
        }

        var sectorIds = new HashSet<string>(StringComparer.Ordinal);
        var globalSystemIds = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var values = new HashSet<double>();
        foreach (var sector in state.Sectors)
        {
            if (sector is null
                || string.IsNullOrWhiteSpace(sector.SectorId)
                || !sectorIds.Add(sector.SectorId)
                || sector.Systems is null
                || sector.Systems.Count == 0)
            {
                return null;
            }

            var systemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var system in sector.Systems)
            {
                if (system is null
                    || string.IsNullOrWhiteSpace(system.SystemId)
                    || !systemIds.Add(system.SystemId)
                    || !globalSystemIds.Add(system.SystemId)
                    || system.Metrics is null
                    || system.Metrics.Count == 0)
                {
                    return null;
                }

                var metricNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var metric in system.Metrics)
                {
                    if (metric is null
                        || string.IsNullOrWhiteSpace(metric.Name)
                        || !metricNames.Add(metric.Name)
                        || !Enum.IsDefined(metric.Visibility)
                        || !double.IsFinite(metric.Value))
                    {
                        return null;
                    }

                    if (metric.Visibility != MetricVisibility.Observable)
                    {
                        names.Add(NormalizeProjectedText(metric.Name));
                        values.Add(metric.Value);
                    }
                }
            }
        }

        return new(names, values);
    }

    private static ActorObservationViolation? EvaluateProtectedContent(ProtectedMetrics protectedMetrics, string value)
    {
        if (value is null)
        {
            return new(ActorObservationViolationCategory.InvalidWorldState);
        }

        if (value.Any(character => character is < ' ' or > '~'))
        {
            return new(ActorObservationViolationCategory.NonAsciiProjectedContent);
        }

        var normalized = NormalizeProjectedText(value);
        if (protectedMetrics.Names.Any(name => ContainsProtectedMetricToken(normalized, name)))
        {
            return new(ActorObservationViolationCategory.ProtectedMetricName);
        }

        foreach (Match match in NumericTokenPattern.Matches(normalized))
        {
            var candidate = match.Groups["value"].Value.Replace("_", string.Empty, StringComparison.Ordinal);
            if (double.TryParse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericValue)
                && protectedMetrics.Values.Contains(numericValue))
            {
                return new(ActorObservationViolationCategory.ProtectedMetricValue);
            }
        }

        return null;
    }

    private static string NormalizeProjectedText(string value)
    {
        value = value.Normalize(NormalizationForm.FormKC);
        var result = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Format
                or UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            if (category == UnicodeCategory.DecimalDigitNumber)
            {
                result.Append(Rune.GetNumericValue(rune).ToString(CultureInfo.InvariantCulture));
                continue;
            }

            result.Append(rune.ToString());
        }

        return result.ToString()
            .Replace("\u2212", "-")
            .Replace(",", ".")
            .Replace("\u066B", ".");
    }

    private static bool ContainsIdentifierToken(string signal, string name)
    {
        var startIndex = 0;
        while ((startIndex = signal.IndexOf(name, startIndex, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var beforeIsBoundary = startIndex == 0 || !IsIdentifierCharacter(signal[startIndex - 1]);
            var afterIndex = startIndex + name.Length;
            var afterIsBoundary = afterIndex == signal.Length || !IsIdentifierCharacter(signal[afterIndex]);
            if (beforeIsBoundary && afterIsBoundary)
            {
                return true;
            }
            startIndex++;
        }
        return false;
    }

    private static bool ContainsProtectedMetricToken(string value, string name)
    {
        if (ContainsIdentifierToken(value, name))
        {
            return true;
        }

        var segments = ExtractIdentifierSegments(value);
        for (var start = 0; start < segments.Length; start++)
        {
            var candidate = string.Empty;
            for (var end = start; end < segments.Length && candidate.Length < name.Length; end++)
            {
                candidate += segments[end];
                if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsIdentifierCharacter(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
    }

    private static string[] ExtractIdentifierSegments(string value)
    {
        var segments = new List<string>();
        var current = new StringBuilder();
        foreach (var character in value)
        {
            if (IsIdentifierCharacter(character))
            {
                current.Append(character);
            }
            else if (current.Length > 0)
            {
                segments.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length > 0)
        {
            segments.Add(current.ToString());
        }
        return segments.ToArray();
    }

    private enum ResolutionStatus
    {
        Resolved,
        UnknownOrAmbiguous,
        InvalidWorldState
    }

    private readonly record struct MetricResolution(ResolutionStatus Status, SystemMetric? Metric);

    private readonly record struct ProtectedMetrics(HashSet<string> Names, HashSet<double> Values);
}
