using Npgsql;
using PharmaFlow.DatabaseRelease;
using Xunit;

namespace PharmaFlow.Api.Tests;

public sealed class ProvisioningPasswordModeTests
{
    [Theory]
    [InlineData(null, "ScramVerifier")]
    [InlineData("ScramVerifier", "ScramVerifier")]
    [InlineData("NeonPlaintext", "NeonPlaintext")]
    public void Only_exact_modes_are_recognized(string? input, string expected)
        => Assert.Equal(expected, ProvisioningPassword.ParseMode(input).ToString());

    [Theory]
    [InlineData("")]
    [InlineData("neonplaintext")]
    [InlineData("NeonPlaintext ")]
    [InlineData("Automatic")]
    public void Invalid_modes_never_fall_back(string input)
        => Assert.Throws<ReleaseSafetyException>(() => ProvisioningPassword.ParseMode(input));

    [Theory]
    [InlineData(SslMode.Disable)]
    [InlineData(SslMode.Prefer)]
    [InlineData(SslMode.Require)]
    [InlineData(SslMode.VerifyCA)]
    public void Neon_requires_verify_full_without_downgrade(SslMode ssl)
    {
        var settings = new NpgsqlConnectionStringBuilder { SslMode = ssl };
        Assert.Throws<ReleaseSafetyException>(() => ProvisioningPassword.ConfigureConnection(settings, ProvisioningPasswordMode.NeonPlaintext));
        Assert.Equal(ssl, settings.SslMode);
    }

    [Fact]
    public void Neon_disables_GSS_without_changing_TLS_or_connection_target()
    {
        var settings = new NpgsqlConnectionStringBuilder
        { Host = "synthetic-host.invalid", Database = "synthetic_database", Username = "synthetic_owner", SslMode = SslMode.VerifyFull, GssEncryptionMode = GssEncryptionMode.Require };
        ProvisioningPassword.ConfigureConnection(settings, ProvisioningPasswordMode.NeonPlaintext);
        Assert.Equal(SslMode.VerifyFull, settings.SslMode);
        Assert.Equal(GssEncryptionMode.Disable, settings.GssEncryptionMode);
        Assert.Equal("synthetic-host.invalid", settings.Host);
        Assert.Equal("synthetic_database", settings.Database);
        Assert.Equal("synthetic_owner", settings.Username);
        settings.GssEncryptionMode = GssEncryptionMode.Prefer;
        ProvisioningPassword.ConfigureConnection(settings, ProvisioningPasswordMode.ScramVerifier);
        Assert.Equal(GssEncryptionMode.Prefer, settings.GssEncryptionMode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("'")]
    [InlineData("\\")]
    [InlineData("\n")]
    [InlineData("é")]
    public void Plaintext_literal_rejects_unsafe_or_invalid_input_before_building_SQL(string suffix)
    {
        var password = suffix is "" or "short" ? suffix : new string('x', 48) + suffix;
        Assert.Throws<ReleaseSafetyException>(() => ProvisioningPassword.PlaintextLiteral(password));
    }

    [Fact]
    public void Literal_strategies_are_separate_and_scram_does_not_contain_original_password()
    {
        var password = new string('x', 48);
        var plain = ProvisioningPassword.Literal(password, ProvisioningPasswordMode.NeonPlaintext);
        Assert.True(plain == "E'" + password + "'", "Plaintext literal must be correctly delimited.");
        Assert.False(ProvisioningPassword.Literal(password, ProvisioningPasswordMode.ScramVerifier).Contains(password, StringComparison.Ordinal));
    }
}

public sealed class ProvisioningCommitClassificationTests
{
    [Theory]
    [InlineData("before", "BeforeCommit", false)]
    [InlineData("commit", "CommitInProgress", false)]
    [InlineData("transaction", "TransactionDisposal", true)]
    [InlineData("connection", "ConnectionDisposal", true)]
    [InlineData("none", "CommitAcknowledged", true)]
    public async Task Commit_and_disposal_failures_have_distinct_outcomes(string fail, string expected, bool acknowledged)
    {
        var prepares = 0; var commits = 0; var transactions = 0; var connections = 0;
        var outcome = await ProvisioningExecution.RunAsync(
            () => { prepares++; return fail == "before" ? Task.FromException(SensitiveError()) : Task.CompletedTask; },
            () => { commits++; return fail == "commit" ? Task.FromException(SensitiveError()) : Task.CompletedTask; },
            () => { transactions++; return fail == "transaction" ? ValueTask.FromException(SensitiveError()) : ValueTask.CompletedTask; },
            () => { connections++; return fail == "connection" ? ValueTask.FromException(SensitiveError()) : ValueTask.CompletedTask; });
        Assert.Equal(expected, outcome.Phase.ToString()); Assert.Equal(acknowledged, outcome.CommitAcknowledged);
        Assert.Equal(1, prepares); Assert.Equal(fail == "before" ? 0 : 1, commits);
        Assert.Equal(1, transactions); Assert.Equal(1, connections);
        var output = new List<string>();
        outcome = outcome.Publish(output.Add, "Provisioning completed.");
        Assert.Equal(fail == "none" ? 0 : 1, outcome.ExitCode);
        Assert.Equal(fail == "commit", output.Single().Contains("commit-outcome-unknown", StringComparison.Ordinal));
        Assert.DoesNotContain("sensitive-marker", output.Single());
    }

    [Fact]
    public async Task Reporting_failure_after_successful_commit_is_not_ambiguous_and_is_not_retried()
    {
        var outcome = await ProvisioningExecution.RunAsync(() => Task.CompletedTask, () => Task.CompletedTask, () => ValueTask.CompletedTask, () => ValueTask.CompletedTask);
        var reports = 0;
        outcome = outcome.Publish(_ => { reports++; throw SensitiveError(); }, "Completed.");
        Assert.Equal(1, reports); Assert.Equal(1, outcome.ExitCode);
        Assert.True(outcome.CommitAcknowledged); Assert.True(outcome.ReportingFailed);
        Assert.Equal(ProvisioningPhase.Reporting, outcome.Phase);
        Assert.Equal("reporting-failed", outcome.ErrorCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cleanup_and_reporting_never_replace_primary_failure(bool duringCommit)
    {
        var outcome = await ProvisioningExecution.RunAsync(
            () => duringCommit ? Task.CompletedTask : Task.FromException(new ReleaseSafetyException("primary-safety-failure")),
            () => Task.FromException(SensitiveError()),
            () => ValueTask.FromException(new InvalidOperationException("secondary-secret")),
            () => ValueTask.FromException(new InvalidOperationException("third-secret")));
        var expected = outcome.ErrorCode;
        Assert.True(outcome.CleanupFailed);
        outcome = outcome.Publish(_ => throw new Exception("report-secret"), "Completed.");
        Assert.Equal(expected, outcome.ErrorCode);
        Assert.Equal(duringCommit ? ProvisioningPhase.CommitInProgress : ProvisioningPhase.BeforeCommit, outcome.Phase);
    }

    private static PostgresException SensitiveError() => new("sensitive-marker-never-print", "ERROR", "ERROR", "XX000");
}
