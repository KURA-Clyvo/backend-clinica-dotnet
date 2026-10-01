namespace Kura.Application.DTOs.Luna;

using System.Text.Json.Serialization;

/// <summary>REC-15 — resposta de POST .../resposta-confirmacao.</summary>
public sealed class RespostaConfirmacaoResponseDto
{
    [JsonPropertyName("id_agendamento")]
    public long IdAgendamento { get; init; }

    [JsonPropertyName("ds_status")]
    public string DsStatus { get; init; } = string.Empty;

    [JsonPropertyName("ds_resposta_confirmacao")]
    public string? DsRespostaConfirmacao { get; init; }
}
