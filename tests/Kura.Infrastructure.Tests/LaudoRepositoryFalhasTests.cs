namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Exceptions;
using Kura.Domain.Laudos;
using Kura.Infrastructure.Mongo;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

/// <summary>
/// Fix wave da S4-NET-02 (G2-I2, m2, m4, m5): classificação das falhas, paginação defensiva e
/// conversão real de datas para UTC. Contra mongod 8.0 embarcado.
/// </summary>
[Collection(MongoCollection.Nome)]
public class LaudoRepositoryFalhasTests(MongoFixture mongo)
{
    private const string ConteudoClinico = "CONTEUDO-CLINICO-SIGILOSO-";

    [Fact]
    public async Task Schema_recusado_vira_excecao_de_dado_invalido_sem_vazar_o_valor_recusado()
    {
        var (_, repo) = await mongo.NovoRepositorioAsync();
        var titulo = ConteudoClinico + new string('x', 121); // > 120: o schema recusa (código 121)
        var laudo = MongoFixture.NovoLaudo(titulo: titulo);
        laudo.Titulo.Should().Contain(ConteudoClinico, "controle: o valor sigiloso existe no input");

        var agir = () => repo.InserirAsync(1, laudo);

        var ex = (await agir.Should().ThrowAsync<LaudoRecusadoPeloSchemaException>()).Which;
        ex.InnerException.Should().BeNull("a exceção do driver carrega consideredValue (conteúdo clínico)");
        ex.ToString().Should().NotContain(ConteudoClinico);
        ex.Should().NotBeAssignableTo<ArmazenamentoDocumentosIndisponivelException>();
    }

    [Fact]
    public async Task Mongo_inalcancavel_vira_indisponivel()
    {
        var ctx = new MongoContext(Options.Create(new MongoOptions
        {
            ConnectionString = "mongodb://127.0.0.1:1/kura_docs",
            ServerSelectionTimeoutSeconds = 1,
        }));
        var repo = new LaudoRepository(ctx, TimeProvider.System);

        var agir = () => repo.ObterAsync(1, ObjectId.GenerateNewId().ToString());

        await agir.Should().ThrowAsync<ArmazenamentoDocumentosIndisponivelException>();
    }

    [Fact]
    public async Task Connection_string_malformada_vira_indisponivel_e_nao_vaza_a_senha()
    {
        var ctx = new MongoContext(Options.Create(new MongoOptions
        {
            ConnectionString = "mongodb://kura_dotnet:SenhaFixWave@host:portaruim/kura_docs",
        }));
        var repo = new LaudoRepository(ctx, TimeProvider.System);

        var agir = () => repo.ObterAsync(1, ObjectId.GenerateNewId().ToString());

        var ex = (await agir.Should().ThrowAsync<ArmazenamentoDocumentosIndisponivelException>()).Which;
        ex.ToString().Should().NotContain("SenhaFixWave");
    }

    [Fact]
    public async Task Mongo_nao_configurado_continua_indisponivel()
    {
        var ctx = new MongoContext(Options.Create(new MongoOptions()));
        var repo = new LaudoRepository(ctx, TimeProvider.System);

        var agir = () => repo.ListarPorPetAsync(1, 10, new ConsultaLaudos());

        await agir.Should().ThrowAsync<ArmazenamentoDocumentosIndisponivelException>();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task Paginacao_fora_do_intervalo_e_recusada_de_forma_explicita(int pagina, int tamanho)
    {
        var (_, repo) = await mongo.NovoRepositorioAsync();

        var agir = () => repo.ListarPorPetAsync(1, 10, new ConsultaLaudos(Pagina: pagina, TamanhoPagina: tamanho));

        var ex = (await agir.Should().ThrowAsync<ArgumentOutOfRangeException>()).Which;
        ex.Message.Should().NotContain("-200", "o erro tem de ser nosso, não o overflow do driver");
    }

    [Fact]
    public async Task Pagina_grande_mas_dentro_do_alcance_nao_estoura()
    {
        var (_, repo) = await mongo.NovoRepositorioAsync();

        var pagina = await repo.ListarPorPetAsync(1, 10, new ConsultaLaudos(Pagina: 1_000_000, TamanhoPagina: 100));

        pagina.Itens.Should().BeEmpty();
    }

    [Fact]
    public void ParaUtc_converte_Local_de_verdade_com_fuso_fixado()
    {
        var menos3 = TimeZoneInfo.CreateCustomTimeZone("teste-3", TimeSpan.FromHours(-3), "teste-3", "teste-3");
        var local = new DateTime(2026, 10, 1, 21, 0, 0, DateTimeKind.Local);

        var utc = LaudoDocument.ParaUtc(local, menos3);

        utc.Should().Be(new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc));
        utc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ParaUtc_mantem_Utc_e_trata_Unspecified_como_Utc()
    {
        var utc = new DateTime(2026, 10, 1, 21, 0, 0, DateTimeKind.Utc);
        var semKind = new DateTime(2026, 10, 1, 21, 0, 0, DateTimeKind.Unspecified);

        LaudoDocument.ParaUtc(utc).Should().Be(utc);
        var convertido = LaudoDocument.ParaUtc(semKind);
        convertido.Kind.Should().Be(DateTimeKind.Utc);
        convertido.Hour.Should().Be(21);
    }

    [Fact]
    public async Task Insert_e_filtro_de_periodo_com_data_Local_usam_o_instante_UTC()
    {
        var (ctx, repo) = await mongo.NovoRepositorioAsync();
        var local = new DateTime(2026, 10, 1, 21, 0, 0, DateTimeKind.Local);
        var inserido = await repo.InserirAsync(1, MongoFixture.NovoLaudo(dataExame: local));

        var bruto = await ctx.LaudosBruto
            .Find(new BsonDocument("_id", ObjectId.Parse(inserido.Id))).SingleAsync();
        var instante = local.ToUniversalTime();
        bruto["dataExame"].ToUniversalTime().Should().Be(instante);

        // filtro com os limites também em Local: o mesmo instante tem de casar
        var achou = await repo.ListarPorPetAsync(1, 10, new ConsultaLaudos(DataDe: local, DataAte: local));
        achou.Itens.Should().ContainSingle();
        var naoAchou = await repo.ListarPorPetAsync(1, 10,
            new ConsultaLaudos(DataDe: local.AddMinutes(1)));
        naoAchou.Itens.Should().BeEmpty();
    }
}
