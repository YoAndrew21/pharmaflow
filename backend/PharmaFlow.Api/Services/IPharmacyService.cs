using PharmaFlow.Api.Models;

namespace PharmaFlow.Api.Services;

public interface IPharmacyService
{
    Task<IReadOnlyList<PharmacyBranch>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PharmacyBranch?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
