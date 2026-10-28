namespace Kura.Infrastructure.Mongo;

using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Domain.Laudos;
using MongoDB.Bson;
using MongoDB.Driver;

/// <summary>
/// Repositório concreto de laudos. O filtro SEMPRE começa por <c>idClinica</c> (e, na lista,
/// <c>idPet</c>) e por <c>stAtivo=true</c>: não existe caminho de leitura sem escopo, e não
/// existe <c>DeleteOne</c> (exclusão é lógica).
/// </summary>
public sealed class LaudoRepository : ILaudoRepository
{
    private static readonly FilterDefinitionBuilder<LaudoDocument> F = Builders<LaudoDocument>.Filter;
    private static readonly SortDefinitionBuilder<LaudoDocument> S = Builders<LaudoDocument>.Sort;

    private readonly MongoContext _ctx;
    private readonly TimeProvider _time;

    public LaudoRepository(MongoContext ctx, TimeProvider time)
    {
        _ctx = ctx;
        _time = time;
    }

    private IMongoCollection<LaudoDocument> Colecao =>
        _ctx.Database.GetCollection<LaudoDocument>(MongoContext.ColecaoLaudos);

    public async Task<Laudo?> ObterAsync(long idClinica, string id, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _)) return null;
        var filtro = F.And(F.Eq(d => d.IdClinica, idClinica), F.Eq(d => d.Id, id), F.Eq(d => d.StAtivo, true));
        var col = Colecao;
        var doc = await Executar(() => col.Find(filtro).FirstOrDefaultAsync(ct));
        return doc?.ParaDominio();
    }

    public async Task<PaginaLaudos> ListarPorPetAsync(
        long idClinica, long idPet, ConsultaLaudos consulta, CancellationToken ct = default)
    {
        var pagina = Math.Max(1, consulta.Pagina);
        var tamanho = Math.Clamp(consulta.TamanhoPagina, 1, 100);

        var filtros = new List<FilterDefinition<LaudoDocument>>
        {
            F.Eq(d => d.IdClinica, idClinica),
            F.Eq(d => d.IdPet, idPet),
            F.Eq(d => d.StAtivo, true),
        };
        if (consulta.Tipo is { } tipo) filtros.Add(F.Eq(d => d.Tipo, tipo));
        if (consulta.DataDe is { } de) filtros.Add(F.Gte(d => d.DataExame, DateTime.SpecifyKind(de, DateTimeKind.Utc)));
        if (consulta.DataAte is { } ate) filtros.Add(F.Lte(d => d.DataExame, DateTime.SpecifyKind(ate, DateTimeKind.Utc)));
        var filtro = F.And(filtros);

        var ordem = OrdenarPor(consulta.Ordem);
        var col = Colecao;
        var total = await Executar(() => col.CountDocumentsAsync(filtro, cancellationToken: ct));
        var docs = await Executar(() => col.Find(filtro)
            .Sort(ordem)
            .Skip((pagina - 1) * tamanho)
            .Limit(tamanho)
            .ToListAsync(ct));

        return new PaginaLaudos(docs.Select(d => d.ParaDominio()).ToList(), pagina, tamanho, total);
    }

    public async Task<Laudo> InserirAsync(long idClinica, Laudo laudo, CancellationToken ct = default)
    {
        if (laudo.IdClinica != idClinica)
            throw new ArgumentException("laudo.IdClinica difere do idClinica informado.", nameof(laudo));

        var agora = _time.GetUtcNow().UtcDateTime;
        laudo.Id = ObjectId.GenerateNewId().ToString();
        laudo.StAtivo = true;
        laudo.RemovidoEm = null;
        laudo.CriadoEm = agora;
        laudo.AtualizadoEm = agora;

        var col = Colecao;
        await Executar(async () =>
        {
            await col.InsertOneAsync(LaudoDocument.De(laudo), cancellationToken: ct);
            return 0;
        });
        return laudo;
    }

    public async Task<bool> ExcluirLogicoAsync(long idClinica, string id, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _)) return false;
        var agora = _time.GetUtcNow().UtcDateTime;
        var filtro = F.And(F.Eq(d => d.IdClinica, idClinica), F.Eq(d => d.Id, id), F.Eq(d => d.StAtivo, true));
        var update = Builders<LaudoDocument>.Update
            .Set(d => d.StAtivo, false)
            .Set(d => d.RemovidoEm, agora)
            .Set(d => d.AtualizadoEm, agora);
        var col = Colecao;
        var r = await Executar(() => col.UpdateOneAsync(filtro, update, cancellationToken: ct));
        return r.MatchedCount > 0;
    }

    /// <summary>Lista branca: enum fechado para expressão; desempate sempre por _id na mesma direção.</summary>
    private static SortDefinition<LaudoDocument> OrdenarPor(OrdenacaoLaudo ordem) => ordem switch
    {
        OrdenacaoLaudo.DataExameAsc => S.Ascending(d => d.DataExame).Ascending(d => d.Id),
        OrdenacaoLaudo.CriadoEmDesc => S.Descending(d => d.CriadoEm).Descending(d => d.Id),
        OrdenacaoLaudo.CriadoEmAsc => S.Ascending(d => d.CriadoEm).Ascending(d => d.Id),
        OrdenacaoLaudo.TituloAsc => S.Ascending(d => d.Titulo).Ascending(d => d.Id),
        OrdenacaoLaudo.TituloDesc => S.Descending(d => d.Titulo).Descending(d => d.Id),
        _ => S.Descending(d => d.DataExame).Descending(d => d.Id),
    };

    private static async Task<T> Executar<T>(Func<Task<T>> op)
    {
        try
        {
            return await op();
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException or System.Net.Sockets.SocketException)
        {
            throw new ArmazenamentoDocumentosIndisponivelException(
                "Armazenamento de documentos indisponível: " + ex.GetType().Name, ex);
        }
    }
}
