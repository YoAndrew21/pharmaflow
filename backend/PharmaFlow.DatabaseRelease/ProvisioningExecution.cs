using Npgsql;

namespace PharmaFlow.DatabaseRelease;

internal enum ProvisioningPhase
{
    BeforeCommit, CommitInProgress, CommitAcknowledged, TransactionDisposal, ConnectionDisposal, Reporting
}

internal sealed record ProvisioningOutcome(ProvisioningPhase Phase, bool CommitAcknowledged, string? ErrorCode)
{
    internal int ExitCode => ErrorCode is null ? 0 : 1;
    internal bool CleanupFailed { get; init; }
    internal bool ReportingFailed { get; init; }

    internal ProvisioningOutcome Publish(Action<string> report, string successMessage)
    {
        var message = ErrorCode is null ? successMessage
            : "Runtime role provisioning failed during " + PhaseName(Phase)
                + " (commit acknowledged: " + (CommitAcknowledged ? "YES" : "NO") + "): " + ErrorCode + ".";
        try { report(message); return this; }
        catch (Exception)
        {
            // Reporting is never retried, and its exception cannot expose SQL/passwords
            // or replace a primary failure. A broken output stream is represented by exit code.
            return ErrorCode is null
                ? this with { Phase = ProvisioningPhase.Reporting, ErrorCode = "reporting-failed", ReportingFailed = true }
                : this with { ReportingFailed = true };
        }
    }

    private static string PhaseName(ProvisioningPhase phase) => phase switch
    {
        ProvisioningPhase.BeforeCommit => "before_commit",
        ProvisioningPhase.CommitInProgress => "commit_in_progress",
        ProvisioningPhase.CommitAcknowledged => "commit_acknowledged",
        ProvisioningPhase.TransactionDisposal => "transaction_disposal",
        ProvisioningPhase.ConnectionDisposal => "connection_disposal",
        ProvisioningPhase.Reporting => "reporting",
        _ => "unknown"
    };
}

// Resource operations are supplied by the real command. The narrow boundary permits
// deterministic commit/cleanup fault tests without adding hooks to database business logic.
internal static class ProvisioningExecution
{
    internal static async Task<ProvisioningOutcome> RunAsync(Func<Task> prepare, Func<Task> commit,
        Func<ValueTask> disposeTransaction, Func<ValueTask> disposeConnection)
    {
        var phase = ProvisioningPhase.BeforeCommit;
        var acknowledged = false;
        ProvisioningOutcome? failure = null;
        try
        {
            await prepare();
            phase = ProvisioningPhase.CommitInProgress;
            await commit();
            acknowledged = true;
            phase = ProvisioningPhase.CommitAcknowledged;
        }
        catch (Exception error) { failure = Failure(phase, acknowledged, error); }

        try { await disposeTransaction(); }
        catch (Exception error)
        {
            failure = failure is null ? Failure(ProvisioningPhase.TransactionDisposal, acknowledged, error) with { CleanupFailed = true }
                : failure with { CleanupFailed = true };
        }
        try { await disposeConnection(); }
        catch (Exception error)
        {
            failure = failure is null ? Failure(ProvisioningPhase.ConnectionDisposal, acknowledged, error) with { CleanupFailed = true }
                : failure with { CleanupFailed = true };
        }
        return failure ?? new(ProvisioningPhase.CommitAcknowledged, acknowledged, null);
    }

    private static ProvisioningOutcome Failure(ProvisioningPhase phase, bool acknowledged, Exception error)
    {
        var code = phase == ProvisioningPhase.CommitInProgress ? "commit-outcome-unknown-verify-before-retry"
            : error is ReleaseSafetyException safety ? safety.Code : error.GetType().Name;
        if (error is PostgresException postgres && postgres.SqlState.Length == 5
            && postgres.SqlState.All(c => char.IsAsciiDigit(c) || c is >= 'A' and <= 'Z')) code += "/" + postgres.SqlState;
        return new(phase, acknowledged, code);
    }
}
