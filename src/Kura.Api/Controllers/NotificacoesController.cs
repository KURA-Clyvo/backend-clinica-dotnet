namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Notificacao;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/notificacoes")]
public class NotificacoesController : ControllerBase
{
    private readonly INotificacaoService _service;
    private readonly IClinicaContext _clinicaContext;

    public NotificacoesController(INotificacaoService service, IClinicaContext clinicaContext)
    {
        _service = service;
        _clinicaContext = clinicaContext;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<NotificacaoResponseDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] bool? apenasNaoLidas)
    {
        var result = await _service.GetAllByClinicaAsync(_clinicaContext.IdClinica, apenasNaoLidas);
        return Ok(result);
    }

    [HttpPatch("{id:long}/marcar-lida")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> MarcarLida(long id)
    {
        await _service.MarcarLidaAsync(id);
        return NoContent();
    }
}
