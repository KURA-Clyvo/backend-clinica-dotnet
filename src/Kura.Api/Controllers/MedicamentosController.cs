namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Medicamento;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/medicamentos")]
public class MedicamentosController : ControllerBase
{
    private readonly IMedicamentoService _service;

    public MedicamentosController(IMedicamentoService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MedicamentoResponseDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] string? busca)
    {
        var result = await _service.SearchAsync(busca);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(MedicamentoResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MedicamentoResponseDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] MedicamentoCreateDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
