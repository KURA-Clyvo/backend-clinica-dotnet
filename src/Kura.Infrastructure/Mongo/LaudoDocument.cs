namespace Kura.Infrastructure.Mongo;

using Kura.Domain.Laudos;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// Mapeamento BSON do contrato <c>laudos</c> v1 (G0 §5), separado da entidade de domínio.
/// Nomes camelCase explícitos; <c>long</c> sai como Int64; datas em UTC; sem campos extras
/// (o <c>$jsonSchema</c> tem <c>additionalProperties:false</c>).
/// </summary>
internal sealed class LaudoDocument
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("idClinica")] public long IdClinica { get; set; }
    [BsonElement("idPet")] public long IdPet { get; set; }
    [BsonElement("idEventoClinico")] public long? IdEventoClinico { get; set; }
    [BsonElement("idUsuarioAutor")] public long IdUsuarioAutor { get; set; }
    [BsonElement("tipo"), BsonRepresentation(BsonType.String)] public TipoLaudo Tipo { get; set; }
    [BsonElement("titulo")] public string Titulo { get; set; } = string.Empty;
    [BsonElement("dataExame"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime DataExame { get; set; }
    [BsonElement("laboratorio")] public string? Laboratorio { get; set; }
    [BsonElement("observacao")] public string? Observacao { get; set; }
    [BsonElement("dados")] public BsonDocument Dados { get; set; } = new();
    [BsonElement("arquivo")] public ArquivoDocument Arquivo { get; set; } = new();
    [BsonElement("stAtivo")] public bool StAtivo { get; set; } = true;
    [BsonElement("criadoEm"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime CriadoEm { get; set; }
    [BsonElement("atualizadoEm"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime AtualizadoEm { get; set; }
    [BsonElement("removidoEm"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime? RemovidoEm { get; set; }

    /// <summary>
    /// Converte para UTC de verdade: <c>Local</c> => <c>ToUniversalTime()</c>; <c>Utc</c> fica como está;
    /// <c>Unspecified</c> é tratado como UTC (decisão: a borda HTTP da S4-NET-03 entrega
    /// <c>DateTimeOffset</c>/UTC, então um instante sem Kind já está em UTC).
    /// </summary>
    internal static DateTime ParaUtc(DateTime d, TimeZoneInfo? fusoLocal = null) => d.Kind switch
    {
        // fusoLocal só existe para teste fixar o fuso; em produção é o fuso do processo.
        DateTimeKind.Local when fusoLocal is not null =>
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(d, DateTimeKind.Unspecified), fusoLocal),
        DateTimeKind.Local => d.ToUniversalTime(),
        DateTimeKind.Utc => d,
        _ => DateTime.SpecifyKind(d, DateTimeKind.Utc),
    };

    public static LaudoDocument De(Laudo l) => new()
    {
        Id = l.Id,
        IdClinica = l.IdClinica,
        IdPet = l.IdPet,
        IdEventoClinico = l.IdEventoClinico,
        IdUsuarioAutor = l.IdUsuarioAutor,
        Tipo = l.Tipo,
        Titulo = l.Titulo,
        DataExame = ParaUtc(l.DataExame),
        Laboratorio = l.Laboratorio,
        Observacao = l.Observacao,
        Dados = new BsonDocument(l.Dados.ToDictionary(k => k.Key, k => BsonTypeMapper.MapToBsonValue(k.Value))),
        Arquivo = new ArquivoDocument
        {
            Chave = l.Arquivo.Chave,
            ContentType = l.Arquivo.ContentType,
            TamanhoBytes = l.Arquivo.TamanhoBytes,
            Sha256 = l.Arquivo.Sha256,
        },
        StAtivo = l.StAtivo,
        CriadoEm = ParaUtc(l.CriadoEm),
        AtualizadoEm = ParaUtc(l.AtualizadoEm),
        RemovidoEm = l.RemovidoEm is null ? null : ParaUtc(l.RemovidoEm.Value),
    };

    public Laudo ParaDominio() => new()
    {
        Id = Id,
        IdClinica = IdClinica,
        IdPet = IdPet,
        IdEventoClinico = IdEventoClinico,
        IdUsuarioAutor = IdUsuarioAutor,
        Tipo = Tipo,
        Titulo = Titulo,
        DataExame = DataExame,
        Laboratorio = Laboratorio,
        Observacao = Observacao,
        Dados = Dados.ToDictionary().ToDictionary(k => k.Key, k => (object?)k.Value),
        Arquivo = new ArquivoLaudo
        {
            Chave = Arquivo.Chave,
            ContentType = Arquivo.ContentType,
            TamanhoBytes = Arquivo.TamanhoBytes,
            Sha256 = Arquivo.Sha256,
        },
        StAtivo = StAtivo,
        CriadoEm = CriadoEm,
        AtualizadoEm = AtualizadoEm,
        RemovidoEm = RemovidoEm,
    };
}

internal sealed class ArquivoDocument
{
    [BsonElement("chave")] public string Chave { get; set; } = string.Empty;
    [BsonElement("contentType")] public string ContentType { get; set; } = string.Empty;
    [BsonElement("tamanhoBytes")] public long TamanhoBytes { get; set; }
    [BsonElement("sha256")] public string Sha256 { get; set; } = string.Empty;
}
