namespace PharmaFlow.DatabaseRelease;

// Shared by the permission writer and the independent post-release reader.
internal static class ReleaseReadContract
{
    internal const string InitialMigration = "20261011043419_InitialCreate";
    internal static IReadOnlyList<string> Tables { get; } = Array.AsReadOnly(new[]
    {
        "medications", "medication_active_ingredients", "pharmacy_branches", "opening_intervals", "availability"
    });
}
