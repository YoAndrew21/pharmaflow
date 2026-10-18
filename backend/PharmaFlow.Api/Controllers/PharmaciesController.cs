using Microsoft.AspNetCore.Mvc;
using PharmaFlow.Api.Models;
using PharmaFlow.Api.Services;

namespace PharmaFlow.Api.Controllers;

[ApiController]
[Route("api/pharmacies")]
public sealed class PharmaciesController(IPharmacyService pharmacyService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PharmacyBranch>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PharmacyBranch>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await pharmacyService.GetAllAsync(cancellationToken));

    [HttpGet("{slug}")]
    [ProducesResponseType<PharmacyBranch>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PharmacyBranch>> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        var branch = await pharmacyService.GetBySlugAsync(slug, cancellationToken);
        if (branch is null)
        {
            return NotFound();
        }

        return Ok(branch);
    }
}
