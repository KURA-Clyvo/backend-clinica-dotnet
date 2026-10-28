namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Infrastructure.Mongo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

/// <summary>
/// S4-NET-02: runner de schema/índices contra mongod 8.0 embarcado. O <c>$jsonSchema</c> é
/// provado escrevendo DIRETO pela coleção (sem passar pelo repositório), com controle
/// positivo: o documento válido entra, os inválidos são recusados.
/// </summary>
[Collection(MongoCollection.Nome)]
public class MongoSchemaRunnerTests(MongoFixture mongo)
{
    private static BsonDocument DocumentoValido() => new()
    {
        { "idClinica", 1L }, { "idPet", 10L }, { "idEventoClinico", BsonNull.Value }, { "idUsuarioAutor", 7L },
        { "tipo", "HEMOGRAMA" }, { "titulo", "Hemograma" }, { "dataExame", DateTime.UtcNow },
        { "laboratorio", BsonNull.Value }, { "observacao", BsonNull.Value }, { "dados", new BsonDocument() },
        { "arquivo", new BsonDocument
            {
                { "chave", "clinica/1/pet/10/laudo/x.pdf" }, { "contentType", "application/pdf" },
                { "tamanhoBytes", 10L }, { "sha256", new string('b', 64) },
            }
        },
        { "stAtivo", true }, { "criadoEm", DateTime.UtcNow }, { "atualizadoEm", DateTime.UtcNow },
        { "removidoEm", BsonNull.Value },
    };

    private async Task<MongoContext> ContextoComSchemaAsync()
    {
        var ctx = mongo.NovoContexto();
        await new MongoSchemaRunner(ctx, NullLogger<MongoSchemaRunner>.Instance).AplicarAsync(CancellationToken.None);
        return ctx;
    }

