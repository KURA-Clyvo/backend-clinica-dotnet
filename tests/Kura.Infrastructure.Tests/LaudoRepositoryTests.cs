namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Laudos;
using MongoDB.Bson;
using MongoDB.Driver;

/// <summary>
/// S4-NET-02: repositório de laudos contra mongod 8.0 embarcado (Mongo2Go). Padrão AAA.
/// </summary>
[Collection(MongoCollection.Nome)]
public class LaudoRepositoryTests(MongoFixture mongo)
{
    private static DateTime Dia(int d) => new(2026, 10, d, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Inserir_e_Obter_devolve_o_laudo_com_contrato_preservado()
    {
        var (_, repo) = await mongo.NovoRepositorioAsync();
        var novo = MongoFixture.NovoLaudo(idClinica: 1, idPet: 10);

        var inserido = await repo.InserirAsync(1, novo);
        var lido = await repo.ObterAsync(1, inserido.Id);

        lido.Should().NotBeNull();
        lido!.Id.Should().HaveLength(24);
        lido.IdClinica.Should().Be(1);
        lido.IdPet.Should().Be(10);
        lido.Tipo.Should().Be(TipoLaudo.HEMOGRAMA);
        lido.StAtivo.Should().BeTrue();
        lido.RemovidoEm.Should().BeNull();
        lido.DataExame.Should().Be(novo.DataExame);
        lido.Arquivo.Sha256.Should().Be(new string('a', 64));
        lido.Arquivo.TamanhoBytes.Should().Be(1234);
        lido.Dados.Should().ContainKey("hematocrito");
    }

    [Fact]
    public async Task Documento_gravado_segue_o_contrato_BSON_do_G0()
    {
        var (ctx, repo) = await mongo.NovoRepositorioAsync();
        var inserido = await repo.InserirAsync(1, MongoFixture.NovoLaudo(idClinica: 1, idPet: 10));

        var bruto = await ctx.LaudosBruto.Find(new BsonDocument("_id", ObjectId.Parse(inserido.Id))).SingleAsync();

        bruto["idClinica"].BsonType.Should().Be(BsonType.Int64);
        bruto["idPet"].BsonType.Should().Be(BsonType.Int64);
        bruto["idUsuarioAutor"].BsonType.Should().Be(BsonType.Int64);
        bruto["idEventoClinico"].BsonType.Should().Be(BsonType.Null);
        bruto["tipo"].AsString.Should().Be("HEMOGRAMA");
        bruto["dataExame"].BsonType.Should().Be(BsonType.DateTime);
        bruto["arquivo"]["tamanhoBytes"].BsonType.Should().Be(BsonType.Int64);
        bruto["arquivo"]["sha256"].AsString.Should().MatchRegex("^[0-9a-f]{64}$");
        bruto["stAtivo"].AsBoolean.Should().BeTrue();
        bruto["removidoEm"].BsonType.Should().Be(BsonType.Null);
        bruto.Names.Should().BeEquivalentTo(new[]
        {
            "_id", "idClinica", "idPet", "idEventoClinico", "idUsuarioAutor", "tipo", "titulo", "dataExame",
            "laboratorio", "observacao", "dados", "arquivo", "stAtivo", "criadoEm", "atualizadoEm", "removidoEm",
        });
    }

    [Fact]
    public async Task Laudo_de_uma_clinica_e_invisivel_para_outra_no_detalhe()
    {
        var (_, repo) = await mongo.NovoRepositorioAsync();
        var a = await repo.InserirAsync(1, MongoFixture.NovoLaudo(idClinica: 1, idPet: 10));

        (await repo.ObterAsync(2, a.Id)).Should().BeNull();
        (await repo.ObterAsync(1, a.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Laudo_de_uma_clinica_e_invisivel_para_outra_na_lista_mesmo_com_mesmo_idPet()
    {
        var (_, repo) = await mongo.NovoRepositorioAsync();
        await repo.InserirAsync(1, MongoFixture.NovoLaudo(idClinica: 1, idPet: 10, titulo: "da A"));
        await repo.InserirAsync(2, MongoFixture.NovoLaudo(idClinica: 2, idPet: 10, titulo: "da B"));

        var listaB = await repo.ListarPorPetAsync(2, 10, new ConsultaLaudos());
        var listaA = await repo.ListarPorPetAsync(1, 10, new ConsultaLaudos());

        listaB.Itens.Select(l => l.Titulo).Should().Equal("da B");
        listaA.Itens.Select(l => l.Titulo).Should().Equal("da A");
        listaB.Total.Should().Be(1);
    }

    [Fact]
    public async Task Exclusao_pela_clinica_B_nao_altera_o_documento_da_clinica_A()
    {
        var (ctx, repo) = await mongo.NovoRepositorioAsync();
        var a = await repo.InserirAsync(1, MongoFixture.NovoLaudo(idClinica: 1, idPet: 10));

        var excluiu = await repo.ExcluirLogicoAsync(2, a.Id);

        excluiu.Should().BeFalse();
        var bruto = await ctx.LaudosBruto.Find(new BsonDocument("_id", ObjectId.Parse(a.Id))).SingleAsync();
        bruto["stAtivo"].AsBoolean.Should().BeTrue();
        bruto["removidoEm"].BsonType.Should().Be(BsonType.Null);
        (await repo.ObterAsync(1, a.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Exclusao_logica_marca_stAtivo_false_e_removidoEm_e_some_das_leituras_sem_apagar()
    {
        var (ctx, repo) = await mongo.NovoRepositorioAsync();
        var a = await repo.InserirAsync(1, MongoFixture.NovoLaudo(idClinica: 1, idPet: 10));

        var excluiu = await repo.ExcluirLogicoAsync(1, a.Id);
        var segunda = await repo.ExcluirLogicoAsync(1, a.Id);

        excluiu.Should().BeTrue();
        segunda.Should().BeFalse();
        (await repo.ObterAsync(1, a.Id)).Should().BeNull();
        (await repo.ListarPorPetAsync(1, 10, new ConsultaLaudos())).Itens.Should().BeEmpty();
        var bruto = await ctx.LaudosBruto.Find(new BsonDocument("_id", ObjectId.Parse(a.Id))).SingleAsync();
        bruto["stAtivo"].AsBoolean.Should().BeFalse();
        bruto["removidoEm"].BsonType.Should().Be(BsonType.DateTime);
    }

    [Fact]
    public async Task Id_invalido_nao_lanca_e_nao_encontra()
    {
        var (_, repo) = await mongo.NovoRepositorioAsync();

        (await repo.ObterAsync(1, "nao-e-objectid")).Should().BeNull();
        (await repo.ExcluirLogicoAsync(1, "nao-e-objectid")).Should().BeFalse();
    }

    [Fact]
    public async Task Inserir_com_idClinica_divergente_do_laudo_e_recusado()
    {
        var (_, repo) = await mongo.NovoRepositorioAsync();

        var agir = () => repo.InserirAsync(2, MongoFixture.NovoLaudo(idClinica: 1));

        await agir.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Lista_filtra_por_tipo_e_por_periodo()
    {
        var (_, repo) = await mongo.NovoRepositorioAsync();
        await repo.InserirAsync(1, MongoFixture.NovoLaudo(tipo: TipoLaudo.HEMOGRAMA, titulo: "h1", dataExame: Dia(1)));
        await repo.InserirAsync(1, MongoFixture.NovoLaudo(tipo: TipoLaudo.IMAGEM, titulo: "i5", dataExame: Dia(5)));
        await repo.InserirAsync(1, MongoFixture.NovoLaudo(tipo: TipoLaudo.IMAGEM, titulo: "i9", dataExame: Dia(9)));

        var soImagem = await repo.ListarPorPetAsync(1, 10, new ConsultaLaudos(Tipo: TipoLaudo.IMAGEM));
        var periodo = await repo.ListarPorPetAsync(1, 10, new ConsultaLaudos(DataDe: Dia(4), DataAte: Dia(6)));

        soImagem.Itens.Select(l => l.Titulo).Should().Equal("i9", "i5");
        periodo.Itens.Select(l => l.Titulo).Should().Equal("i5");
    }

    [Fact]
    public async Task Lista_ordena_pela_lista_branca()
    {
        var (_, repo) = await mongo.NovoRepositorioAsync();
        await repo.InserirAsync(1, MongoFixture.NovoLaudo(titulo: "B", dataExame: Dia(2)));
        await repo.InserirAsync(1, MongoFixture.NovoLaudo(titulo: "C", dataExame: Dia(3)));
        await repo.InserirAsync(1, MongoFixture.NovoLaudo(titulo: "A", dataExame: Dia(1)));

        var padrao = await repo.ListarPorPetAsync(1, 10, new ConsultaLaudos());
        var dataAsc = await repo.ListarPorPetAsync(1, 10, new ConsultaLaudos(Ordem: OrdenacaoLaudo.DataExameAsc));
        var tituloAsc = await repo.ListarPorPetAsync(1, 10, new ConsultaLaudos(Ordem: OrdenacaoLaudo.TituloAsc));
        var tituloDesc = await repo.ListarPorPetAsync(1, 10, new ConsultaLaudos(Ordem: OrdenacaoLaudo.TituloDesc));

        padrao.Itens.Select(l => l.Titulo).Should().Equal("C", "B", "A");
        dataAsc.Itens.Select(l => l.Titulo).Should().Equal("A", "B", "C");
        tituloAsc.Itens.Select(l => l.Titulo).Should().Equal("A", "B", "C");
        tituloDesc.Itens.Select(l => l.Titulo).Should().Equal("C", "B", "A");
    }

    [Fact]
    public async Task Lista_pagina_e_informa_o_total()
    {
        var (_, repo) = await mongo.NovoRepositorioAsync();
        for (var i = 1; i <= 5; i++)
            await repo.InserirAsync(1, MongoFixture.NovoLaudo(titulo: $"L{i}", dataExame: Dia(i)));

        var p1 = await repo.ListarPorPetAsync(1, 10, new ConsultaLaudos(Pagina: 1, TamanhoPagina: 2));
        var p3 = await repo.ListarPorPetAsync(1, 10, new ConsultaLaudos(Pagina: 3, TamanhoPagina: 2));
        var p4 = await repo.ListarPorPetAsync(1, 10, new ConsultaLaudos(Pagina: 4, TamanhoPagina: 2));

        p1.Itens.Select(l => l.Titulo).Should().Equal("L5", "L4");
        p3.Itens.Select(l => l.Titulo).Should().Equal("L1");
        p4.Itens.Should().BeEmpty();
        p1.Total.Should().Be(5);
        p1.TamanhoPagina.Should().Be(2);
    }

    [Fact]
    public async Task Lista_so_traz_o_pet_pedido()
    {
        var (_, repo) = await mongo.NovoRepositorioAsync();
        await repo.InserirAsync(1, MongoFixture.NovoLaudo(idPet: 10, titulo: "pet10"));
        await repo.InserirAsync(1, MongoFixture.NovoLaudo(idPet: 11, titulo: "pet11"));

        var lista = await repo.ListarPorPetAsync(1, 11, new ConsultaLaudos());

        lista.Itens.Select(l => l.Titulo).Should().Equal("pet11");
    }
}
