using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PharmaFlow.Api.Persistence;
using PharmaFlow.Api.Persistence.Seeding;

namespace PharmaFlow.DatabaseRelease;

/// <summary>A separate, explicit job; never called by the web entry point.</summary>
public static class ReleaseCommand
{
    public static async Task<int> ExecuteAsync(string[] args, IConfiguration configuration,
        Action<string> report, CancellationToken cancellationToken = default)
    {
        var stage = "confirmation";
        try
        {
            if (args.Length != 1 || args[0] != "--release-database")
                throw new ReleaseSafetyException("explicit-release-action-required");
            if (configuration["RuntimeRoleProvisioning:Password"] is not null)
                throw new ReleaseSafetyException("release-rejects-provisioning-password");
            var settings = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("PharmaFlow"));
            var password = configuration["Database:Password"];
            var role = configuration["DatabaseRelease:RuntimeRole"];
            var expectedHost = configuration["DatabaseRelease:ExpectedHost"];
            var expectedDatabase = configuration["DatabaseRelease:ExpectedDatabase"];
            var expectedReleaseRole = configuration["DatabaseRelease:ExpectedReleaseRole"];
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(role)
                || string.IsNullOrWhiteSpace(expectedHost) || string.IsNullOrWhiteSpace(expectedDatabase)
                || string.IsNullOrWhiteSpace(expectedReleaseRole)
                || settings.Host != expectedHost || settings.Database != expectedDatabase
                || settings.Username != expectedReleaseRole || role == expectedReleaseRole)
                throw new ReleaseSafetyException("explicit-target-and-distinct-roles-required");
            settings.Password = password;

            stage = "preflight";
            await using var connection = new NpgsqlConnection(settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using (var identity = new NpgsqlCommand(
                "SELECT current_database() = @db AND current_user = @owner AND pg_get_userbyid(datdba) = current_user FROM pg_database WHERE datname = current_database()", connection))
            {
                identity.Parameters.AddWithValue("db", expectedDatabase);
                identity.Parameters.AddWithValue("owner", expectedReleaseRole);
                if (!Equals(await identity.ExecuteScalarAsync(cancellationToken), true))
                    throw new ReleaseSafetyException("release-must-be-confirmed-database-owner");
            }
            // Serialize this job's migration/seed/ACL sequence. Requires a direct DB endpoint.
            await using (var gate = new NpgsqlCommand(
                "SELECT pg_advisory_lock(hashtextextended(current_database() || ':pharmaflow-release', 0))", connection))
                await gate.ExecuteNonQueryAsync(cancellationToken);
            await RuntimePermissions.CheckRoleAsync(connection, role, cancellationToken);

            stage = "migrations";
            await RunBundleAsync(configuration, cancellationToken);
            report("Database release: migrations completed.");

            stage = "canonical-seed";
            await using var context = new PharmaFlowDbContext(
                new DbContextOptionsBuilder<PharmaFlowDbContext>().UseNpgsql(connection).Options);
            await new CanonicalDatabaseSeeder(context).SeedAsync(cancellationToken);
            report("Database release: canonical seed completed.");

            stage = "runtime-permissions";
            await RuntimePermissions.ApplyAndVerifyAsync(connection, role, cancellationToken);
            report("Database release: read-only runtime permissions verified. Completed.");
            return 0;
        }
        catch (Exception error)
        {
            // Never forward Npgsql, bundle output, configuration values or exception messages.
            var code = error is ReleaseSafetyException safety ? safety.Code : error.GetType().Name;
            if (error is PostgresException postgres) code += "/" + postgres.SqlState;
            report($"Database release failed during {stage}: {code}.");
            return 1;
        }
    }

    private static async Task RunBundleAsync(IConfiguration configuration, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "efbundle"))
            {
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        // The bundle uses the API's actual DbContext registration and separate password property.
        process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        process.StartInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";
        process.StartInfo.Environment["ConnectionStrings__PharmaFlow"] = configuration.GetConnectionString("PharmaFlow");
        process.StartInfo.Environment["Database__Password"] = configuration["Database:Password"];
        process.StartInfo.Environment.Remove("RuntimeRoleProvisioning__Password");
        process.StartInfo.Environment.Remove("RuntimeRoleProvisioning:Password");
        process.Start();
        try
        {
            await Task.WhenAll(process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, cancellationToken),
                process.StandardError.BaseStream.CopyToAsync(Stream.Null, cancellationToken),
                process.WaitForExitAsync(cancellationToken));
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
        if (process.ExitCode != 0) throw new ReleaseSafetyException("migration-bundle-failed");
    }
}

internal sealed class ReleaseSafetyException(string code) : Exception
{
    public string Code { get; } = code;
}
