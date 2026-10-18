using PharmaFlow.Api.Models;

namespace PharmaFlow.Api.Services;

public interface IMedicationService
{
    Task<IReadOnlyList<Medication>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Medication?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
