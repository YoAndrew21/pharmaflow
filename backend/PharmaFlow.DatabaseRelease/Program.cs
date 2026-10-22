using Microsoft.Extensions.Configuration;

namespace PharmaFlow.DatabaseRelease;

internal static class Program
{
    public static Task<int> Main(string[] args) => ExecuteAsync(args,
        new ConfigurationBuilder().AddEnvironmentVariables().Build(), Console.WriteLine);

    public static Task<int> ExecuteAsync(string[] args, IConfiguration configuration, Action<string> report) =>
        args.Length == 1 && args[0] == "--verify-database-release"
            ? VerifyDatabaseReleaseCommand.ExecuteAsync(configuration, report)
            : args.Length == 1 && args[0] == "--verify-runtime-role"
            ? VerifyRuntimeRoleCommand.ExecuteAsync(configuration, report)
            : args.Length == 1 && args[0] == "--diagnose-provisioning-config"
            ? Task.FromResult(DiagnoseProvisioningConfigCommand.Execute(configuration, report))
            : args.Length == 1 && args[0] == "--provision-runtime-role"
            ? ProvisionRuntimeRoleCommand.ExecuteAsync(configuration, report)
            : ReleaseCommand.ExecuteAsync(args, configuration, report);
}
