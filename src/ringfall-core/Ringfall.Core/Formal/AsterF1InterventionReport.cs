using Ringfall.Core.Actions;

namespace Ringfall.Core.Formal;

internal enum AsterF1ReportStatus
{
    Invalid,
    Unsupported,
    Repairable,
    Fallback
}

internal enum AsterF1FormalInputStatus
{
    Valid,
    Invalid,
    Unsupported,
    Repairable,
    Fallback
}

internal sealed record AsterF1InterventionReport
{
    private IReadOnlyList<string> _coreIssueCodes = [];
    private IReadOnlyList<string> _diagnostics = [];

    public required string FamilyId { get; init; }
    public required string FamilyVersion { get; init; }
    public string Posture => "report_only";
    public required string PacketId { get; init; }
    public required AsterL1DecisionStatus CoreStatus { get; init; }

    public required IReadOnlyList<string> CoreIssueCodes
    {
        get => _coreIssueCodes;
        init => _coreIssueCodes = Array.AsReadOnly(value.ToArray());
    }

    public AsterF1FormalInputStatus? FormalInputStatus { get; init; }
    public required AsterF1ReportStatus FinalStatus { get; init; }

    public required IReadOnlyList<string> Diagnostics
    {
        get => _diagnostics;
        init => _diagnostics = Array.AsReadOnly(value.ToArray());
    }

    public bool Blocking => true;
    public bool MutationAuthorized => false;
}
