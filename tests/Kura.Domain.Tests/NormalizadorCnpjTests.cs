namespace Kura.Domain.Tests;

using FluentAssertions;
using Kura.Domain.Clinicas;

/// <summary>S4-FIX-02 (E67): formato canônico único de CNPJ.</summary>
public class NormalizadorCnpjTests
{
    [Theory]
    [InlineData("58.001.200/0001-49")]
    [InlineData("58001200000149")]
    [InlineData(" 58.001.200/0001-49 ")]
    [InlineData("58001200/000149")]
    public void TentarNormalizar_FormasValidas_DevolveMascarada(string entrada)
    {
        NormalizadorCnpj.TentarNormalizar(entrada, out var canonico).Should().BeTrue();
        canonico.Should().Be("58.001.200/0001-49");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("5800120000014")]      // 13 dígitos
    [InlineData("580012000001499")]    // 15 dígitos
    [InlineData("58.001.200/0001-4A")] // letra
    [InlineData("58 001 200 0001 49")] // separador não aceito
    public void TentarNormalizar_Invalidas_DevolveFalso(string? entrada)
    {
        NormalizadorCnpj.TentarNormalizar(entrada, out _).Should().BeFalse();
    }

    [Fact]
    public void SomenteDigitos_RemoveMascara() =>
        NormalizadorCnpj.SomenteDigitos("58.001.200/0001-49").Should().Be("58001200000149");
}
