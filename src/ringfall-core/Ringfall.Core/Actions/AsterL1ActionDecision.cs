namespace Ringfall.Core.Actions;

internal enum AsterL1DecisionStatus
{
    Allowed,
    Denied,
    Unsupported,
    Invalid
}

internal readonly record struct AsterL1ActionIssue(string Code, string? Field = null);

internal sealed record AsterL1ActionDecision
{
    private IReadOnlyList<AsterL1ActionIssue> _issues = [];

    public required AsterL1DecisionStatus Status { get; init; }

    public required IReadOnlyList<AsterL1ActionIssue> Issues
    {
        get => _issues;
        init => _issues = Array.AsReadOnly(value.ToArray());
    }

    public static AsterL1ActionDecision Create(IEnumerable<AsterL1ActionIssue> issues)
    {
        var ordered = issues
            .Distinct()
            .OrderBy(issue => AsterL1IssueCatalog.Rank(issue.Code))
            .ThenBy(issue => issue.Field ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        var status = ordered.Any(issue => AsterL1IssueCatalog.Status(issue.Code) == AsterL1DecisionStatus.Invalid)
            ? AsterL1DecisionStatus.Invalid
            : ordered.Any(issue => AsterL1IssueCatalog.Status(issue.Code) == AsterL1DecisionStatus.Denied)
                ? AsterL1DecisionStatus.Denied
                : ordered.Length > 0
                    ? AsterL1DecisionStatus.Unsupported
                    : AsterL1DecisionStatus.Allowed;

        return new AsterL1ActionDecision
        {
            Status = status,
            Issues = ordered
        };
    }
}

internal static class AsterL1IssueCatalog
{
    public const string InvalidWorldState = "invalid_world_state";
    public const string InvalidCandidate = "invalid_candidate";
    public const string PacketTypeMismatch = "packet_type_mismatch";
    public const string SchemaVersionMismatch = "schema_version_mismatch";
    public const string IssuerNotFound = "issuer_not_found";
    public const string IssuerAmbiguous = "issuer_ambiguous";
    public const string IssuerLayerMismatch = "issuer_layer_mismatch";
    public const string IssuerLayerDenied = "issuer_layer_denied";
    public const string IssuerSectorUnsupported = "issuer_sector_unsupported";
    public const string VisibilityIntentUnsupported = "visibility_intent_unsupported";
    public const string ToolNotFound = "tool_not_found";
    public const string ToolAmbiguous = "tool_ambiguous";
    public const string ToolNotReferenced = "tool_not_referenced";
    public const string ToolUnavailable = "tool_unavailable";
    public const string ToolActionNotSupported = "tool_action_not_supported";
    public const string ToolActionOutsideFamily = "tool_action_outside_family";
    public const string ToolModeInvalid = "tool_mode_invalid";
    public const string ToolExecuteDenied = "tool_execute_denied";
    public const string ToolRequiresDryRunConflict = "tool_requires_dry_run_conflict";
    public const string ToolArgumentsInvalid = "tool_arguments_invalid";
    public const string ToolArgumentsMissing = "tool_arguments_missing";
    public const string ToolMacroSurfaceDenied = "tool_macro_surface_denied";
    public const string ToolArgumentUnknown = "tool_argument_unknown";
    public const string ToolArgumentTypeInvalid = "tool_argument_type_invalid";
    public const string ToolArgumentValueInvalid = "tool_argument_value_invalid";
    public const string ToolArgumentValueUnsupported = "tool_argument_value_unsupported";
    public const string WorkOrderTargetMissing = "work_order_target_missing";
    public const string WorkOrderTargetsConflict = "work_order_targets_conflict";
    public const string CrewPoolUnsupported = "crew_pool_unsupported";
    public const string CrewNotFound = "crew_not_found";
    public const string CrewAmbiguous = "crew_ambiguous";
    public const string CrewNotReferenced = "crew_not_referenced";
    public const string CrewUnavailable = "crew_unavailable";
    public const string CrewAssignmentMismatch = "crew_assignment_mismatch";
    public const string CrewSectorMismatch = "crew_sector_mismatch";
    public const string WorkOrderLocationUnsupported = "work_order_location_unsupported";
    public const string WorkOrderTaskUnsupported = "work_order_task_unsupported";
    public const string WorkOrderRiskToleranceUnsupported = "work_order_risk_tolerance_unsupported";
    public const string WorkOrderResourceAuthorityDenied = "work_order_resource_authority_denied";
    public const string WorkOrderConstraintsUnsupported = "work_order_constraints_unsupported";
    public const string WorkOrderExpectedReportUnsupported = "work_order_expected_report_unsupported";
    public const string WorkOrderFallbackUnsupported = "work_order_fallback_unsupported";
    public const string WorkOrderStealthDenied = "work_order_stealth_denied";
    public const string WorkOrderPublicVisibilityUnsupported = "work_order_public_visibility_unsupported";

