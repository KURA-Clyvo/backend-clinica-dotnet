namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Application.Laudos;

/// <summary>
/// S4-NET-03 (G2-m3 da S4-NET-02): o <c>dados</c> que o service entrega ao repositório —
/// convertido de JSON por <see cref="LaudoEntrada.TentarDados"/> — grava e volta pelo Mongo
/// REAL embarcado (mongod 8.0), com objeto aninhado, array e números. Antes da conversão, um
/// <c>JsonElement</c> em <c>dados</c> dava <c>ArgumentException</c> crua no repositório.
/// </summary>
[Collection(MongoCollection.Nome)]
public class LaudoDadosConversaoMongoTests(MongoFixture mongo)
{
    [Fact]
    public async Task Dados_convertidos_de_JSON_gravam_e_voltam_pelo_mongo_com_aninhado_array_e_numero()
    {
        var (_, repo) = await mongo.NovoRepositorioAsync();
        LaudoEntrada.TentarDados(
            """{"hematocrito":42.5,"leucocitos":9000,"ok":true,"nada":null,"faixa":{"min":37,"max":55},"serie":[1,2.5,"x",{"a":1}]}""",
            out var dados).Should().BeTrue();
        var laudo = MongoFixture.NovoLaudo();
        laudo.Dados = dados;

        var inserido = await repo.InserirAsync(1, laudo);
        var lido = await repo.ObterAsync(1, inserido.Id);

        lido!.Dados["hematocrito"].Should().Be(42.5);
        lido.Dados["leucocitos"].Should().Be(9000L);
        lido.Dados["ok"].Should().Be(true);
        lido.Dados.Should().ContainKey("nada");
        ((IDictionary<string, object>)lido.Dados["faixa"]!)["min"].Should().Be(37L);
        var serie = ((System.Collections.IEnumerable)lido.Dados["serie"]!).Cast<object>().ToList();
        serie.Should().HaveCount(4);
        serie[1].Should().Be(2.5);
    }
}
