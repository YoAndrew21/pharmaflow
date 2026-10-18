using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace PharmaFlow.Api.Tests.Infrastructure;

/// <summary>Fail closed before opening any EF connection that is not the fixture-owned target.</summary>
internal sealed class ContainerDatabaseGuard : DbConnectionInterceptor
{
    private readonly NpgsqlConnectionStringBuilder expected;
    private readonly ConditionalWeakTable<DbConnection, object> verifiedConnections = new();

    internal ContainerDatabaseGuard(string connectionString)
    {
        expected = new(connectionString);
        if (string.Equals(expected.Database, "pharmaflow_dev", StringComparison.OrdinalIgnoreCase) ||
            expected.Database is null || !expected.Database.StartsWith("pharmaflow_test_", StringComparison.Ordinal) ||
            string.IsNullOrEmpty(expected.Host) || string.IsNullOrEmpty(expected.Username) ||
            string.IsNullOrEmpty(expected.Password))
            throw new InvalidOperationException("A positively identified disposable PostgreSQL test target is required.");
    }

    internal void AssertTarget(string? connectionString)
    {
        NpgsqlConnectionStringBuilder actual;
        try { actual = new(connectionString); }
        catch (ArgumentException) { throw new InvalidOperationException("Invalid disposable database target configuration."); }
        var mismatch = string.Equals(actual.Database, "pharmaflow_dev", StringComparison.OrdinalIgnoreCase) ? "developer database" :
            actual.Database != expected.Database ? "database" : actual.Host != expected.Host ? "host" :
            actual.Port != expected.Port ? "port" : actual.Username != expected.Username ? "username" :
            actual.Password != expected.Password ? "password" : null;
        if (mismatch is not null)
            throw new InvalidOperationException($"Refusing a connection outside the fixture-owned PostgreSQL test target ({mismatch} mismatch).");
    }

    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        AssertConnection(connection);
        return result;
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        AssertConnection(connection);
        return ValueTask.FromResult(result);
    }

    private void AssertConnection(DbConnection connection)
    {
        var settings = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        // Npgsql removes the public password after opening (Persist Security Info=false).
        // Only an already verified connection object may omit it on a subsequent open.
        // Destination fields are still rechecked every time; no connection is redirected.
        if (string.IsNullOrEmpty(settings.Password) && verifiedConnections.TryGetValue(connection, out _))
            settings.Password = expected.Password;
        AssertTarget(settings.ConnectionString);
        verifiedConnections.GetValue(connection, _ => new object());
    }
}
