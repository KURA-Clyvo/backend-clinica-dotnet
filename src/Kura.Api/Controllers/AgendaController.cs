namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Agenda;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Consulta e atualização da agenda de agendamentos (tabela AGENDAMENTO gerenciada pelo backend Java).
/// O .NET realiza leitura e atualização de status com controle de concorrência otimista (NrVersion).
/// </summary>
[ApiController]
[Route("api/v1/agenda")]
[Authorize]
public class AgendaController(IAgendaService agendaService) : ControllerBase
{
    /// <summary>
    /// Retorna os agendamentos de um intervalo de datas, com filtro opcional por veterinário.
    /// </summary>
    /// <param name="dataInicio">Data inicial do intervalo (inclusive).</param>
    /// <param name="dataFim">Data final do intervalo (inclusive, máx. 31 dias).</param>
    /// <param name="veterinarioId">Filtrar por veterinário responsável (opcional).</param>
    /// <returns>Agenda do intervalo com lista de agendamentos mapeados.</returns>
    /// <response code="200">Agenda retornada com sucesso.</response>
    /// <response code="422">Intervalo inválido ou superior a 31 dias.</response>
    [HttpGet]
    [ProducesResponseType(typeof(AgendaResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> GetAgenda(
        [FromQuery] DateTime dataInicio,
        [FromQuery] DateTime dataFim,
        [FromQuery] long? veterinarioId = null)
    {
        var result = await agendaService.GetAgendaAsync(dataInicio, dataFim, veterinarioId);
        return Ok(result);
    }

    /// <summary>
    /// Atualiza o status de um agendamento com controle de concorrência otimista.
    /// O cliente deve enviar o NrVersion obtido na última leitura para evitar sobrescrita silenciosa.
    /// </summary>
    /// <param name="id">Identificador do agendamento.</param>
    /// <param name="dto">Novo status (REALIZADO | CANCELADO | NAO_COMPARECEU | CONFIRMADO), NrVersion atual e observação opcional.</param>
    /// <returns>Agendamento com status e NrVersion atualizados.</returns>
    /// <response code="200">Status atualizado com sucesso.</response>
    /// <response code="400">Dados inválidos (status ou versão).</response>
    /// <response code="404">Agendamento não encontrado.</response>
    /// <response code="409">Conflito de concorrência — outro processo atualizou o agendamento. Atualize e tente novamente.</response>
    /// <response code="422">Agendamento já está em estado final (REALIZADO, CANCELADO ou NAO_COMPARECEU) ou a transição de status pedida não é permitida a partir do status atual — ver a máquina de estados em <c>AgendaService.TransicoesPermitidas</c>.</response>
    [HttpPatch("~/api/v1/agendamentos/{id:long}/status")]
    [ProducesResponseType(typeof(AgendamentoItemDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> AtualizarStatus(long id, [FromBody] AtualizarStatusAgendamentoDto dto)
    {
        var result = await agendaService.AtualizarStatusAsync(id, dto);
        return Ok(result);
    }

    /// <summary>
    /// REC-10 — cria um agendamento pela recepção da clínica (ou pelo card de uma triagem
    /// da Luna, F-3). <c>IdClinica</c> vem SEMPRE do JWT, nunca do corpo.
    /// </summary>
    /// <param name="dto">Dados do agendamento (tutor, pet, veterinário, data/hora local de
    /// SP, tipo, duração opcional, observações opcionais, triagem de origem opcional).</param>
    /// <returns>Agendamento criado, no mesmo shape do item de agenda (REC-09).</returns>
    /// <response code="201">Agendamento criado com sucesso.</response>
    /// <response code="400">Payload malformado (tipo fora da lista, duração fora de 5–480, etc.).</response>
    /// <response code="404">Tutor, pet, veterinário ou triagem de origem não encontrados (ou pertencem a outra clínica — mesma resposta, sem oráculo).</response>
    /// <response code="422">Pet não vinculado ao tutor; triagem de origem pertence a outro tutor da mesma clínica; ou data mais de 15 minutos no passado.</response>
    [HttpPost("~/api/v1/agendamentos")]
    [ProducesResponseType(typeof(AgendamentoItemDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> Criar([FromBody] AgendamentoCreateDto dto)
    {
        var result = await agendaService.CriarAsync(dto);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// REC-11 — registra a chegada do paciente (check-in). Só a partir de <c>AGENDADO</c> ou
    /// <c>CONFIRMADO</c>. Idempotente: uma 2ª chamada devolve o estado atual sem sobrescrever o
    /// horário nem incrementar <c>NrVersion</c>. Horário sempre do relógio da clínica (hora local
    /// de SP), nunca do cliente.
    /// </summary>
    /// <param name="id">Identificador do agendamento.</param>
    /// <param name="dto">Versão atual do agendamento (lock otimista).</param>
    /// <returns>Agendamento com <c>DtCheckin</c> e <c>DsEtapaRecepcao</c> atualizados.</returns>
    /// <response code="200">Check-in registrado (ou já existente — idempotente).</response>
    /// <response code="404">Agendamento não encontrado (ou de outra clínica — mesma resposta).</response>
    /// <response code="409">Conflito de concorrência — <c>NrVersion</c> desatualizado.</response>
    /// <response code="422">Status atual não permite check-in.</response>
    [HttpPost("~/api/v1/agendamentos/{id:long}/checkin")]
    [ProducesResponseType(typeof(AgendamentoItemDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> Checkin(long id, [FromBody] RegistrarEventoRecepcaoDto dto)
    {
        var result = await agendaService.CheckinAsync(id, dto);
        return Ok(result);
    }

    /// <summary>
    /// REC-11 — registra o início do atendimento. Permitido sem check-in prévio (walk-in que
    /// entra direto) — nunca preenche <c>DtCheckin</c> como efeito colateral. Mesmo contrato de
    /// lock/idempotência do check-in.
    /// </summary>
    /// <param name="id">Identificador do agendamento.</param>
    /// <param name="dto">Versão atual do agendamento (lock otimista).</param>
    /// <returns>Agendamento com <c>DtInicioAtendimento</c> e <c>DsEtapaRecepcao</c> atualizados.</returns>
    /// <response code="200">Início registrado (ou já existente — idempotente).</response>
    /// <response code="404">Agendamento não encontrado (ou de outra clínica — mesma resposta).</response>
    /// <response code="409">Conflito de concorrência — <c>NrVersion</c> desatualizado.</response>
    /// <response code="422">Status atual não permite iniciar atendimento.</response>
    [HttpPost("~/api/v1/agendamentos/{id:long}/inicio-atendimento")]
    [ProducesResponseType(typeof(AgendamentoItemDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> IniciarAtendimento(long id, [FromBody] RegistrarEventoRecepcaoDto dto)
    {
        var result = await agendaService.IniciarAtendimentoAsync(id, dto);
        return Ok(result);
    }
}