    public static readonly IReadOnlyList<string> Codes = Array.AsReadOnly(new[]
    {
        InvalidWorldState,
        InvalidCandidate,
        PacketTypeMismatch,
        SchemaVersionMismatch,
        IssuerNotFound,
        IssuerAmbiguous,
        IssuerLayerMismatch,
        IssuerLayerDenied,
        IssuerSectorUnsupported,
        VisibilityIntentUnsupported,
        ToolNotFound,
        ToolAmbiguous,
        ToolNotReferenced,
        ToolUnavailable,
        ToolActionNotSupported,
        ToolActionOutsideFamily,
        ToolModeInvalid,
        ToolExecuteDenied,
        ToolRequiresDryRunConflict,
        ToolArgumentsInvalid,
        ToolArgumentsMissing,
        ToolMacroSurfaceDenied,
        ToolArgumentUnknown,
        ToolArgumentTypeInvalid,
        ToolArgumentValueInvalid,
        ToolArgumentValueUnsupported,
        WorkOrderTargetMissing,
        WorkOrderTargetsConflict,
        CrewPoolUnsupported,
        CrewNotFound,
        CrewAmbiguous,
        CrewNotReferenced,
        CrewUnavailable,
        CrewAssignmentMismatch,
        CrewSectorMismatch,
        WorkOrderLocationUnsupported,
        WorkOrderTaskUnsupported,
        WorkOrderRiskToleranceUnsupported,
        WorkOrderResourceAuthorityDenied,
        WorkOrderConstraintsUnsupported,
        WorkOrderExpectedReportUnsupported,
        WorkOrderFallbackUnsupported,
        WorkOrderStealthDenied,
        WorkOrderPublicVisibilityUnsupported
    });

    private static readonly IReadOnlyDictionary<string, int> Ranks = Codes
        .Select((code, rank) => new KeyValuePair<string, int>(code, rank))
        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> InvalidCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        InvalidWorldState,
        InvalidCandidate,
        PacketTypeMismatch,
        SchemaVersionMismatch,
        IssuerAmbiguous,
        ToolAmbiguous,
        ToolModeInvalid,
        ToolArgumentsInvalid,
        ToolArgumentTypeInvalid,
        ToolArgumentValueInvalid,
        WorkOrderTargetMissing,
        CrewAmbiguous
    };

    private static readonly IReadOnlySet<string> DeniedCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        IssuerNotFound,
        IssuerLayerMismatch,
        IssuerLayerDenied,
        ToolNotFound,
        ToolNotReferenced,
        ToolUnavailable,
        ToolActionNotSupported,
        ToolExecuteDenied,
        ToolRequiresDryRunConflict,
        ToolArgumentsMissing,
        ToolMacroSurfaceDenied,
        WorkOrderTargetsConflict,
        CrewNotFound,
        CrewNotReferenced,
        CrewUnavailable,
        CrewAssignmentMismatch,
        CrewSectorMismatch,
        WorkOrderResourceAuthorityDenied,
        WorkOrderStealthDenied
    };

    public static int Rank(string code)
    {
        return Ranks.TryGetValue(code, out var rank)
            ? rank
            : throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown Aster L1 issue code.");
    }

    public static AsterL1DecisionStatus Status(string code)
    {
        _ = Rank(code);
        return InvalidCodes.Contains(code)
            ? AsterL1DecisionStatus.Invalid
            : DeniedCodes.Contains(code)
                ? AsterL1DecisionStatus.Denied
                : AsterL1DecisionStatus.Unsupported;
    }
}
