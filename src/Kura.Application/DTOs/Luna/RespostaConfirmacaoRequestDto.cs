namespace Kura.Application.DTOs.Luna;

using System.Text.Json.Serialization;

/// <summary>
/// REC-15 — corpo de POST /api/v1/luna/agendamentos/{id}/resposta-confirmacao. A Luna
/// já resolveu o tutor pelo telefone na entrada (A-10/a, fora do escopo deste
/// endpoint) — aqui o servidor só confere que id_tutor É o tutor DAQUELE agendamento
/// (G0 item 11). resposta ∈ SIM | CANCELAR | REMARCAR (mesmo CHECK de
/// DS_RESPOSTA_CONFIRMACAO na V23).
/// </summary>
public sealed class RespostaConfirmacaoRequestDto
{
    [JsonPropertyName("id_tutor")]
    public long IdTutor { get; init; }

    [JsonPropertyName("resposta")]
    public string Resposta { get; init; } = string.Empty;
}
