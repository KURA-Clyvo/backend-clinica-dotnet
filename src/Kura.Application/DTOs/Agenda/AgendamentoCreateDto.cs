namespace Kura.Application.DTOs.Agenda;

/// <summary>
/// REC-10 — <c>POST /api/v1/agendamentos</c>. <c>IdClinica</c> NUNCA aparece neste DTO de
/// propósito: a clínica do agendamento é sempre derivada do JWT (<c>IClinicaContext.IdClinica</c>
/// em <c>AgendaService.CriarAsync</c>), nunca do corpo — mesma decisão que a TASK-74 já tomou
/// no lado Java para o pet (aqui vale para o agendamento inteiro).
/// </summary>
public sealed class AgendamentoCreateDto
{
    public long IdTutor { get; init; }
    public long IdPet { get; init; }
    public long IdVeterinario { get; init; }

    /// <summary>Hora LOCAL de São Paulo (F-4/A-5) — não UTC, sem <c>Z</c>.</summary>
    public DateTime DtAgendamento { get; init; }

    /// <summary>Minutos, opcional — default 30 aplicado em <c>AgendaService.CriarAsync</c>
    /// quando ausente (mesmo default do Java, <c>Agendamento.criar().comDuracao</c>).</summary>
    public int? Duracao { get; init; }

    public string DsTipo { get; init; } = string.Empty;
    public string? DsObservacoes { get; init; }

    /// <summary>
    /// FK opcional para <c>TRIAGEM_LUNA.ID_TRIAGEM</c> (F-3) — quando presente, a origem
    /// gravada é <c>TRIAGEM_LUNA</c>; ausente, <c>RECEPCAO</c>. A triagem referenciada tem de
    /// ser da MESMA clínica do JWT e do MESMO tutor (<c>IdTutor</c> acima) — G2/m-4.
    /// </summary>
    public long? IdTriagemOrigem { get; init; }
}
