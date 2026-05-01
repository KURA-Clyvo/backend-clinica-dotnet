namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Clinica;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/v1/clinicas")]
public class ClinicasController : ControllerBase
{
    private readonly IClinicaService _service;

    public ClinicasController(IClinicaService service) => _service = service;

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ClinicaResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(result);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ClinicaResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(long id, [FromBody] ClinicaUpdateDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return Ok(result);
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(long id)
    {
        await _service.SoftDeleteAsync(id);
        return NoContent();
    }
}