    [Fact]
    public async Task Schema_aceita_documento_valido_controle_positivo()
    {
        var ctx = await ContextoComSchemaAsync();

        var agir = () => ctx.LaudosBruto.InsertOneAsync(DocumentoValido());

        await agir.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Schema_recusa_tipo_fora_do_enum()
    {
        var ctx = await ContextoComSchemaAsync();
        var doc = DocumentoValido();
        doc["tipo"] = "XYZ";

        var agir = () => ctx.LaudosBruto.InsertOneAsync(doc);

        (await agir.Should().ThrowAsync<MongoWriteException>())
            .Which.WriteError.Code.Should().Be(121); // DocumentValidationFailure
    }

    [Fact]
    public async Task Schema_recusa_campo_extra_na_raiz()
    {
        var ctx = await ContextoComSchemaAsync();
        var doc = DocumentoValido();
        doc["nomeArquivoOriginal"] = "joao-silva.pdf";

        var agir = () => ctx.LaudosBruto.InsertOneAsync(doc);

        (await agir.Should().ThrowAsync<MongoWriteException>()).Which.WriteError.Code.Should().Be(121);
    }

    [Fact]
    public async Task Schema_recusa_campo_extra_em_arquivo_sha256_invalido_e_idClinica_int32()
    {
        var ctx = await ContextoComSchemaAsync();

        var extra = DocumentoValido();
        extra["arquivo"]["extra"] = 1;
        var shaRuim = DocumentoValido();
        shaRuim["arquivo"]["sha256"] = "ZZ";
        var int32 = DocumentoValido();
        int32["idClinica"] = 1; // Int32, o contrato exige long

        foreach (var doc in new[] { extra, shaRuim, int32 })
        {
            var agir = () => ctx.LaudosBruto.InsertOneAsync(doc);
            (await agir.Should().ThrowAsync<MongoWriteException>()).Which.WriteError.Code.Should().Be(121);
        }
    }

    [Fact]
    public async Task Indices_existem_com_os_tres_nomes_do_contrato()
    {
        var ctx = await ContextoComSchemaAsync();

        var indices = await (await ctx.LaudosBruto.Indexes.ListAsync()).ToListAsync();
        var nomes = indices.Select(i => i["name"].AsString).ToList();

        nomes.Should().Contain(new[]
        {
            "ix_laudo_clinica_pet_ativo_data",
            "ix_laudo_clinica_pet_ativo_tipo_data",
            "ix_laudo_pet_ativo_data",
        });
        var ix3 = indices.Single(i => i["name"].AsString == "ix_laudo_pet_ativo_data")["key"].AsBsonDocument;
        ix3.Names.Should().Equal("idPet", "stAtivo", "dataExame");
    }

    [Fact]
    public async Task Runner_roda_duas_vezes_sem_erro_e_sem_duplicar_indices()
    {
        var ctx = mongo.NovoContexto();
        var runner = new MongoSchemaRunner(ctx, NullLogger<MongoSchemaRunner>.Instance);

        await runner.StartAsync(CancellationToken.None);
        var segunda = () => runner.StartAsync(CancellationToken.None);

        await segunda.Should().NotThrowAsync();
        var indices = await (await ctx.LaudosBruto.Indexes.ListAsync()).ToListAsync();
        indices.Should().HaveCount(4); // _id_ + os 3 do contrato
    }

    [Fact]
    public async Task Segunda_aplicacao_repara_validator_removido_collMod()
    {
        var ctx = await ContextoComSchemaAsync();
        await ctx.Database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "collMod", "laudos" }, { "validator", new BsonDocument() }, { "validationLevel", "off" },
        });
        var invalido = DocumentoValido();
        invalido["tipo"] = "XYZ";
        await ctx.LaudosBruto.InsertOneAsync(invalido); // sem validator entra

        await new MongoSchemaRunner(ctx, NullLogger<MongoSchemaRunner>.Instance).StartAsync(CancellationToken.None);

        var outro = DocumentoValido();
        outro["tipo"] = "XYZ";
        var agir = () => ctx.LaudosBruto.InsertOneAsync(outro);
        await agir.Should().ThrowAsync<MongoWriteException>();
    }

    [Fact]
    public async Task Runner_com_InitEnabled_false_nao_toca_no_banco()
    {
        var ctx = mongo.NovoContexto(initEnabled: false);

        await new MongoSchemaRunner(ctx, NullLogger<MongoSchemaRunner>.Instance).StartAsync(CancellationToken.None);

        var colecoes = await (await ctx.Database.ListCollectionNamesAsync()).ToListAsync();
        colecoes.Should().NotContain("laudos");
    }

    [Fact]
    public async Task Runner_sem_connection_string_nao_lanca()
    {
        var ctx = new MongoContext(Options.Create(new MongoOptions()));

        var agir = () => new MongoSchemaRunner(ctx, NullLogger<MongoSchemaRunner>.Instance)
            .StartAsync(CancellationToken.None);

        await agir.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Runner_com_mongo_inalcancavel_nao_lanca_e_loga_um_warn_sem_URI()
    {
        const string senha = "S3nh4Secreta";
        var ctx = new MongoContext(Options.Create(new MongoOptions
        {
            ConnectionString = $"mongodb://kura_dotnet:{senha}@127.0.0.1:1/kura_docs?authSource=kura_docs",
            ServerSelectionTimeoutSeconds = 1,
        }));
        var logger = new Mock<ILogger<MongoSchemaRunner>>();
        var mensagens = new List<(LogLevel Nivel, string Texto)>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        logger.Setup(l => l.Log(
                It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(i =>
            {
                var formatter = (Delegate)i.Arguments[4];
                var texto = (string)formatter.DynamicInvoke(i.Arguments[2], i.Arguments[3])!;
                mensagens.Add(((LogLevel)i.Arguments[0], texto));
            }));

        var agir = () => new MongoSchemaRunner(ctx, logger.Object).StartAsync(CancellationToken.None);

        await agir.Should().NotThrowAsync();
        mensagens.Should().ContainSingle();
        mensagens[0].Nivel.Should().Be(LogLevel.Warning);
        mensagens[0].Texto.Should().NotContain(senha).And.NotContain("mongodb://").And.NotContain("127.0.0.1");
    }
}
