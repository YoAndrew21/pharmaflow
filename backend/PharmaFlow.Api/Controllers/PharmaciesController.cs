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
    public ActionResult<IReadOnlyList<PharmacyBranch>> GetAll() => Ok(pharmacyService.GetAll());

    [HttpGet("{slug}")]
    [ProducesResponseType<PharmacyBranch>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<PharmacyBranch> GetBySlug(string slug)
    {
        var branch = pharmacyService.GetBySlug(slug);
        if (branch is null)
        {
            return NotFound();
        }

        return Ok(branch);
    }
}
