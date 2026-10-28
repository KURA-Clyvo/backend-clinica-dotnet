namespace Kura.Domain.Laudos;

/// <summary>
/// Laudo (metadados) guardado no MongoDB, coleção <c>laudos</c> (contrato v1 do G0 §5 da
/// S4-NET-01). O binário vive no <c>IArmazenamentoArquivos</c>; aqui só a chave e a
/// integridade (<c>sha256</c>). Sem dependência de Mongo no Domain: o mapeamento BSON mora
/// em <c>Kura.Infrastructure.Mongo.LaudoDocument</c>.
/// </summary>
public sealed class Laudo
{
    /// <summary>ObjectId em hexadecimal; vazio antes do insert.</summary>
    public string Id { get; set; } = string.Empty;
    public long IdClinica { get; set; }
    public long IdPet { get; set; }
    public long? IdEventoClinico { get; set; }
    public long IdUsuarioAutor { get; set; }
    public TipoLaudo Tipo { get; set; }
    public string Titulo { get; set; } = string.Empty;
    /// <summary>UTC.</summary>
    public DateTime DataExame { get; set; }
    public string? Laboratorio { get; set; }
    public string? Observacao { get; set; }
    /// <summary>Campos variáveis por tipo; pode ser vazio, nunca nulo.</summary>
    public Dictionary<string, object?> Dados { get; set; } = new();
    public ArquivoLaudo Arquivo { get; set; } = new();
    public bool StAtivo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
    public DateTime? RemovidoEm { get; set; }
}

/// <summary>Referência ao binário do laudo no armazenamento de arquivos.</summary>
public sealed class ArquivoLaudo
{
    public string Chave { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long TamanhoBytes { get; set; }
    /// <summary>64 caracteres hexadecimais minúsculos.</summary>
    public string Sha256 { get; set; } = string.Empty;
}
