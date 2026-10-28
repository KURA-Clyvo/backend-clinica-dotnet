namespace Kura.Infrastructure.Mongo;

using Kura.Domain.Exceptions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

/// <summary>
/// Cliente Mongo singleton e <b>lazy</b>: nada conecta na construção e, sem connection
/// string, a API sobe normalmente (testes e dev). O driver é thread-safe e reaproveita
/// conexões; por isso um único <see cref="MongoClient"/> por processo.
/// </summary>
public sealed class MongoContext
{
    public const string ColecaoLaudos = "laudos";

    private readonly MongoOptions _options;
    private readonly Lazy<IMongoDatabase> _database;

    public MongoContext(IOptions<MongoOptions> options)
    {
        _options = options.Value;
        _database = new Lazy<IMongoDatabase>(Criar, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool Configurado => _options.Configurado;
    public MongoOptions Opcoes => _options;

    /// <summary>Database; lança <see cref="ArmazenamentoDocumentosIndisponivelException"/> se não configurado.</summary>
    public IMongoDatabase Database
    {
        get
        {
            if (!Configurado)
                throw new ArmazenamentoDocumentosIndisponivelException("MongoDB não configurado.");
            return _database.Value;
        }
    }

    public IMongoCollection<BsonDocument> LaudosBruto => Database.GetCollection<BsonDocument>(ColecaoLaudos);

    /// <summary>Comando <c>ping</c>; qualquer falha vira <see cref="ArmazenamentoDocumentosIndisponivelException"/> sem URI/credencial.</summary>
    public async Task PingAsync(CancellationToken ct)
    {
        try
        {
            await Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);
        }
        catch (ArmazenamentoDocumentosIndisponivelException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ArmazenamentoDocumentosIndisponivelException(
                "MongoDB não respondeu: " + ex.GetType().Name, ex);
        }
    }

    private IMongoDatabase Criar()
    {
        var settings = MongoClientSettings.FromConnectionString(_options.ConnectionString);
        var teto = _options.TimeoutSelecao;
        if (settings.ServerSelectionTimeout > teto) settings.ServerSelectionTimeout = teto;
        if (settings.ConnectTimeout > teto) settings.ConnectTimeout = teto;
        return new MongoClient(settings).GetDatabase(_options.Database);
    }
}
