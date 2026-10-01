namespace Kura.Application.DTOs.Luna;

using System.Text.Json.Serialization;

/// <summary>
/// REC-15 — item de GET /api/v1/luna/agendamentos/confirmacao-pendente. Um por
/// agendamento AGENDADO, do dia pedido, cujo tutor tem DS_WHATSAPP preenchido,
/// consente lembrete (CONSENTIMENTO, DS_TIPO='LEMBRETES' — ver LunaService, não existe
/// coluna ST_CONSENTE_LEMBRETE em TUTOR) e ainda não recebeu lembrete
/// (DT_LEMBRETE_CONFIRMACAO nulo). Shape snake_case, espelhando o padrão dos outros
/// DTOs deste diretório (contrato consumido pela Luna, Python/Pydantic).
/// </summary>
public sealed class ConfirmacaoPendenteItemDto
{
    [JsonPropertyName("id_agendamento")]
    public long IdAgendamento { get; init; }

    [JsonPropertyName("id_clinica")]
    public long IdClinica { get; init; }

    [JsonPropertyName("id_tutor")]
    public long IdTutor { get; init; }

    [JsonPropertyName("ds_whatsapp")]
    public string DsWhatsapp { get; init; } = string.Empty;

    [JsonPropertyName("nm_tutor")]
    public string NmTutor { get; init; } = string.Empty;

    [JsonPropertyName("nm_pet")]
    public string? NmPet { get; init; }

    [JsonPropertyName("dt_agendamento")]
    public DateTime DtAgendamento { get; init; }

    [JsonPropertyName("ds_servico")]
    public string? DsServico { get; init; }
}
