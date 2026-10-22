using Microsoft.Extensions.Configuration;
using Xunit;
using ReleaseProgram = PharmaFlow.DatabaseRelease.Program;

namespace PharmaFlow.Api.Tests;

public sealed class SafeProvisioningConfigDiagnosticTests
{
    private static readonly string[] Names =
    [
        "owner_password_present_nonblank", "runtime_role_present_nonblank", "runtime_role_pattern_valid",
        "runtime_role_reserved_prefix", "runtime_equals_owner", "expected_owner_present_nonblank",
        "expected_host_present_nonblank", "expected_database_present_nonblank", "connection_string_present",
        "connection_string_parse_ok", "parsed_host_matches_expected", "parsed_database_matches_expected",
        "parsed_username_matches_expected", "provisioning_password_present", "provisioning_password_policy_valid",
        "provisioning_password_mode_present", "provisioning_password_mode_valid",
        "provisioning_password_mode_neon_plaintext", "neon_verify_full_valid", "neon_gss_disabled_effective"
    ];

    [Fact]
    public async Task Actual_dispatch_produces_only_allowlisted_booleans_without_resolving_unreachable_host()
    {
        var settings = Settings();
        var output = await Diagnose(settings);
        Assert.Equal("NO", output["runtime_role_reserved_prefix"]);
        Assert.Equal("NO", output["runtime_equals_owner"]);
        Assert.All(output.Where(pair => Names.Take(15).Contains(pair.Key) && pair.Key is not "runtime_role_reserved_prefix" and not "runtime_equals_owner"), pair => Assert.Equal("YES", pair.Value));
    }

    [Theory]
    [InlineData(null, "NO", "YES", "NO")]
    [InlineData("ScramVerifier", "YES", "YES", "NO")]
    [InlineData("NeonPlaintext", "YES", "YES", "YES")]
    [InlineData("", "YES", "NO", "NO")]
    [InlineData("neonplaintext", "YES", "NO", "NO")]
    [InlineData(" NeonPlaintext ", "YES", "NO", "NO")]
    [InlineData("invalid-sensitive-mode-marker", "YES", "NO", "NO")]
    public async Task Mode_checks_use_provisioning_parser_without_exposing_values(string? mode, string present, string valid, string neon)
    {
        var settings = Settings();
        settings["RuntimeRoleProvisioning:PasswordMode"] = mode;
        var output = await Diagnose(settings);
        Assert.Equal(present, output["provisioning_password_mode_present"]);
        Assert.Equal(valid, output["provisioning_password_mode_valid"]);
        Assert.Equal(neon, output["provisioning_password_mode_neon_plaintext"]);
        Assert.Equal("NO", output["neon_verify_full_valid"]);
        Assert.Equal("NO", output["neon_gss_disabled_effective"]);
    }

    [Theory]
    [InlineData("VerifyFull", "Prefer", "YES")]
    [InlineData("VerifyFull", "Require", "YES")]
    [InlineData("VerifyFull", "Disable", "YES")]
    [InlineData("Require", "Disable", "NO")]
    [InlineData("Prefer", "Prefer", "NO")]
    [InlineData("Disable", "Disable", "NO")]
    public async Task Neon_checks_use_effective_shared_connection_configuration(string ssl, string gss, string expected)
    {
        var settings = Settings();
        settings["RuntimeRoleProvisioning:PasswordMode"] = "NeonPlaintext";
        settings["ConnectionStrings:PharmaFlow"] += $";SSL Mode={ssl};GSS Encryption Mode={gss}";
        var output = await Diagnose(settings);
        Assert.Equal("YES", output["provisioning_password_mode_valid"]);
        Assert.Equal("YES", output["provisioning_password_mode_neon_plaintext"]);
        Assert.Equal(expected, output["neon_verify_full_valid"]);
        Assert.Equal(expected, output["neon_gss_disabled_effective"]);
    }

    [Fact]
    public async Task Invalid_connection_cannot_pass_neon_checks()
    {
        var settings = Settings();
        settings["RuntimeRoleProvisioning:PasswordMode"] = "NeonPlaintext";
        settings["ConnectionStrings:PharmaFlow"] = "Port=invalid-sensitive-port-marker";
        var output = await Diagnose(settings);
        Assert.Equal("NO", output["connection_string_parse_ok"]);
        Assert.Equal("YES", output["provisioning_password_mode_neon_plaintext"]);
        Assert.Equal("NO", output["neon_verify_full_valid"]);
        Assert.Equal("NO", output["neon_gss_disabled_effective"]);
    }

