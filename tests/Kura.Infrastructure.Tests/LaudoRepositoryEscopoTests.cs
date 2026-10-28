namespace Kura.Infrastructure.Tests;

using System.Reflection;
using FluentAssertions;
using Kura.Domain.Interfaces;

/// <summary>
/// S4-NET-02: o equivalente Mongo do <c>TenantFilterCoverageTests</c>. Todo método público de
/// <see cref="ILaudoRepository"/> tem de exigir <c>long idClinica</c>; método novo sem escopo
/// quebra este teste em vez de virar leitura cross-tenant silenciosa. Controle positivo
/// (mutação) registrado no relatório da task.
/// </summary>
public class LaudoRepositoryEscopoTests
{
    [Fact]
    public void Toda_operacao_do_repositorio_exige_long_idClinica()
    {
        var metodos = typeof(ILaudoRepository)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .ToList();

        metodos.Should().NotBeEmpty("o detector precisa enxergar métodos para a prova valer");
        var semEscopo = metodos
            .Where(m => !m.GetParameters().Any(p => p.Name == "idClinica" && p.ParameterType == typeof(long)))
            .Select(m => m.Name)
            .ToList();

        semEscopo.Should().BeEmpty("método sem 'long idClinica' = leitura/escrita sem escopo de tenant");
    }

    [Fact]
    public void A_interface_nao_expoe_exclusao_fisica()
    {
        typeof(ILaudoRepository).GetMethods().Select(m => m.Name)
            .Should().NotContain(n => n.Contains("Delete") || n.StartsWith("Remover"));
    }
}
