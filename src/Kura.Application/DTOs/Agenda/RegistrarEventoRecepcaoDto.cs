namespace Kura.Application.DTOs.Agenda;

/// <summary>
/// REC-11 — corpo de <c>POST .../checkin</c> e <c>POST .../inicio-atendimento</c>. Só carrega
/// o lock otimista (mesmo contrato de <see cref="AtualizarStatusAgendamentoDto.NrVersion"/>) —
/// o horário nunca vem do cliente, é sempre <c>IRelogioClinica.Agora()</c> (A-5).
/// </summary>
public sealed class RegistrarEventoRecepcaoDto
{
    public long NrVersion { get; init; }
}
