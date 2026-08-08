using Ringfall.Core.Actions;

namespace Ringfall.Core.Formal;

internal static class AsterF1ReportReconciler
{
    public const string CoreInvalid = "core_invalid";
    public const string CoreDenied = "core_denied";
    public const string CoreUnsupported = "core_unsupported";
    public const string SolverNotRun = "solver_not_run";
    public const string FormalAssessmentUntrusted = "formal_assessment_untrusted";
    public const string FormalCoreDisagreement = "formal_core_disagreement";
    public const string FormalInvalid = "formal_invalid";
    public const string FormalUnsupported = "formal_unsupported";
    public const string FormalRepairable = "formal_repairable";
    public const string FormalFallback = "formal_fallback";

    public static readonly IReadOnlyList<string> DiagnosticCodes = Array.AsReadOnly(new[]
    {
        CoreInvalid,
        CoreDenied,
        CoreUnsupported,
        SolverNotRun,
        FormalAssessmentUntrusted,
        FormalCoreDisagreement,
        FormalInvalid,
        FormalUnsupported,
        FormalRepairable,
        FormalFallback
    });

    private static readonly IReadOnlyDictionary<string, int> DiagnosticRanks = DiagnosticCodes
        .Select((code, rank) => new KeyValuePair<string, int>(code, rank))
        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    public static AsterF1InterventionReport Reconcile(
        string packetId,
        AsterL1ActionDecision coreDecision,
        AsterF1FormalInputStatus? formalInputStatus = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packetId);
        ArgumentNullException.ThrowIfNull(coreDecision);

        var diagnostics = new List<string>();
        AsterF1ReportStatus finalStatus;

        if (formalInputStatus == AsterF1FormalInputStatus.Valid)
        {
            (finalStatus, diagnostics) = ReconcileUntrustedValid(coreDecision.Status);
        }
        else
        {
            (finalStatus, diagnostics) = coreDecision.Status switch
            {
                AsterL1DecisionStatus.Invalid => (AsterF1ReportStatus.Invalid, [CoreInvalid]),
                AsterL1DecisionStatus.Denied => (AsterF1ReportStatus.Invalid, [CoreDenied]),
                AsterL1DecisionStatus.Unsupported => (AsterF1ReportStatus.Unsupported, [CoreUnsupported]),
                _ => ReconcileAllowed(formalInputStatus)
            };
        }

        var orderedDiagnostics = diagnostics
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => DiagnosticRanks[code])
            .ToArray();

        return new AsterF1InterventionReport
        {
            FamilyId = AsterF1Policy.FamilyId,
            FamilyVersion = AsterF1Policy.FamilyVersion,
            PacketId = packetId,
            CoreStatus = coreDecision.Status,
            CoreIssueCodes = coreDecision.Issues.Select(issue => issue.Code).ToArray(),
            FormalInputStatus = formalInputStatus,
            FinalStatus = finalStatus,
            Diagnostics = orderedDiagnostics
        };
    }

    private static (AsterF1ReportStatus Status, List<string> Diagnostics) ReconcileAllowed(
        AsterF1FormalInputStatus? formalInputStatus)
    {
        return formalInputStatus switch
        {
            null => (AsterF1ReportStatus.Fallback, [SolverNotRun]),
            AsterF1FormalInputStatus.Invalid =>
                (AsterF1ReportStatus.Fallback, [FormalCoreDisagreement, FormalInvalid]),
            AsterF1FormalInputStatus.Unsupported =>
                (AsterF1ReportStatus.Unsupported, [FormalUnsupported]),
            AsterF1FormalInputStatus.Repairable =>
                (AsterF1ReportStatus.Repairable, [FormalRepairable]),
            AsterF1FormalInputStatus.Fallback =>
                (AsterF1ReportStatus.Fallback, [FormalFallback]),
            _ => throw new ArgumentOutOfRangeException(nameof(formalInputStatus))
        };
    }

    private static (AsterF1ReportStatus Status, List<string> Diagnostics) ReconcileUntrustedValid(
        AsterL1DecisionStatus coreStatus)
    {
        return coreStatus switch
        {
            AsterL1DecisionStatus.Allowed =>
                (AsterF1ReportStatus.Fallback, [FormalAssessmentUntrusted]),
            AsterL1DecisionStatus.Denied =>
                (AsterF1ReportStatus.Fallback, [CoreDenied, FormalCoreDisagreement]),
            AsterL1DecisionStatus.Invalid =>
                (AsterF1ReportStatus.Fallback, [CoreInvalid, FormalCoreDisagreement]),
            AsterL1DecisionStatus.Unsupported =>
                (AsterF1ReportStatus.Unsupported, [CoreUnsupported, FormalCoreDisagreement]),
            _ => throw new ArgumentOutOfRangeException(nameof(coreStatus))
        };
    }
}
