namespace Kura.Application.DTOs.Luna;

using System.Text.Json.Serialization;

/// <summary>
/// REC-15 — resposta de POST /api/v1/luna/agendamentos/{id}/lembrete-enviado.
/// dt_lembrete_confirmacao é sempre a gravada (primeira chamada) ou a JÁ gravada
/// (chamada repetida, idempotente — ver LunaService.RegistrarLembreteEnviadoAsync).
/// </summary>
public sealed class LembreteEnviadoResponseDto
{
    [JsonPropertyName("id_agendamento")]
    public long IdAgendamento { get; init; }

    [JsonPropertyName("dt_lembrete_confirmacao")]
    public DateTime DtLembreteConfirmacao { get; init; }
}
