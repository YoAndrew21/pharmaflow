using Microsoft.AspNetCore.Mvc;
using PharmaFlow.Api.Models;
using PharmaFlow.Api.Services;

namespace PharmaFlow.Api.Controllers;

[ApiController]
[Route("api/medications")]
public sealed class MedicationsController(IMedicationService medicationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Medication>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Medication>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await medicationService.GetAllAsync(cancellationToken));

    [HttpGet("{slug}")]
    [ProducesResponseType<Medication>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Medication>> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        var medication = await medicationService.GetBySlugAsync(slug, cancellationToken);
        if (medication is null)
        {
            return NotFound();
        }

        return Ok(medication);
    }
}
