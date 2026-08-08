using System.Reflection;
using Ringfall.Core.Actions;
using Ringfall.Core.Formal;

namespace Ringfall.Core.Tests;

[TestClass]
public sealed class AsterF1ReportReconcilerTests
{
    [TestMethod]
    public void Report_contract_has_no_authorizing_final_status()
    {
        var properties = typeof(AsterF1InterventionReport)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                "Blocking", "CoreIssueCodes", "CoreStatus", "Diagnostics", "FamilyId", "FamilyVersion",
                "FinalStatus", "FormalInputStatus", "MutationAuthorized", "PacketId", "Posture"
            },
            properties);
        Assert.IsFalse(Enum.GetNames<AsterF1ReportStatus>().Contains("Valid", StringComparer.Ordinal));
        CollectionAssert.AreEqual(
            new[]
            {
                "core_invalid", "core_denied", "core_unsupported", "solver_not_run",
                "formal_assessment_untrusted", "formal_core_disagreement", "formal_invalid",
                "formal_unsupported", "formal_repairable", "formal_fallback"
            },
            AsterF1ReportReconciler.DiagnosticCodes.ToArray());
    }

    [TestMethod]
    public void Missing_formal_input_uses_frozen_core_classification()
    {
        AssertReport(Allowed(), null, AsterF1ReportStatus.Fallback, "solver_not_run");
        AssertReport(Invalid(), null, AsterF1ReportStatus.Invalid, "core_invalid");
        AssertReport(Denied(), null, AsterF1ReportStatus.Invalid, "core_denied");
        AssertReport(Unsupported(), null, AsterF1ReportStatus.Unsupported, "core_unsupported");
    }

    [TestMethod]
    public void Allowed_core_decision_uses_frozen_formal_table()
    {
        AssertReport(
            Allowed(),
            AsterF1FormalInputStatus.Valid,
            AsterF1ReportStatus.Fallback,
            "formal_assessment_untrusted");
        AssertReport(
            Allowed(),
            AsterF1FormalInputStatus.Invalid,
            AsterF1ReportStatus.Fallback,
            "formal_core_disagreement",
            "formal_invalid");
        AssertReport(
            Allowed(),
            AsterF1FormalInputStatus.Unsupported,
            AsterF1ReportStatus.Unsupported,
            "formal_unsupported");
        AssertReport(
            Allowed(),
            AsterF1FormalInputStatus.Repairable,
            AsterF1ReportStatus.Repairable,
            "formal_repairable");
        AssertReport(
            Allowed(),
            AsterF1FormalInputStatus.Fallback,
            AsterF1ReportStatus.Fallback,
            "formal_fallback");
    }

    [TestMethod]
    public void Hypothetical_valid_input_never_overrides_core()
    {
        AssertReport(
            Denied(),
            AsterF1FormalInputStatus.Valid,
            AsterF1ReportStatus.Fallback,
            "core_denied",
            "formal_core_disagreement");
        AssertReport(
            Invalid(),
            AsterF1FormalInputStatus.Valid,
            AsterF1ReportStatus.Fallback,
            "core_invalid",
            "formal_core_disagreement");
        AssertReport(
            Unsupported(),
            AsterF1FormalInputStatus.Valid,
            AsterF1ReportStatus.Unsupported,
            "core_unsupported",
            "formal_core_disagreement");
    }

    [TestMethod]
    public void Nonvalid_formal_input_cannot_weaken_nonallowed_core_decision()
    {
        foreach (var formal in new[]
        {
            AsterF1FormalInputStatus.Invalid,
            AsterF1FormalInputStatus.Unsupported,
            AsterF1FormalInputStatus.Repairable,
            AsterF1FormalInputStatus.Fallback
        })
        {
            AssertReport(Denied(), formal, AsterF1ReportStatus.Invalid, "core_denied");
            AssertReport(Invalid(), formal, AsterF1ReportStatus.Invalid, "core_invalid");
            AssertReport(Unsupported(), formal, AsterF1ReportStatus.Unsupported, "core_unsupported");
        }
    }

    [TestMethod]
    public void Report_copies_core_issues_and_is_always_blocking()
    {
        var issues = new List<AsterL1ActionIssue>
        {
            new(AsterL1IssueCatalog.ToolExecuteDenied)
        };
        var decision = new AsterL1ActionDecision
        {
            Status = AsterL1DecisionStatus.Denied,
            Issues = issues
        };

        var report = AsterF1ReportReconciler.Reconcile("pkt-1", decision);
        issues.Add(new(AsterL1IssueCatalog.ToolUnavailable));

        Assert.AreEqual("aster-l1-action-work-order", report.FamilyId);
        Assert.AreEqual("0.1", report.FamilyVersion);
        Assert.AreEqual("report_only", report.Posture);
        Assert.IsTrue(report.Blocking);
        Assert.IsFalse(report.MutationAuthorized);
        CollectionAssert.AreEqual(new[] { "tool_execute_denied" }, report.CoreIssueCodes.ToArray());
    }

    private static AsterL1ActionDecision Allowed()
    {
        return AsterL1ActionDecision.Create([]);
    }

    private static AsterL1ActionDecision Invalid()
    {
        return AsterL1ActionDecision.Create([new(AsterL1IssueCatalog.InvalidCandidate)]);
    }

    private static AsterL1ActionDecision Denied()
    {
        return AsterL1ActionDecision.Create([new(AsterL1IssueCatalog.ToolExecuteDenied)]);
    }

    private static AsterL1ActionDecision Unsupported()
    {
        return AsterL1ActionDecision.Create([new(AsterL1IssueCatalog.ToolArgumentUnknown)]);
    }

    private static void AssertReport(
        AsterL1ActionDecision coreDecision,
        AsterF1FormalInputStatus? formalInput,
        AsterF1ReportStatus expectedStatus,
        params string[] expectedDiagnostics)
    {
        var report = AsterF1ReportReconciler.Reconcile("pkt-1", coreDecision, formalInput);

        Assert.AreEqual(expectedStatus, report.FinalStatus);
        CollectionAssert.AreEqual(expectedDiagnostics, report.Diagnostics.ToArray());
        CollectionAssert.AreEqual(
            coreDecision.Issues.Select(issue => issue.Code).ToArray(),
            report.CoreIssueCodes.ToArray());
        Assert.IsTrue(report.Blocking);
        Assert.IsFalse(report.MutationAuthorized);
    }
}
