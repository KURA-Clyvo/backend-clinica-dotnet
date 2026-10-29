namespace Kura.Infrastructure.Tests;

using System.Text;
using FluentAssertions;

/// <summary>
/// S4-DB-06 — teste-guarda: a rotina Oracle que exporta cobranças de <b>TODAS</b> as clínicas
/// nunca pode ser chamada pela .NET. Uma chamada dela, a partir de um endpoint autenticado,
/// seria vazamento cross-tenant completo em um GET.
///
/// <para>Dois detectores, cada um com o seu controle positivo (feito à mão ao escrever o teste:
/// pôr o nome da rotina num arquivo de <c>src/</c> faz o primeiro acusar; o segundo é provado
/// pelo teste de controle abaixo, que escaneia bytes que sabidamente contêm a string):</para>
/// <list type="number">
/// <item><description>fonte: nenhum <c>*.cs</c>/<c>*.sql</c> de <c>src/</c> contém o nome,
/// em qualquer caixa;</description></item>
/// <item><description>binário: nenhum dos assemblies de <c>src/</c> carregados no teste contém o
/// nome (UTF-16 ou UTF-8, em qualquer caixa) — pega literal que o detector de fonte não veria
/// (ex.: string vinda de arquivo gerado).</description></item>
/// </list>
/// </summary>
public class ExportarTodasGuardaTests
{
    // Montado em partes para este arquivo (que mora em tests/, fora do escopo do scan) não ser
    // o motivo de um falso positivo em ferramentas de busca.
    private static readonly string Proibida = "EXPORTAR" + "_" + "TODAS";

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KuraApi.slnx")))
            dir = dir.Parent;
        dir.Should().NotBeNull("o teste precisa achar a raiz do repo para escanear src/");
        return dir!.FullName;
    }

    private static bool ContemEmQualquerCaixa(byte[] bytes, string needle)
    {
        foreach (var enc in new Encoding[] { Encoding.UTF8, Encoding.Unicode })
        {
            var texto = enc.GetString(bytes);
            if (texto.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    [Fact]
    public void Nenhum_arquivo_de_fonte_em_src_menciona_a_rotina_de_todas_as_clinicas()
    {
        var src = Path.Combine(RaizDoRepo(), "src");
        var arquivos = Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".sql") || f.EndsWith(".json") || f.EndsWith(".xml"))
            .ToList();

        arquivos.Should().NotBeEmpty("um scan sobre zero arquivos é um detector cego");

        var ofensores = arquivos
            .Where(f => File.ReadAllText(f).Contains(Proibida, StringComparison.OrdinalIgnoreCase))
            .ToList();

        ofensores.Should().BeEmpty("a .NET nunca pode chamar a exportação de todas as clínicas");
    }

    [Fact]
    public void Nenhum_assembly_de_src_contem_o_nome_da_rotina()
    {
        var assemblies = new[]
        {
            typeof(Kura.Infrastructure.RotinasOracle.RotinasCobrancaOracle).Assembly,
            typeof(Kura.Application.Services.CobrancaOracleService).Assembly,
            typeof(Kura.Domain.Interfaces.IRotinasCobrancaOracle).Assembly,
            typeof(Kura.Api.Controllers.FinanceiroController).Assembly,
        };

        foreach (var asm in assemblies)
        {
            var bytes = File.ReadAllBytes(asm.Location);
            bytes.Length.Should().BeGreaterThan(1000);
            ContemEmQualquerCaixa(bytes, Proibida)
                .Should().BeFalse($"{asm.GetName().Name} não pode referenciar a exportação de todas as clínicas");
        }
    }

    [Fact]
    public void Controle_positivo_o_detector_binario_enxerga_a_string_em_qualquer_caixa_e_codificacao()
    {
        ContemEmQualquerCaixa(Encoding.UTF8.GetBytes("xx" + Proibida.ToLowerInvariant() + "yy"), Proibida).Should().BeTrue();
        ContemEmQualquerCaixa(Encoding.Unicode.GetBytes("xx" + Proibida + "yy"), Proibida).Should().BeTrue();
        ContemEmQualquerCaixa(Encoding.UTF8.GetBytes("PKG_COBRANCA.LISTAR_JSON"), Proibida).Should().BeFalse();
    }
}
