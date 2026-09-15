namespace Kura.Api.Controllers;

using Kura.Api.Filters;
using Kura.Application.DTOs.Common;
using Kura.Application.DTOs.Luna;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Endpoints da IA Luna — triagem automática de pets via chatbot Python.
/// Relatório analítico acessível via JWT (uso clínico); os endpoints de escrita
/// (interactions/triage) são chamados pela Luna servidor-a-servidor, autenticados por
/// API Key (ver <see cref="LunaApiKeyAuthFilter"/>), nunca JWT de clínica.
/// </summary>
[ApiController]
[Route("api/v1/luna")]
public class LunaController(ILunaService lunaService) : ControllerBase
{
    /// <summary>
    /// Gera relatório agregado de triagens realizadas pela Luna em um intervalo de datas.
    /// Inclui total de triagens, distribuição de urgência e taxa de encaminhamento ao veterinário.
    /// </summary>
    /// <param name="dataInicio">Data inicial do relatório (inclusive).</param>
    /// <param name="dataFim">Data final do relatório (inclusive).</param>
    /// <returns>Relatório analítico de triagens no período.</returns>
    /// <response code="200">Relatório gerado com sucesso.</response>
    /// <response code="400">Intervalo de datas inválido.</response>
    [HttpGet("triagens/relatorio")]
    [Authorize]
    [ProducesResponseType(typeof(RelatorioTriagensDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> GerarRelatorio(
        [FromQuery] DateTime dataInicio,
        [FromQuery] DateTime dataFim)
    {
        var result = await lunaService.GerarRelatorioAsync(dataInicio, dataFim);
        return Ok(result);
    }

    /// <summary>
    /// TASK-67: registra uma interação de canal (WhatsApp/e-mail/SMS) recebida ou
    /// enviada pela IA Luna. Deriva ID_CLINICA a partir do tutor quando id_tutor é
    /// informado. Desde a TASK-77 (FIX_7, decisão de produto): id_tutor ausente
    /// (telefone não cadastrado) NÃO é mais rejeitado — a interação é registrada mesmo
    /// assim, com id_clinica nulo (ver LunaService.RegistrarInteracaoAsync para a
    /// decisão completa e a consequência aceita sobre visibilidade por clínica).
    /// </summary>
    /// <param name="dto">Dados da interação (shape espelha InteractionRequestDTO, Pydantic).</param>
    /// <returns>ID da interação registrada.</returns>
    /// <response code="201">Interação registrada com sucesso (com ou sem tutor identificado).</response>
    /// <response code="400">Payload malformado (ds_canal/ds_direcao fora do enum, ds_conteudo vazio).</response>
    /// <response code="404">id_tutor informado (quando presente) não corresponde a um tutor existente.</response>
    [HttpPost("interactions")]
    [AllowAnonymous]
    [ServiceFilter(typeof(LunaApiKeyAuthFilter))]
    [ProducesResponseType(typeof(InteractionResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> RegistrarInteracao([FromBody] InteractionRequestDto dto)
    {
        var result = await lunaService.RegistrarInteracaoAsync(dto);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// TASK-67: registra o resultado de uma triagem de IA, ligada à interação que a
    /// originou (id_interacao).
    /// </summary>
    /// <param name="dto">Dados da triagem (shape espelha TriageRequestDTO, Pydantic).</param>
    /// <returns>ID da triagem registrada.</returns>
    /// <response code="201">Triagem registrada com sucesso.</response>
    /// <response code="400">Payload malformado (ds_urgencia fora do enum, campos obrigatórios ausentes).</response>
    /// <response code="404">id_interacao ou id_tutor informados não existem.</response>
    /// <response code="422">id_interacao não pertence à clínica do tutor informado (id_tutor) — ver LunaService.RegistrarTriagemAsync.</response>
    [HttpPost("triage")]
    [AllowAnonymous]
    [ServiceFilter(typeof(LunaApiKeyAuthFilter))]
    [ProducesResponseType(typeof(TriageResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> RegistrarTriagem([FromBody] TriageRequestDto dto)
    {
        var result = await lunaService.RegistrarTriagemAsync(dto);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// LU-08: fila de triagens da clínica logada (JWT — idClinica nunca vem de query
    /// string, ver LunaService.ListarTriagensAsync). Ordenação fixa: ALTA → MEDIA →
    /// BAIXA, depois mais recente. trechoMensagem vem do join com INTERACAO_CANAL,
    /// escopado explicitamente por clínica (não delegado ao query filter global) —
    /// telefone do tutor NÃO entra na resposta.
    /// </summary>
    /// <param name="urgencia">Filtra por nível de urgência (BAIXA/MEDIA/ALTA), opcional.</param>
    /// <param name="dataInicio">Início do período (opcional; se informado com dataFim, máx. 90 dias).</param>
    /// <param name="dataFim">Fim do período (opcional; se informado com dataInicio, máx. 90 dias).</param>
    /// <param name="page">Número da página (padrão: 1).</param>
    /// <param name="pageSize">Itens por página (padrão: 20, máx: 100).</param>
    /// <returns>Página de triagens da clínica logada.</returns>
    /// <response code="200">Lista retornada com sucesso.</response>
    /// <response code="422">Período informado é inválido ou excede 90 dias.</response>
    [HttpGet("triagens")]
    [Authorize]
    [ProducesResponseType(typeof(PagedResultDto<TriagemListaItemDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> ListarTriagens(
        [FromQuery] string? urgencia = null,
        [FromQuery] DateTime? dataInicio = null,
        [FromQuery] DateTime? dataFim = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await lunaService.ListarTriagensAsync(urgencia, dataInicio, dataFim, page, pageSize);
        return Ok(result);
    }
}
