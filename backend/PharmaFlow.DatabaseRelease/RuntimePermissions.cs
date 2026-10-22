using Npgsql;

namespace PharmaFlow.DatabaseRelease;

internal static class RuntimePermissions
{
    private static string[] Tables => ReleaseReadContract.Tables.ToArray();

    public static async Task CheckRoleAsync(NpgsqlConnection connection, string role, CancellationToken token, NpgsqlTransaction? transaction = null)
    {
        var safety = await RuntimeRoleSafetyEvaluator.EvaluateAsync(connection, role, transaction, token);
        if (!safety.BasePreflightSafe)
            throw new ReleaseSafetyException("runtime-role-must-exist-without-ownership-elevated-flags-or-memberships");
    }

    public static async Task ApplyAndVerifyAsync(NpgsqlConnection connection, string role, CancellationToken token)
    {
        await using var transaction = await connection.BeginTransactionAsync(token);
        await CheckRoleAsync(connection, role, token, transaction);
        using var identifiers = new NpgsqlCommandBuilder();
        var runtime = identifiers.QuoteIdentifier(role);
        var database = identifiers.QuoteIdentifier(connection.Database);
        var tables = string.Join(", ", Tables.Select(table => "public." + identifiers.QuoteIdentifier(table)));
        // Dedicated application DB: PUBLIC must not give runtime CREATE/TEMP or data rights.
        // Future tables stay denied until explicitly reviewed/added to this read allowlist.
        var sql = $"""
            REVOKE CREATE, TEMPORARY ON DATABASE {database} FROM PUBLIC;
            REVOKE ALL PRIVILEGES ON DATABASE {database} FROM {runtime};
            GRANT CONNECT ON DATABASE {database} TO {runtime};
            REVOKE CREATE ON SCHEMA public FROM PUBLIC;
            REVOKE ALL PRIVILEGES ON SCHEMA public FROM {runtime};
            GRANT USAGE ON SCHEMA public TO {runtime};
            REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA public FROM PUBLIC, {runtime};
            REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public FROM PUBLIC, {runtime};
            ALTER DEFAULT PRIVILEGES REVOKE ALL PRIVILEGES ON TABLES FROM PUBLIC, {runtime};
            ALTER DEFAULT PRIVILEGES REVOKE ALL PRIVILEGES ON SEQUENCES FROM PUBLIC, {runtime};
            ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL PRIVILEGES ON TABLES FROM PUBLIC, {runtime};
            ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL PRIVILEGES ON SEQUENCES FROM PUBLIC, {runtime};
            GRANT SELECT ON TABLE {tables} TO {runtime};
            """;
        await using (var permissions = new NpgsqlCommand(sql, connection, transaction))
            await permissions.ExecuteNonQueryAsync(token);
        await using var verify = new NpgsqlCommand("""
            SELECT has_database_privilege(@role, current_database(), 'CONNECT')
              AND NOT has_database_privilege(@role, current_database(), 'CREATE,TEMPORARY')
              AND has_schema_privilege(@role, 'public', 'USAGE')
              AND NOT has_schema_privilege(@role, 'public', 'CREATE')
              AND (SELECT count(*) = 5 FROM pg_tables WHERE schemaname = 'public' AND tablename = ANY(@tables)
                   AND tableowner = current_user AND has_table_privilege(@role, quote_ident(schemaname) || '.' || quote_ident(tablename), 'SELECT'))
              AND NOT EXISTS (SELECT FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                   WHERE n.nspname = 'public' AND CASE WHEN c.relkind IN ('r','p','v','m','f') THEN
                   (has_table_privilege(@role, c.oid, 'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
                     OR has_any_column_privilege(@role, c.oid, 'INSERT,UPDATE,REFERENCES')
                     OR has_table_privilege(@role, c.oid, 'SELECT WITH GRANT OPTION')
                     OR has_any_column_privilege(@role, c.oid, 'SELECT WITH GRANT OPTION')
                     OR (c.relname <> ALL(@tables) AND has_table_privilege(@role, c.oid, 'SELECT'))) ELSE false END)
              AND NOT EXISTS (SELECT FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                   WHERE n.nspname = 'public' AND CASE WHEN c.relkind = 'S' THEN
                   has_sequence_privilege(@role, c.oid, 'USAGE,SELECT,UPDATE') ELSE false END)
            """, connection, transaction);
        verify.Parameters.AddWithValue("role", role);
        verify.Parameters.AddWithValue("tables", Tables);
        if (!Equals(await verify.ExecuteScalarAsync(token), true))
            throw new ReleaseSafetyException("effective-runtime-permissions-not-read-only");
        await transaction.CommitAsync(token);
    }
}
