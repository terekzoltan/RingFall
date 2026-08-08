namespace Ringfall.Core.Actions;

internal sealed record AsterF1ActionRule
{
    public required string ToolId { get; init; }
    public required string Action { get; init; }
    public required IReadOnlyList<string> RequiredArguments { get; init; }
    public required bool AllowAbsentArguments { get; init; }
    public required bool RejectFalseRequiresDryRun { get; init; }
}

internal static class AsterF1Policy
{
    public const string FamilyId = "aster-l1-action-work-order";
    public const string FamilyVersion = "0.1";
    public const string IssuerLayer = "L1";
    public const string SectorId = "Aster";
    public const string TargetLocation = "Aster/Grid-Spine-03";
    public const string WorkOrderTask = "inspect_and_patch";
    public const string SourceBranch = "Aster-G3";
    public const string TargetBranch = "Aster-G4";
    public const string CouplerAsset = "Aster/Grid-Spine-03/Coupler-7";
    public const double MaximumRerouteFraction = 0.20;

    private static readonly IReadOnlySet<string> ForbiddenArgumentKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "world_state",
        "state_patch",
        "state_diff",
        "delta",
        "set_value",
        "target_state",
        "execute",
        "apply",
        "macro_allocation",
        "doctrine",
        "institution_order",
        "council_doctrine"
    };

    private static readonly IReadOnlyDictionary<string, AsterF1ActionRule> Rules =
        new Dictionary<string, AsterF1ActionRule>(StringComparer.Ordinal)
        {
            ["query_branch_load"] = Rule("local_grid_panel", "query_branch_load", ["branch_id"], false, false),
            ["query_heat_alarm"] = Rule("local_grid_panel", "query_heat_alarm", [], true, false),
            ["dry_run_reroute"] = Rule(
                "local_grid_panel",
                "dry_run_reroute",
                ["from_branch", "to_branch", "load_fraction"],
                false,
                true),
            ["query_asset_status"] = Rule("maintenance_console", "query_asset_status", ["asset_id"], false, false),
            ["query_backlog"] = Rule("maintenance_console", "query_backlog", [], true, false),
            ["dry_run_patch"] = Rule("maintenance_console", "dry_run_patch", ["asset_id"], false, true)
        };

    public static bool TryGetActionRule(string action, out AsterF1ActionRule? rule)
    {
        return Rules.TryGetValue(action, out rule);
    }

    public static bool IsForbiddenArgumentKey(string key)
    {
        return ForbiddenArgumentKeys.Contains(key);
    }

    private static AsterF1ActionRule Rule(
        string toolId,
        string action,
        string[] requiredArguments,
        bool allowAbsentArguments,
        bool rejectFalseRequiresDryRun)
    {
        return new AsterF1ActionRule
        {
            ToolId = toolId,
            Action = action,
            RequiredArguments = Array.AsReadOnly(requiredArguments),
            AllowAbsentArguments = allowAbsentArguments,
            RejectFalseRequiresDryRun = rejectFalseRequiresDryRun
        };
    }
}
