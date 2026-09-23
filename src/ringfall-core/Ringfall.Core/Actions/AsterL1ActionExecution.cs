using Ringfall.Core.Artifacts;
using Ringfall.Core.State;

namespace Ringfall.Core.Actions;

internal enum AsterL1ExecutionDisposition
{
    IdentityRejected,
    Rejected,
    Deferred,
    Succeeded,
    Failed
}

internal sealed class AsterL1ActionExecution
{
    public WorldState InitialState { get; }
    public WorldState FinalState { get; }
    public AsterL1ActionDecision Decision { get; }
    public AsterL1ExecutionDisposition Disposition { get; }
    public ActionTraceArtifact? ActionTrace { get; }
    public ExecutionResultArtifact? ExecutionResult { get; }
    public StateDiffArtifact? StateDiff { get; }

    public AsterL1ActionExecution(
        WorldState initialState,
        WorldState finalState,
        AsterL1ActionDecision decision,
        AsterL1ExecutionDisposition disposition,
        ActionTraceArtifact? actionTrace,
        ExecutionResultArtifact? executionResult,
        StateDiffArtifact? stateDiff)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(finalState);
        ArgumentNullException.ThrowIfNull(decision);

        if ((actionTrace is null) != (executionResult is null))
        {
            throw new ArgumentException("ActionTrace and ExecutionResult must be present together.");
        }
        if ((actionTrace is null) != (disposition == AsterL1ExecutionDisposition.IdentityRejected))
        {
            throw new ArgumentException("Only identity rejection can omit the artifact pair.");
        }
        if (disposition == AsterL1ExecutionDisposition.IdentityRejected && decision.Status == AsterL1DecisionStatus.Allowed)
        {
            throw new ArgumentException("An allowed candidate cannot fail artifact identity admission.");
        }
        if (disposition == AsterL1ExecutionDisposition.Rejected && decision.Status == AsterL1DecisionStatus.Allowed
            || disposition is AsterL1ExecutionDisposition.Deferred or AsterL1ExecutionDisposition.Succeeded or AsterL1ExecutionDisposition.Failed
                && decision.Status != AsterL1DecisionStatus.Allowed)
        {
            throw new ArgumentException("Execution disposition must agree with the fresh A4-F decision.");
        }
        if (disposition == AsterL1ExecutionDisposition.Succeeded && actionTrace?.ActionClass == "work_order"
            && (stateDiff is null || ReferenceEquals(initialState, finalState)))
        {
            throw new ArgumentException("A successful WorkOrder requires a StateDiff and a changed final state.");
        }
        if (stateDiff is not null
            && (disposition != AsterL1ExecutionDisposition.Succeeded
                || actionTrace?.ActionClass != "work_order"
                || ReferenceEquals(initialState, finalState)))
        {
            throw new ArgumentException("Only a successful WorkOrder can publish a StateDiff.");
        }
        if (stateDiff is null && !ReferenceEquals(initialState, finalState))
        {
            throw new ArgumentException("A changed final state requires a successful WorkOrder StateDiff.");
        }
        if (actionTrace is not null
            && (actionTrace.ExecutionStatus != executionResult!.ExecutionStatus
                || actionTrace.ExecutionStatus != (disposition switch
                {
                    AsterL1ExecutionDisposition.Rejected => "rejected",
                    AsterL1ExecutionDisposition.Deferred => "deferred",
                    AsterL1ExecutionDisposition.Succeeded => "success",
                    AsterL1ExecutionDisposition.Failed => "failed",
                    _ => throw new ArgumentOutOfRangeException(nameof(disposition))
                })))
        {
            throw new ArgumentException("Artifact statuses must match the execution disposition.");
        }

        InitialState = initialState;
        FinalState = finalState;
        Decision = decision;
        Disposition = disposition;
        ActionTrace = actionTrace;
        ExecutionResult = executionResult;
        StateDiff = stateDiff;
    }
}
