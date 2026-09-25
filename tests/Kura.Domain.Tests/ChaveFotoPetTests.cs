namespace Kura.Domain.Tests;

using FluentAssertions;
using Kura.Domain.Storage;

/// <summary>
/// Cobertura direta de <see cref="ChaveFotoPet"/> — FT-03/backlog
/// <c>KURA_BACKLOG_FOTO_PET.md</c>, ruling F7-a. A cobertura indireta (via
/// <c>PetFotoServiceTests</c>/<c>PetFotoHttpTests</c>) continua existindo; este arquivo é
/// novo na fix wave G2 (g2-ft03.md, achado G2-f) especificamente para o comportamento de
/// borda que a G2 apontou como sem teste próprio: chave sem extensão.
/// </summary>
public class ChaveFotoPetTests
{
    [Fact]
    public void Base_MontaChaveComExtensao()
    {
        var chave = ChaveFotoPet.Base(7, 12, "abc", "webp");
        chave.Should().Be("clinica/7/pet/12/abc.webp");
    }

    [Theory]
    [InlineData("clinica/7/pet/12/abc.webp", ChaveFotoPet.SufixoThumb, "clinica/7/pet/12/abc_256.webp")]
    [InlineData("clinica/7/pet/12/abc.webp", ChaveFotoPet.SufixoMedia, "clinica/7/pet/12/abc_1080.webp")]
    [InlineData("clinica/1/pet/1/x.jpg", ChaveFotoPet.SufixoThumb, "clinica/1/pet/1/x_256.jpg")]
    public void Variante_InsereSufixoAntesDaExtensao(string chaveBase, string sufixo, string esperada)
    {
        ChaveFotoPet.Variante(chaveBase, sufixo).Should().Be(esperada);
    }

    /// <summary>
    /// 🔴 Fix wave G2 (g2-ft03.md, achado G2-f): a versão anterior tinha um fallback
    /// silencioso para chave sem ponto — devolvia <c>"{chaveBase}_{sufixo}"</c> sem extensão
    /// em vez de sinalizar o problema. A G2 apontou o risco: <see cref="ChaveFotoPet.Base"/>
    /// SEMPRE produz chave com extensão, então chegar aqui sem ponto só pode significar uso
    /// indevido do helper — e uma chave sem extensão que sobrevive sem erro é pior para quem
    /// replica esta fórmula (FT-05/Java, regra 11 do CLAUDE.md) do que falhar cedo. Mordida:
    /// antes desta fix wave, este teste falhava (recebia
    /// <c>"clinica/7/pet/12/abc_256"</c> em vez de lançar).
    /// </summary>
    [Fact]
    public void Variante_ChaveBaseSemExtensao_Lanca()
    {
        var act = () => ChaveFotoPet.Variante("clinica/7/pet/12/abc", ChaveFotoPet.SufixoThumb);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("chaveBase");
    }
}
