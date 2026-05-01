namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Veterinario;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/veterinarios")]
public class VeterinariosController : ControllerBase
{
    private readonly IVeterinarioService _service;

    public VeterinariosController(IVeterinarioService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<VeterinarioResponseDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] long? clinicaId)
    {
        var result = clinicaId.HasValue
            ? await _service.GetByClinicaAsync(clinicaId.Value)
            : await _service.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(VeterinarioResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(VeterinarioResponseDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] VeterinarioCreateDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(VeterinarioResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(long id, [FromBody] VeterinarioUpdateDto dto)
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
