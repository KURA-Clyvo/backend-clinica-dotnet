namespace Kura.Application.DTOs.Luna;

/// <summary>
/// Item de GET /api/v1/luna/triagens (LU-08). JWT de clínica, sem [JsonPropertyName] —
/// serializa camelCase (a política default deste projeto para tudo que não espelha o
/// Pydantic da Luna, diferente dos outros DTOs deste diretório).
///
/// Telefone do tutor NÃO entra aqui de propósito (brief LU-08): a tela de resposta
/// busca o tutor por idTutor (LU-09), não pelo trecho da fila.
/// </summary>
public sealed class TriagemListaItemDto
{
    public long IdTriagem { get; init; }
    public DateTime DtTriagem { get; init; }
    public string Urgencia { get; init; } = string.Empty;
    public List<string> Sintomas { get; init; } = [];
    public int? Score { get; init; }
    public string? RegrasVersao { get; init; }
    public bool EncaminhadoVet { get; init; }
    public TutorTriagemDto? Tutor { get; init; }
    public List<PetTriagemDto> Pets { get; init; } = [];

    /// <summary>Primeiros 280 caracteres de INTERACAO_CANAL.DS_CONTEUDO, via o join
    /// escopado por clínica — null quando a triagem não tem interação associada ou o
    /// join não encontra correspondência NA MESMA clínica.</summary>
    public string? TrechoMensagem { get; init; }
}

public sealed class TutorTriagemDto
{
    public long Id { get; init; }
    public string Nome { get; init; } = string.Empty;
}

public sealed class PetTriagemDto
{
    public long Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string Especie { get; init; } = string.Empty;
}
