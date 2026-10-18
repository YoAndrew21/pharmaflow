using PharmaFlow.Api.Models;

namespace PharmaFlow.Api.Services;

public interface IAvailabilityService
{
    Task<IReadOnlyList<Availability>> GetAllAsync(CancellationToken cancellationToken = default);
}
