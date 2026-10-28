namespace Kura.Application.DTOs.Laudo;

using Kura.Domain.Laudos;

/// <summary>
/// Entrada de <c>POST /api/v1/pets/{idPet}/laudos</c>. Os campos chegam como TEXTO (é multipart):
/// o validador diz se são interpretáveis e <see cref="Kura.Application.Laudos.LaudoEntrada"/> faz a
/// interpretação, com a mesma regra nos dois lugares. O <see cref="Arquivo"/> é
/// <see cref="Stream"/> puro (Application não referencia ASP.NET); quem o abre e fecha é o controller.
/// </summary>
public sealed class LaudoCriarDto
{
    public string? Tipo { get; init; }
    public string? Titulo { get; init; }
    public string? DataExame { get; init; }
    public string? Laboratorio { get; init; }
    public string? Observacao { get; init; }
    public string? IdEventoClinico { get; init; }
    /// <summary>Objeto JSON serializado (campos variáveis por tipo). Opcional; vazio vira <c>{}</c>.</summary>
    public string? Dados { get; init; }
    public Stream? Arquivo { get; init; }
}

/// <summary>Filtros e ordenação da lista (já interpretados pela camada Api).</summary>
public sealed record LaudoListarDto(
    int Page,
    int PageSize,
    OrdenacaoLaudo Ordem,
    TipoLaudo? Tipo,
    DateTime? De,
    DateTime? Ate);

public sealed class LaudoArquivoResponseDto
{
    public string ContentType { get; init; } = string.Empty;
    public long TamanhoBytes { get; init; }
    public string Sha256 { get; init; } = string.Empty;
}

/// <summary>
/// Laudo sem links (os <c>_links</c> são responsabilidade da camada Api). NÃO expõe a chave de
/// storage nem o <c>idClinica</c>: o binário se baixa por <c>/laudos/{id}/arquivo</c>.
/// </summary>
public class LaudoResponseDto
{
    public string Id { get; init; } = string.Empty;
    public long IdPet { get; init; }
    public long? IdEventoClinico { get; init; }
    public long IdUsuarioAutor { get; init; }
    public string Tipo { get; init; } = string.Empty;
    public string Titulo { get; init; } = string.Empty;
    public DateTime DataExame { get; init; }
    public string? Laboratorio { get; init; }
    public string? Observacao { get; init; }
    public Dictionary<string, object?> Dados { get; init; } = new();
    public LaudoArquivoResponseDto Arquivo { get; init; } = new();
    public DateTime CriadoEm { get; init; }
    public DateTime AtualizadoEm { get; init; }
}

/// <summary>Conteúdo binário de um laudo para download. Quem recebe descarta o stream.</summary>
public sealed class LaudoArquivoDownload
{
    public LaudoArquivoDownload(Stream conteudo, string contentType, string extensao)
    {
        Conteudo = conteudo;
        ContentType = contentType;
        Extensao = extensao;
    }

    public Stream Conteudo { get; }
    public string ContentType { get; }
    public string Extensao { get; }
}
