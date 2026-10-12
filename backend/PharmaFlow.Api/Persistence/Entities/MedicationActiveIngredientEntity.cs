namespace PharmaFlow.Api.Persistence.Entities;

public sealed class MedicationActiveIngredientEntity
{
    public required string MedicationId { get; set; }
    // Zero-based position preserves the canonical ingredient array order.
    public int Position { get; set; }
    public required string Ingredient { get; set; }
    public MedicationEntity Medication { get; set; } = null!;
}
