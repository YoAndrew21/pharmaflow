using Npgsql;

namespace PharmaFlow.DatabaseRelease;

// All privileges are inspected as the owner; never SET ROLE or authenticate as runtime.
internal static class PostReleasePermissions
{
    internal static async Task VerifyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string role, Dictionary<string, bool> checks, CancellationToken token)
    {
        var safety = await RuntimeRoleSafetyEvaluator.EvaluateAsync(connection, role, transaction, token);
        bool All(params string[] names) => names.All(name => safety.Checks[name]);
        checks["runtime_role_exists"] = safety.Exists;
        checks["runtime_attributes_safe"] = All("runtime_login", "runtime_noinherit", "runtime_no_superuser",
            "runtime_no_createdb", "runtime_no_createrole", "runtime_no_replication", "runtime_no_bypassrls");
        checks["runtime_memberships_safe"] = All("runtime_no_direct_memberships");
        checks["runtime_no_ownership"] = All("runtime_no_database_ownership", "runtime_no_schema_ownership", "runtime_no_relation_ownership");
        checks["runtime_no_create"] = safety.NoCreate;
        checks["runtime_no_sequence_permissions"] = All("runtime_no_incompatible_sequence_privileges");
        if (!safety.Exists) return;

        await using var command = new NpgsqlCommand("""
            WITH runtime AS (SELECT oid FROM pg_roles WHERE rolname=@role),
            relations AS MATERIALIZED (
              SELECT c.*,n.nspname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
              WHERE n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname NOT LIKE 'pg_%'
                AND c.relkind IN ('r','p','v','m','f')
            ), unexpected AS (SELECT * FROM relations WHERE nspname <> 'public' OR relname <> ALL(@tables))
            SELECT
              has_database_privilege(r.oid,current_database(),'CONNECT') AS runtime_connect_allowed,
              has_schema_privilege(r.oid,'public','USAGE') AS runtime_public_usage_allowed,
              (SELECT count(*)=5 FROM relations c WHERE c.nspname='public' AND c.relname=ANY(@tables)
                AND has_table_privilege(r.oid,c.oid,'SELECT')) AS runtime_expected_select_allowed,
              NOT EXISTS (SELECT FROM unexpected c WHERE has_table_privilege(r.oid,c.oid,'SELECT')
                OR has_any_column_privilege(r.oid,c.oid,'SELECT')) AS runtime_no_unexpected_select,
              NOT EXISTS (SELECT FROM relations c WHERE has_table_privilege(r.oid,c.oid,'INSERT')
                OR has_any_column_privilege(r.oid,c.oid,'INSERT')) AS runtime_no_insert,
              NOT EXISTS (SELECT FROM relations c WHERE has_table_privilege(r.oid,c.oid,'UPDATE')
                OR has_any_column_privilege(r.oid,c.oid,'UPDATE')) AS runtime_no_update,
              NOT EXISTS (SELECT FROM relations c WHERE has_table_privilege(r.oid,c.oid,'DELETE')) AS runtime_no_delete,
              NOT EXISTS (SELECT FROM relations c WHERE has_table_privilege(r.oid,c.oid,'TRUNCATE')) AS runtime_no_truncate,
              NOT EXISTS (SELECT FROM relations c WHERE has_table_privilege(r.oid,c.oid,'REFERENCES')
                OR has_any_column_privilege(r.oid,c.oid,'REFERENCES')) AS runtime_no_references,
              NOT EXISTS (SELECT FROM relations c WHERE has_table_privilege(r.oid,c.oid,'TRIGGER')) AS runtime_no_trigger,
              NOT EXISTS (SELECT FROM relations c WHERE has_table_privilege(r.oid,c.oid,'MAINTAIN')) AS runtime_no_maintain,
              NOT has_database_privilege(r.oid,current_database(),'TEMPORARY') AS runtime_no_temporary,
              NOT EXISTS (SELECT FROM pg_class c WHERE CASE WHEN c.relkind='S' THEN
                has_sequence_privilege(r.oid,c.oid,'USAGE,SELECT,UPDATE') ELSE false END) AS runtime_no_sequence_permissions,
              NOT has_table_privilege(r.oid,'public."__EFMigrationsHistory"','SELECT,INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER,MAINTAIN')
                AND NOT has_any_column_privilege(r.oid,'public."__EFMigrationsHistory"','SELECT,INSERT,UPDATE,REFERENCES') AS runtime_no_migration_history_access,
              NOT has_database_privilege(r.oid,current_database(),'CONNECT WITH GRANT OPTION,CREATE WITH GRANT OPTION,TEMPORARY WITH GRANT OPTION')
                AND NOT EXISTS (SELECT FROM pg_namespace n WHERE has_schema_privilege(r.oid,n.oid,'USAGE WITH GRANT OPTION,CREATE WITH GRANT OPTION'))
                AND NOT EXISTS (SELECT FROM relations c WHERE
                  has_table_privilege(r.oid,c.oid,'SELECT WITH GRANT OPTION,INSERT WITH GRANT OPTION,UPDATE WITH GRANT OPTION,DELETE WITH GRANT OPTION,TRUNCATE WITH GRANT OPTION,REFERENCES WITH GRANT OPTION,TRIGGER WITH GRANT OPTION,MAINTAIN WITH GRANT OPTION')
                  OR has_any_column_privilege(r.oid,c.oid,'SELECT WITH GRANT OPTION,INSERT WITH GRANT OPTION,UPDATE WITH GRANT OPTION,REFERENCES WITH GRANT OPTION'))
                AND NOT EXISTS (SELECT FROM pg_class c WHERE CASE WHEN c.relkind='S' THEN
                  has_sequence_privilege(r.oid,c.oid,'USAGE WITH GRANT OPTION,SELECT WITH GRANT OPTION,UPDATE WITH GRANT OPTION') ELSE false END) AS runtime_no_grant_options,
              NOT EXISTS (SELECT FROM pg_default_acl d CROSS JOIN LATERAL aclexplode(d.defaclacl) a
                WHERE d.defaclobjtype IN ('r','S') AND (a.grantee=0 OR a.grantee=r.oid)) AS runtime_default_privileges_safe
            FROM runtime r
            """, connection, transaction);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("tables", ReleaseReadContract.Tables.ToArray());
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new ReleaseSafetyException("missing-runtime-result");
        for (var i = 0; i < reader.FieldCount; i++) checks[reader.GetName(i)] = reader.GetBoolean(i);
    }
}
