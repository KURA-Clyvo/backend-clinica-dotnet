namespace Kura.Infrastructure.Mongo;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

/// <summary>
/// Aplica o <c>$jsonSchema</c> (validationLevel strict / validationAction error) e os índices
/// da coleção <c>laudos</c> na partida. Idempotente: cria se não existe, <c>collMod</c> se
/// existe; índice igual não recria. <b>Captura toda exceção</b> (1 WARN sem URI/credencial) e
/// nunca derruba o host — lição da S3D-10. Não roda com <c>InitEnabled=false</c> nem sem
/// connection string.
/// </summary>
public sealed class MongoSchemaRunner : IHostedService
{
    private readonly MongoContext _ctx;
    private readonly ILogger<MongoSchemaRunner> _logger;

    public MongoSchemaRunner(MongoContext ctx, ILogger<MongoSchemaRunner> logger)
    {
        _ctx = ctx;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_ctx.Opcoes.InitEnabled || !_ctx.Configurado) return;
        try
        {
            await AplicarAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Só o tipo da exceção: a mensagem do driver pode carregar endpoint/URI.
            _logger.LogWarning(
                "Mongo: schema/índices de 'laudos' não aplicados ({TipoExcecao}). A API segue no ar; laudos ficam indisponíveis até o Mongo responder.",
                ex.GetType().Name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task AplicarAsync(CancellationToken ct)
    {
        var db = _ctx.Database;
        var schema = BsonDocument.Parse(LerRecurso("laudos.schema.json"));
        var nomes = await db.ListCollectionNamesAsync(
            new ListCollectionNamesOptions { Filter = new BsonDocument("name", MongoContext.ColecaoLaudos) },
            ct);
        var existe = await nomes.AnyAsync(ct);

        if (!existe)
        {
            await db.CreateCollectionAsync(MongoContext.ColecaoLaudos, new CreateCollectionOptions<BsonDocument>
            {
                Validator = new BsonDocumentFilterDefinition<BsonDocument>(new BsonDocument("$jsonSchema", schema)),
                ValidationLevel = DocumentValidationLevel.Strict,
                ValidationAction = DocumentValidationAction.Error,
            }, ct);
        }
        else
        {
            await db.RunCommandAsync<BsonDocument>(new BsonDocument
            {
                { "collMod", MongoContext.ColecaoLaudos },
                { "validator", new BsonDocument("$jsonSchema", schema) },
                { "validationLevel", "strict" },
                { "validationAction", "error" },
            }, cancellationToken: ct);
        }

        var indices = BsonDocument.Parse("{\"v\":" + LerRecurso("laudos.indexes.json") + "}")["v"].AsBsonArray;
        var modelos = indices
            .Select(v => v.AsBsonDocument)
            .Select(d => new CreateIndexModel<BsonDocument>(
                new BsonDocumentIndexKeysDefinition<BsonDocument>(d["keys"].AsBsonDocument),
                new CreateIndexOptions { Name = d["name"].AsString }))
            .ToList();
        await _ctx.LaudosBruto.Indexes.CreateManyAsync(modelos, ct);
    }

    private static string LerRecurso(string nome)
    {
        var asm = typeof(MongoSchemaRunner).Assembly;
        var recurso = asm.GetManifestResourceNames().Single(n => n.EndsWith(nome, StringComparison.Ordinal));
        using var s = asm.GetManifestResourceStream(recurso)!;
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
