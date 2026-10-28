namespace Kura.Infrastructure.Tests;

using Kura.Domain.Laudos;
using Kura.Infrastructure.Mongo;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mongo2Go;

/// <summary>
/// S4-NET-02: um mongod real EMBARCADO (Mongo2Go 5.0.0, mongod 8.0.26) compartilhado pela
/// coleção de testes. Texto permitido: "testado contra mongod 8.0 embarcado". NÃO é o Mongo
/// 7.0 de produção (isso é do G4). Cada teste usa um database próprio (nome único).
/// </summary>
public sealed class MongoFixture : IDisposable
{
    private readonly MongoDbRunner _runner;

    public MongoFixture()
    {
        _runner = MongoDbRunner.Start();
    }

    public string ConnectionString => _runner.ConnectionString;

    public MongoContext NovoContexto(bool initEnabled = true)
    {
        var opcoes = new MongoOptions
        {
            ConnectionString = ConnectionString,
            Database = "t_" + Guid.NewGuid().ToString("N"),
            InitEnabled = initEnabled,
        };
        return new MongoContext(Options.Create(opcoes));
    }

    /// <summary>Contexto com schema/índices já aplicados + repositório.</summary>
    public async Task<(MongoContext Ctx, LaudoRepository Repo)> NovoRepositorioAsync()
    {
        var ctx = NovoContexto();
        await new MongoSchemaRunner(ctx, NullLogger<MongoSchemaRunner>.Instance).AplicarAsync(CancellationToken.None);
        return (ctx, new LaudoRepository(ctx, TimeProvider.System));
    }

    public static Laudo NovoLaudo(
        long idClinica = 1,
        long idPet = 10,
        TipoLaudo tipo = TipoLaudo.HEMOGRAMA,
        string titulo = "Hemograma completo",
        DateTime? dataExame = null) => new()
        {
            IdClinica = idClinica,
            IdPet = idPet,
            IdUsuarioAutor = 7,
            Tipo = tipo,
            Titulo = titulo,
            DataExame = dataExame ?? new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            Laboratorio = "Lab Teste",
            Observacao = "obs",
            Dados = new Dictionary<string, object?> { ["hematocrito"] = 42.5, ["unidade"] = "%" },
            Arquivo = new ArquivoLaudo
            {
                Chave = $"clinica/{idClinica}/pet/{idPet}/laudo/{Guid.NewGuid():N}.pdf",
                ContentType = "application/pdf",
                TamanhoBytes = 1234,
                Sha256 = new string('a', 64),
            },
        };

    public void Dispose() => _runner.Dispose();
}

[CollectionDefinition(Nome)]
public sealed class MongoCollection : ICollectionFixture<MongoFixture>
{
    public const string Nome = "MongoEmbarcado";
}
