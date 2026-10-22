using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using System.Text.RegularExpressions;
using PharmaFlow.Api.Persistence;

namespace PharmaFlow.DatabaseRelease;

internal static class PostReleaseSchema
{
    internal static async Task VerifyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        PharmaFlowDbContext context, Dictionary<string, bool> checks, CancellationToken token)
    {
        var assembly = context.GetService<IMigrationsAssembly>();
        var known = assembly.Migrations.Keys.Order(StringComparer.Ordinal).ToArray();
        var expectedNames = ReleaseReadContract.Tables.Append("__EFMigrationsHistory").Order(StringComparer.Ordinal).ToArray();
        var tables = await Read("SELECT c.relname::text FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public' AND c.relkind IN ('r','p')", r => r.GetString(0));
        checks["expected_tables_present"] = expectedNames.All(tables.Contains);
        checks["no_unexpected_public_tables"] = tables.Order(StringComparer.Ordinal).SequenceEqual(expectedNames);
        // Resolve history only after table presence; missing objects must never be treated as empty success.
        var history = await Read("SELECT \"MigrationId\" FROM public.\"__EFMigrationsHistory\"", r => r.GetString(0));
        checks["initial_migration_exactly_once"] = history.Count(id => id == ReleaseReadContract.InitialMigration) == 1;
        checks["no_unexpected_migrations"] = known.SequenceEqual(new[] { ReleaseReadContract.InitialMigration })
            && history.Order(StringComparer.Ordinal).SequenceEqual(known);
        if (!known.SequenceEqual(new[] { ReleaseReadContract.InitialMigration }))
            throw new ReleaseSafetyException("migration-contract-changed");

        // Reading migration operations does not generate or execute migration SQL.
        var operations = assembly.CreateMigration(assembly.Migrations[ReleaseReadContract.InitialMigration], context.Database.ProviderName!).UpOperations;
        var expectedTables = operations.OfType<CreateTableOperation>().ToArray();
        if (!expectedTables.Select(t => t.Name).Order(StringComparer.Ordinal).SequenceEqual(ReleaseReadContract.Tables.Order(StringComparer.Ordinal)))
            throw new ReleaseSafetyException("migration-contract-changed");
        // EF owns the history-table model. Inspect its generated creation script in memory,
        // never execute it or maintain a second hardcoded history schema.
        var historyScript = context.GetService<IHistoryRepository>().GetCreateScript();
        var historyColumns = Regex.Matches(historyScript, "\"(?<column>[^\"]+)\" (?<type>character varying\\([0-9]+\\)) NOT NULL")
            .Select(m => $"__EFMigrationsHistory|{m.Groups["column"].Value}|{m.Groups["type"].Value}|True").ToArray();
        var historyKey = Regex.Match(historyScript, "CONSTRAINT \"(?<name>[^\"]+)\" PRIMARY KEY \\(\"(?<column>[^\"]+)\"\\)");
        if (historyColumns.Length != 2 || !historyKey.Success) throw new ReleaseSafetyException("unsupported-history-model");
        var expectedColumns = expectedTables.SelectMany(t => t.Columns.Select(c =>
            $"{t.Name}|{c.Name}|{c.ColumnType}|{!c.IsNullable}")).Concat(historyColumns).Order(StringComparer.Ordinal);
        var actualColumns = await Read("""
            SELECT c.relname::text,a.attname::text,format_type(a.atttypid,a.atttypmod),a.attnotnull
            FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace JOIN pg_attribute a ON a.attrelid=c.oid
              JOIN pg_type typ ON typ.oid=a.atttypid
            WHERE n.nspname='public' AND c.relname=ANY(@tables) AND c.relkind='r' AND a.attnum>0 AND NOT a.attisdropped
              AND a.attidentity='' AND a.attgenerated='' AND NOT a.atthasdef
              AND a.attcollation=typ.typcollation AND NOT c.relrowsecurity AND NOT c.relforcerowsecurity
            """, r => $"{r.GetString(0)}|{r.GetString(1)}|{r.GetString(2)}|{r.GetBoolean(3)}");
        var expectedConstraints = new List<string>();
        foreach (var table in expectedTables)
        {
            var pk = table.PrimaryKey!;
            expectedConstraints.Add($"{table.Name}|{pk.Name}|p|{string.Join(',', pk.Columns)}||||");
            foreach (var fk in table.ForeignKeys)
            {
                var delete = fk.OnDelete switch
                {
                    ReferentialAction.Cascade => "c", ReferentialAction.Restrict => "r", ReferentialAction.NoAction => "a",
                    _ => throw new ReleaseSafetyException("unsupported-delete-contract")
                };
                expectedConstraints.Add($"{table.Name}|{fk.Name}|f|{string.Join(',', fk.Columns)}|{fk.PrincipalTable}|{string.Join(',', fk.PrincipalColumns!)}|{delete}|");
            }
            foreach (var check in table.CheckConstraints)
                expectedConstraints.Add($"{table.Name}|{check.Name}|c|||||{CheckExpression.Normalize(check.Sql)}");
        }
        expectedConstraints.Add($"__EFMigrationsHistory|{historyKey.Groups["name"].Value}|p|{historyKey.Groups["column"].Value}||||");
        var constraints = await Read("""
            SELECT c.relname::text,k.conname::text,k.contype::text,
              CASE WHEN k.contype IN ('p','f') THEN coalesce((SELECT string_agg(a.attname,',' ORDER BY u.pos) FROM unnest(k.conkey) WITH ORDINALITY u(num,pos)
                JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum=u.num),'') ELSE '' END AS columns,
              coalesce(p.relname::text,''),
              coalesce((SELECT string_agg(a.attname,',' ORDER BY u.pos) FROM unnest(k.confkey) WITH ORDINALITY u(num,pos)
                JOIN pg_attribute a ON a.attrelid=p.oid AND a.attnum=u.num),''),
              CASE WHEN k.contype='f' THEN k.confdeltype::text ELSE '' END,
              coalesce(pg_get_expr(k.conbin,k.conrelid),''),
              k.convalidated AND k.conenforced AND NOT k.condeferrable AND NOT k.condeferred
                AND (k.contype<>'f' OR (pn.nspname='public' AND k.confupdtype='a' AND k.confmatchtype='s'))
            FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace
            LEFT JOIN pg_class p ON p.oid=k.confrelid LEFT JOIN pg_namespace pn ON pn.oid=p.relnamespace
            WHERE n.nspname='public' AND c.relname=ANY(@tables) AND k.contype IN ('p','f','c','u','x')
            """, r => r.GetBoolean(8)
                ? string.Join('|', Enumerable.Range(0, 8).Select(i => i == 7 && r.GetString(2) == "c" ? CheckExpression.Normalize(r.GetString(i)) : r.GetString(i)))
                : "invalid");
        var expectedIndexes = operations.OfType<CreateIndexOperation>().Select(i =>
            $"{i.Table}|{i.Name}|{i.IsUnique}|{string.Join(',', i.Columns)}").ToList();
        expectedIndexes.AddRange(expectedTables.Select(t => $"{t.Name}|{t.PrimaryKey!.Name}|True|{string.Join(',', t.PrimaryKey.Columns)}"));
        expectedIndexes.Add($"__EFMigrationsHistory|{historyKey.Groups["name"].Value}|True|{historyKey.Groups["column"].Value}");
        var indexes = await Read("""
            SELECT t.relname::text,i.relname::text,x.indisunique,
              coalesce((SELECT string_agg(a.attname,',' ORDER BY u.pos) FROM unnest(x.indkey) WITH ORDINALITY u(num,pos)
                JOIN pg_attribute a ON a.attrelid=t.oid AND a.attnum=u.num),''),
              x.indisvalid AND x.indisready AND x.indislive AND x.indpred IS NULL AND x.indexprs IS NULL
                AND x.indnatts=x.indnkeyatts AND NOT x.indnullsnotdistinct
                AND am.amname='btree' AND NOT EXISTS (SELECT FROM unnest(x.indoption) o WHERE o<>0)
                AND NOT EXISTS (SELECT FROM unnest(x.indclass) op JOIN pg_opclass cl ON cl.oid=op WHERE NOT cl.opcdefault)
                AND NOT EXISTS (SELECT FROM unnest(x.indkey,x.indcollation) keys(num,coll)
                  JOIN pg_attribute a ON a.attrelid=t.oid AND a.attnum=keys.num WHERE keys.coll<>a.attcollation)
            FROM pg_index x JOIN pg_class t ON t.oid=x.indrelid JOIN pg_namespace n ON n.oid=t.relnamespace
              JOIN pg_class i ON i.oid=x.indexrelid JOIN pg_am am ON am.oid=i.relam
            WHERE n.nspname='public' AND t.relname=ANY(@tables)
            """, r => r.GetBoolean(4) ? $"{r.GetString(0)}|{r.GetString(1)}|{r.GetBoolean(2)}|{r.GetString(3)}" : "invalid");
        checks["schema_constraints_valid"] = actualColumns.Order(StringComparer.Ordinal).SequenceEqual(expectedColumns)
            && constraints.Order(StringComparer.Ordinal).SequenceEqual(expectedConstraints.Order(StringComparer.Ordinal))
            && indexes.Order(StringComparer.Ordinal).SequenceEqual(expectedIndexes.Order(StringComparer.Ordinal));

        async Task<List<T>> Read<T>(string sql, Func<NpgsqlDataReader, T> row)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("tables", expectedNames);
            await using var reader = await command.ExecuteReaderAsync(token);
            var values = new List<T>();
            while (await reader.ReadAsync(token)) values.Add(row(reader));
            return values;
        }
    }
}
