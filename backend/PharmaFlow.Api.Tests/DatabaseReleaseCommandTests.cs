using Microsoft.Extensions.Configuration;
using PharmaFlow.DatabaseRelease;
using Xunit;

namespace PharmaFlow.Api.Tests;

public sealed class DatabaseReleaseCommandTests
{
    [Theory]
    [InlineData("")]
    [InlineData("--seed")]
    [InlineData("--release-database --seed")]
    public async Task No_implicit_or_alternative_action_can_run_a_release(string command)
    {
        var messages = new List<string>();
        var result = await ReleaseCommand.ExecuteAsync(command.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            new ConfigurationBuilder().Build(), messages.Add);
        Assert.Equal(1, result);
        Assert.Single(messages);
        Assert.Contains("explicit-release-action-required", messages[0]);
    }

    [Theory]
    [InlineData("DatabaseRelease:ExpectedHost", "another-host")]
    [InlineData("DatabaseRelease:ExpectedDatabase", "another-database")]
    [InlineData("DatabaseRelease:RuntimeRole", "release_owner")]
    public async Task Target_or_role_mismatch_fails_before_connecting(string key, string value)
    {
        var values = Settings();
        values[key] = value;
        var messages = new List<string>();
        var result = await ReleaseCommand.ExecuteAsync(["--release-database"],
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(), messages.Add);
        Assert.Equal(1, result);
        Assert.Contains("explicit-target-and-distinct-roles-required", Assert.Single(messages));
    }

    [Fact]
    public async Task Connection_failure_is_nonzero_without_configuration_or_exception_details()
    {
        var values = Settings();
        var messages = new List<string>();
        var result = await ReleaseCommand.ExecuteAsync(["--release-database"],
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(), messages.Add);
        Assert.Equal(1, result);
        var output = Assert.Single(messages);
        Assert.Contains("preflight", output);
        Assert.DoesNotContain(values["Database:Password"]!, output);
        Assert.DoesNotContain(values["ConnectionStrings:PharmaFlow"]!, output);
        Assert.DoesNotContain("StackTrace", output);
    }

    private static Dictionary<string, string?> Settings() => new()
    {
        ["ConnectionStrings:PharmaFlow"] = "Host=127.0.0.1;Port=1;Database=release_offline_test;Username=release_owner;Timeout=1",
        ["Database:Password"] = "offline-test-marker-not-a-secret",
        ["DatabaseRelease:ExpectedHost"] = "127.0.0.1",
        ["DatabaseRelease:ExpectedDatabase"] = "release_offline_test",
        ["DatabaseRelease:ExpectedReleaseRole"] = "release_owner",
        ["DatabaseRelease:RuntimeRole"] = "runtime_reader"
    };
}
