namespace Kura.Application.DTOs.Luna;

using System.Text.Json.Serialization;

/// <summary>
/// Corpo de POST /api/v1/luna/triage — espelha TriageRequestDTO em
/// kura-luna-ai/luna/src/integration/dtos.py (Pydantic, snake_case).
///
/// TASK-67 (histórico): sintomas[]/nr_score/ds_recomendacao não tinham coluna
/// própria em TRIAGEM_LUNA (V9__schema_drift_clinico.sql) — LunaService compunha
/// esses 3 campos dentro de DS_DESCRICAO (VARCHAR2(2000)) em vez de pedir uma
/// migration nova. Ver decisão 2 no relatório da TASK-67.
///
/// LU-08 (V21, backend-tutor-java, em paralelo): NR_SCORE e um recorte de
/// sintomas[] (DS_SINTOMAS, texto delimitado por ';') ganharam coluna própria.
/// DS_DESCRICAO continua sendo composta do mesmo jeito (ComporDescricao) — a
/// coluna estruturada é *além*, não *em vez de*; nenhum consumidor antigo de
/// DS_DESCRICAO quebra.
/// </summary>
public sealed class TriageRequestDto
{
    [JsonPropertyName("id_interacao")]
    public long IdInteracao { get; init; }

    [JsonPropertyName("id_tutor")]
    public long IdTutor { get; init; }

    [JsonPropertyName("sintomas")]
    public List<string> Sintomas { get; init; } = [];

    [JsonPropertyName("ds_urgencia")]
    public string DsUrgencia { get; init; } = string.Empty;

    [JsonPropertyName("nr_score")]
    public int NrScore { get; init; }

    [JsonPropertyName("ds_recomendacao")]
    public string DsRecomendacao { get; init; } = string.Empty;

    /// <summary>
    /// LU-08/LU-07: versão do motor de regras da Luna (ex.: "1.1"). OPCIONAL e
    /// retrocompatível de propósito — um payload sem este campo continua
    /// desserializando (default null) e continua devolvendo 201; contrato definido
    /// no LU-07 (motor de regras v1.1), em paralelo.
    /// </summary>
    [JsonPropertyName("regras_versao")]
    public string? DsRegrasVersao { get; init; }
}
