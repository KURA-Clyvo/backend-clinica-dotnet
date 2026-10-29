namespace Kura.Infrastructure.Tests;

using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
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

    /// <summary>
    /// Detector binário: (a) user strings do assembly via <c>MetadataReader.GetUserString</c>
    /// (independe de alinhamento do heap #US) e (b) varredura crua em UTF-8 e UTF-16 a partir do
    /// byte 0 <b>e</b> do byte 1 (UTF-16 em offset ímpar escapava da versão anterior).
    /// </summary>
    private static bool ContemEmQualquerCaixa(byte[] bytes, string needle)
    {
        foreach (var enc in new Encoding[] { Encoding.UTF8, Encoding.Unicode })
        {
            for (var inicio = 0; inicio < 2; inicio++)
            {
                var texto = enc.GetString(bytes, inicio, bytes.Length - inicio);
                if (texto.Contains(needle, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return ContemEmUserString(bytes, needle);
    }

    private static bool ContemEmUserString(byte[] pe, string needle)
    {
        try
        {
            using var peReader = new PEReader(new MemoryStream(pe));
            if (!peReader.HasMetadata)
                return false;
            var md = peReader.GetMetadataReader();
            // O handle 0 do heap #US é a string vazia reservada; a enumeração começa no próximo.
            for (var h = md.GetNextHandle(MetadataTokens.UserStringHandle(0)); !h.IsNil; h = md.GetNextHandle(h))
            {
                if (md.GetUserString(h).Contains(needle, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        catch (BadImageFormatException)
        {
            return false; // bytes crus (controles) não são PE
        }
    }

    /// <summary>Todos os assemblies de <c>src/</c> (um por projeto), achados pelo nome da pasta.</summary>
    private static List<string> DllsDeSrc()
    {
        var src = Path.Combine(RaizDoRepo(), "src");
        var nomes = Directory.GetDirectories(src).Select(d => Path.GetFileName(d)!).ToList();
        nomes.Should().Contain("Kura.Api").And.Contain("Kura.Infrastructure");
        return nomes
            .Select(n => Path.Combine(AppContext.BaseDirectory, n + ".dll"))
            .Where(File.Exists)
            .ToList();
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
        var dlls = DllsDeSrc();
        dlls.Count.Should().Be(5, "os 5 projetos de src/ precisam ser escaneados (detector cego se faltar um)");

        foreach (var dll in dlls)
        {
            var bytes = File.ReadAllBytes(dll);
            bytes.Length.Should().BeGreaterThan(1000);
            ContemEmQualquerCaixa(bytes, Proibida)
                .Should().BeFalse($"{Path.GetFileName(dll)} não pode referenciar a exportação de todas as clínicas");
        }
    }

    [Fact]
    public void Controle_positivo_o_detector_binario_enxerga_a_string_em_qualquer_caixa_e_codificacao()
    {
        ContemEmQualquerCaixa(Encoding.UTF8.GetBytes("xx" + Proibida.ToLowerInvariant() + "yy"), Proibida).Should().BeTrue();
        ContemEmQualquerCaixa(Encoding.Unicode.GetBytes("xx" + Proibida + "yy"), Proibida).Should().BeTrue();
        ContemEmQualquerCaixa(Encoding.UTF8.GetBytes("PKG_COBRANCA.LISTAR_JSON"), Proibida).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Controle_positivo_UTF16_em_offset_par_e_impar(int prefixo)
    {
        var bytes = new byte[prefixo].Concat(Encoding.Unicode.GetBytes(Proibida)).ToArray();
        ContemEmQualquerCaixa(bytes, Proibida).Should().BeTrue($"UTF-16 começando no byte {prefixo}");
    }

    [Fact]
    public void Controle_positivo_o_detector_enxerga_literal_ldstr_dentro_de_um_assembly_real()
    {
        // Assembly de verdade (PE + metadata) cujo método carrega o literal partido em duas
        // partes e concatenado em IL — o ldstr grava cada parte no heap #US; as duas juntas só
        // existem em tempo de execução, então a user string inteira também é testada abaixo.
        byte[] Montar(string literal)
        {
            var ab = new PersistedAssemblyBuilder(new AssemblyName("ControleGuarda"), typeof(object).Assembly);
            var mb = ab.DefineDynamicModule("ControleGuarda");
            var tb = mb.DefineType("T", TypeAttributes.Public | TypeAttributes.Class);
            var m = tb.DefineMethod("M", MethodAttributes.Public | MethodAttributes.Static, typeof(string), Type.EmptyTypes);
            var il = m.GetILGenerator();
            il.Emit(OpCodes.Ldstr, "padding-" + literal);
            il.Emit(OpCodes.Ret);
            tb.CreateType();
            var ms = new MemoryStream();
            ab.Save(ms);
            return ms.ToArray();
        }

        ContemEmUserString(Montar(Proibida), Proibida).Should().BeTrue("o literal está no heap #US");
        ContemEmUserString(Montar("outra_coisa"), Proibida).Should().BeFalse();
    }
}
