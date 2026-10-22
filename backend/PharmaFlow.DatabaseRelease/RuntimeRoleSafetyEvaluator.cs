using Npgsql;

namespace PharmaFlow.DatabaseRelease;

// Catalog reads only. No identifiers or secrets are returned to the caller.
internal sealed record RuntimeRoleSafetyResult(IReadOnlyDictionary<string, bool> Checks, bool BasePreflightSafe)
{
    public bool Exists => Checks["runtime_role_exists"];
    public bool NoCreate => Checks["runtime_no_effective_database_create"] && Checks["runtime_no_effective_schema_create"];
    public bool ExistingDataSafe => Checks["runtime_no_incompatible_table_privileges"]
        && Checks["runtime_no_incompatible_column_privileges"] && Checks["runtime_no_incompatible_sequence_privileges"];
    public bool InitialSafe => BasePreflightSafe && Checks.Values.All(value => value);
}

internal static class RuntimeRoleSafetyEvaluator
{
    internal static readonly string[] CheckNames =
    [
        "runtime_role_exists", "runtime_login", "runtime_noinherit", "runtime_no_superuser",
        "runtime_no_createdb", "runtime_no_createrole", "runtime_no_replication", "runtime_no_bypassrls",
        "runtime_no_direct_memberships", "runtime_no_database_ownership", "runtime_no_schema_ownership",
        "runtime_no_relation_ownership", "runtime_no_effective_database_create", "runtime_no_effective_schema_create",
        "runtime_no_incompatible_table_privileges", "runtime_no_incompatible_column_privileges",
        "runtime_no_incompatible_sequence_privileges", "runtime_no_application_data_privileges"
    ];

    public static async Task<RuntimeRoleSafetyResult> EvaluateAsync(NpgsqlConnection connection, string role,
        NpgsqlTransaction? transaction, CancellationToken token)
    {
        var checks = CheckNames.ToDictionary(name => name, _ => false);
        await using var command = new NpgsqlCommand("""
            WITH relations AS (
              SELECT c.*, n.nspname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
              WHERE n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname NOT LIKE 'pg_%'
            )
            SELECT true AS runtime_role_exists,
              r.rolcanlogin AS runtime_login, NOT r.rolinherit AS runtime_noinherit,
              NOT r.rolsuper AS runtime_no_superuser, NOT r.rolcreatedb AS runtime_no_createdb,
              NOT r.rolcreaterole AS runtime_no_createrole, NOT r.rolreplication AS runtime_no_replication,
              NOT r.rolbypassrls AS runtime_no_bypassrls,
              NOT EXISTS (SELECT FROM pg_auth_members WHERE member=r.oid) AS runtime_no_direct_memberships,
              NOT EXISTS (SELECT FROM pg_database WHERE datdba=r.oid) AS runtime_no_database_ownership,
              NOT EXISTS (SELECT FROM pg_namespace WHERE nspowner=r.oid) AS runtime_no_schema_ownership,
              NOT EXISTS (SELECT FROM pg_class WHERE relowner=r.oid) AS runtime_no_relation_ownership,
              NOT has_database_privilege(r.oid,current_database(),'CREATE') AS runtime_no_effective_database_create,
              NOT EXISTS (SELECT FROM pg_namespace n WHERE has_schema_privilege(r.oid,n.oid,'CREATE')) AS runtime_no_effective_schema_create,
              NOT EXISTS (SELECT FROM relations c WHERE CASE WHEN c.relkind IN ('r','p','v','m','f') THEN (
                has_table_privilege(r.oid,c.oid,'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
                OR has_table_privilege(r.oid,c.oid,'SELECT WITH GRANT OPTION')
                OR ((c.nspname <> 'public' OR c.relname <> ALL(@tables))
                  AND has_table_privilege(r.oid,c.oid,'SELECT'))) ELSE false END) AS runtime_no_incompatible_table_privileges,
              NOT EXISTS (SELECT FROM relations c WHERE CASE WHEN c.relkind IN ('r','p','v','m','f') THEN (
                has_any_column_privilege(r.oid,c.oid,'INSERT,UPDATE,REFERENCES')
                OR has_any_column_privilege(r.oid,c.oid,'SELECT WITH GRANT OPTION')
                OR ((c.nspname <> 'public' OR c.relname <> ALL(@tables))
                  AND has_any_column_privilege(r.oid,c.oid,'SELECT'))) ELSE false END) AS runtime_no_incompatible_column_privileges,
              NOT EXISTS (SELECT FROM relations c WHERE CASE WHEN c.relkind='S'
                THEN has_sequence_privilege(r.oid,c.oid,'USAGE,SELECT,UPDATE') ELSE false END) AS runtime_no_incompatible_sequence_privileges,
              NOT EXISTS (SELECT FROM relations c WHERE CASE WHEN c.relkind IN ('r','p','v','m','f') THEN
                has_table_privilege(r.oid,c.oid,'SELECT,INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
                OR has_any_column_privilege(r.oid,c.oid,'SELECT,INSERT,UPDATE,REFERENCES')
                WHEN c.relkind='S' THEN has_sequence_privilege(r.oid,c.oid,'USAGE,SELECT,UPDATE')
                ELSE false END) AS runtime_no_application_data_privileges,
              NOT EXISTS (SELECT FROM pg_namespace n WHERE n.nspname <> 'public'
                AND has_schema_privilege(r.oid,n.oid,'CREATE')) AS base_no_nonpublic_create,
              NOT EXISTS (SELECT FROM relations c WHERE c.nspname <> 'public' AND CASE WHEN c.relkind IN ('r','p','v','m','f')
                THEN (has_table_privilege(r.oid,c.oid,'SELECT,INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
                OR has_any_column_privilege(r.oid,c.oid,'SELECT,INSERT,UPDATE,REFERENCES')) ELSE false END) AS base_no_other_schema_data
            FROM pg_roles r WHERE r.rolname=@role
            """, connection, transaction);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("tables", ReleaseReadContract.Tables.ToArray());
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return new(checks, false);
        foreach (var name in CheckNames) checks[name] = reader.GetBoolean(reader.GetOrdinal(name));
        // Preserve the legacy release/preflight policy: NOINHERIT and public CREATE/data checks
        // belong to initial verification/provisioning profiles, not this existing base contract.
        var baseSafe = checks["runtime_role_exists"] && checks["runtime_login"]
            && checks["runtime_no_superuser"] && checks["runtime_no_createdb"] && checks["runtime_no_createrole"]
            && checks["runtime_no_replication"] && checks["runtime_no_bypassrls"] && checks["runtime_no_direct_memberships"]
            && checks["runtime_no_database_ownership"] && checks["runtime_no_schema_ownership"] && checks["runtime_no_relation_ownership"]
            && reader.GetBoolean(reader.GetOrdinal("base_no_nonpublic_create"))
            && reader.GetBoolean(reader.GetOrdinal("base_no_other_schema_data"));
        return new(checks, baseSafe);
    }
}