    [Theory]
    [InlineData("Database:Password", " ", "owner_password_present_nonblank")]
    [InlineData("DatabaseRelease:RuntimeRole", "Uppercase_role", "runtime_role_pattern_valid")]
    [InlineData("DatabaseRelease:ExpectedHost", "mismatched-host-marker", "parsed_host_matches_expected")]
    [InlineData("DatabaseRelease:ExpectedDatabase", "mismatched-database-marker", "parsed_database_matches_expected")]
    [InlineData("DatabaseRelease:ExpectedReleaseRole", "mismatched-owner-marker", "parsed_username_matches_expected")]
    [InlineData("RuntimeRoleProvisioning:Password", "", "provisioning_password_policy_valid")]
    [InlineData("RuntimeRoleProvisioning:Password", "invalid-runtime-secret-marker", "provisioning_password_policy_valid")]
    public async Task Independent_failures_do_not_short_circuit_or_expose_values(string key, string value, string check)
    {
        var settings = Settings();
        settings[key] = value;
        var output = await Diagnose(settings);
        Assert.Equal("NO", output[check]);
        Assert.Equal("YES", output["connection_string_parse_ok"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Host=must-not-be-printed.invalid;Port=not-a-port-marker")]
    [InlineData("unsupported-sensitive-marker=never-print-this-marker")]
    [InlineData("Host='unterminated-sensitive-marker")]
    public async Task Missing_or_invalid_connection_string_has_no_parser_details(string? value)
    {
        var settings = Settings();
        settings["ConnectionStrings:PharmaFlow"] = value;
        var output = await Diagnose(settings);
        Assert.Equal("NO", output["connection_string_parse_ok"]);
        Assert.All(output.Where(pair => pair.Key.StartsWith("parsed_", StringComparison.Ordinal)), pair => Assert.Equal("NO", pair.Value));
        Assert.Equal("YES", output["owner_password_present_nonblank"]);
    }

    [Theory]
    [InlineData("pg_reserved", "NO")]
    [InlineData("pharmaflow_app", "NO")]
    [InlineData("diagnostic_owner_marker", "YES")]
    public async Task Name_checks_preserve_exact_current_policy(string role, string equalsOwner)
    {
        var settings = Settings();
        settings["DatabaseRelease:RuntimeRole"] = role;
        var output = await Diagnose(settings);
        Assert.Equal("YES", output["runtime_role_pattern_valid"]);
        Assert.Equal(role.StartsWith("pg_", StringComparison.Ordinal) ? "YES" : "NO", output["runtime_role_reserved_prefix"]);
        Assert.Equal(equalsOwner, output["runtime_equals_owner"]);
    }

    [Fact]
    public async Task Absent_and_empty_provisioning_secrets_are_distinguished()
    {
        var settings = Settings();
        settings.Remove("RuntimeRoleProvisioning:Password");
        Assert.Equal("NO", (await Diagnose(settings))["provisioning_password_present"]);
        settings["RuntimeRoleProvisioning:Password"] = "";
        var output = await Diagnose(settings);
        Assert.Equal("YES", output["provisioning_password_present"]);
        Assert.Equal("NO", output["provisioning_password_policy_valid"]);
    }

    [Fact]
    public async Task Absent_configuration_is_safe_and_all_checks_are_still_emitted()
    {
        var output = await Diagnose(new Dictionary<string, string?>());
        Assert.Equal("NO", output["connection_string_present"]);
        Assert.Equal("NO", output["runtime_role_pattern_valid"]);
        // Literal equality deliberately mirrors provisioning, including null == null.
        Assert.Equal("YES", output["runtime_equals_owner"]);
    }

    private static async Task<Dictionary<string,string>> Diagnose(Dictionary<string,string?> values)
    {
        var messages = new List<string>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        Assert.Equal(0, await ReleaseProgram.ExecuteAsync(["--diagnose-provisioning-config"], configuration, messages.Add));
        Assert.Equal(Names.Length, messages.Count);
        foreach (var line in messages)
        {
            Assert.Matches("^[a-z_]+: (YES|NO)$", line);
            Assert.Contains(line.Split(": ")[0], Names);
        }
        var text = string.Join('\n', messages);
        foreach (var value in values.Values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            Assert.False(text.Contains(value!, StringComparison.Ordinal), "Diagnostic must not contain configuration values.");
        }
        Assert.DoesNotContain("SCRAM-SHA-256", text);
        Assert.DoesNotContain("Exception", text);
        return messages.ToDictionary(line => line.Split(": ")[0], line => line.Split(": ")[1]);
    }

    private static Dictionary<string,string?> Settings() => new()
    {
        ["ConnectionStrings:PharmaFlow"] = "Host=diagnostic-unresolvable-host-marker.invalid;Port=5432;Database=diagnostic_database_marker;Username=diagnostic_owner_marker",
        ["Database:Password"] = "diagnostic-owner-password-marker",
        ["DatabaseRelease:ExpectedHost"] = "diagnostic-unresolvable-host-marker.invalid",
        ["DatabaseRelease:ExpectedDatabase"] = "diagnostic_database_marker",
        ["DatabaseRelease:ExpectedReleaseRole"] = "diagnostic_owner_marker",
        ["DatabaseRelease:RuntimeRole"] = "diagnostic_runtime_marker",
        ["RuntimeRoleProvisioning:Password"] = "Diagnostic_Runtime_Password_Marker_0123456789_ABCDEF"
    };
}
