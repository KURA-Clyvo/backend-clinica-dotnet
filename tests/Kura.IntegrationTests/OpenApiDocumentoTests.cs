namespace Kura.IntegrationTests;

using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

/// <summary>
/// S4-NET-06 — o <c>docs/openapi.json</c> versionado é o Swagger EXPORTADO da aplicação e não pode
/// envelhecer: este teste sobe o <c>Program.cs</c> real, baixa <c>/swagger/v1/swagger.json</c> e
/// compara com o arquivo. Rota nova, parâmetro novo ou summary alterado sem regenerar o arquivo
/// derruba a suíte.
///
/// <para><b>Para regenerar:</b> <c>KURA_ATUALIZAR_OPENAPI=1 dotnet test tests/Kura.IntegrationTests
/// --filter "FullyQualifiedName~OpenApiDocumentoTests"</c> (reescreve o arquivo; depois revise o diff e
/// commite). O arquivo nunca é escrito à mão.</para>
///
/// <para>Comparação por texto normalizado (JSON indentado, fim de linha LF): o arquivo é gerado
/// por este mesmo código, então a diferença que sobra é conteúdo, não formatação.</para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class OpenApiDocumentoTests : IClassFixture<KuraApiFactory>
{
    private const string VariavelAtualizar = "KURA_ATUALIZAR_OPENAPI";

    private readonly KuraApiFactory _fabrica;

    public OpenApiDocumentoTests(KuraApiFactory fabrica) => _fabrica = fabrica;

    [Fact]
    public async Task Documento_versionado_confere_com_o_swagger_gerado_pela_aplicacao()
    {
        // Arrange
        var caminho = CaminhoDoArquivo();
        var gerado = await BaixarSwaggerNormalizadoAsync();

        if (Environment.GetEnvironmentVariable(VariavelAtualizar) == "1")
        {
            File.WriteAllText(caminho, gerado);
        }

        // Act
        File.Exists(caminho).Should().BeTrue(
            $"docs/openapi.json precisa existir; gere com {VariavelAtualizar}=1 (ver o XML desta classe).");
        var versionado = Normalizar(File.ReadAllText(caminho));

        // Assert
        versionado.Should().Be(gerado,
            $"docs/openapi.json está desatualizado em relação à API. Regenere com {VariavelAtualizar}=1 " +
            "dotnet test tests/Kura.IntegrationTests --filter \"FullyQualifiedName~OpenApiDocumentoTests\" " +
            "e commite o arquivo.");
    }

    [Fact]
    public async Task Endpoints_novos_aparecem_com_paginacao_ordenacao_filtros_e_respostas()
    {
        // Arrange / Act
        var doc = JsonNode.Parse(await BaixarSwaggerNormalizadoAsync())!;
        var paths = doc["paths"]!.AsObject();

        // Assert — as 3 listas v2 e a lista de laudos expõem page, pageSize, sort e o próprio filtro
        ParametrosDe(paths, "/api/v2/pets", "get").Should().Contain(Nomes("page", "pageSize", "sort", "Nome", "TutorId", "EspecieId", "Porte"));
        ParametrosDe(paths, "/api/v2/tutores", "get").Should().Contain(Nomes("page", "pageSize", "sort", "Busca"));
        ParametrosDe(paths, "/api/v2/agendamentos", "get").Should().Contain(Nomes("page", "pageSize", "sort", "DataInicio", "DataFim", "VeterinarioId", "Status"));
        ParametrosDe(paths, "/api/v1/pets/{idPet}/laudos", "get").Should().Contain(Nomes("page", "pageSize", "sort", "tipo", "de", "ate"));

        // respostas declaradas
        RespostasDe(paths, "/api/v2/pets", "get").Should().Contain(["200", "400"]);
        RespostasDe(paths, "/api/v2/agendamentos", "get").Should().Contain(["200", "400", "422"]);
        RespostasDe(paths, "/api/v1/pets/{idPet}/laudos", "get").Should().Contain(["200", "400", "404", "503"]);
        RespostasDe(paths, "/api/v1/pets/{idPet}/laudos", "post").Should().Contain(["201", "400", "413", "422", "503"]);
        RespostasDe(paths, "/api/v1/laudos/{id}", "delete").Should().Contain(["204", "403", "404"]);
    }

    private static HashSet<string> ParametrosDe(JsonObject paths, string rota, string verbo)
    {
        var op = paths[rota]?[verbo]
            ?? throw new Xunit.Sdk.XunitException($"Operação {verbo.ToUpperInvariant()} {rota} ausente do OpenAPI.");
        var nomes = (op["parameters"]?.AsArray() ?? [])
            .Select(p => Normalizar2(p!["name"]!.GetValue<string>()));
        return nomes.ToHashSet();
    }

    private static HashSet<string> RespostasDe(JsonObject paths, string rota, string verbo)
    {
        var op = paths[rota]?[verbo]
            ?? throw new Xunit.Sdk.XunitException($"Operação {verbo.ToUpperInvariant()} {rota} ausente do OpenAPI.");
        return op["responses"]!.AsObject().Select(r => r.Key).ToHashSet();
    }

    // O Swashbuckle publica os nomes dos parâmetros como o model binding os conhece; a comparação
    // ignora a caixa para não depender de a convenção ser PascalCase (listas v2) ou camelCase.
    private static string[] Nomes(params string[] nomes) => nomes.Select(Normalizar2).ToArray();

    private static string Normalizar2(string nome) => nome.ToLowerInvariant();

    private async Task<string> BaixarSwaggerNormalizadoAsync()
    {
        var resposta = await _fabrica.CreateClient().GetAsync("/swagger/v1/swagger.json");
        resposta.EnsureSuccessStatusCode();
        return Normalizar(await resposta.Content.ReadAsStringAsync());
    }

    private static string Normalizar(string json)
    {
        var no = JsonNode.Parse(json)!;
        var texto = no.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        return texto.Replace("\r\n", "\n").TrimEnd() + "\n";
    }

    /// <summary>Raiz do repositório = pasta que contém <c>KuraApi.slnx</c>; o arquivo vive em <c>docs/openapi.json</c>.</summary>
    private static string CaminhoDoArquivo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KuraApi.slnx")))
            dir = dir.Parent;
        dir.Should().NotBeNull("KuraApi.slnx marca a raiz do repositório");
        return Path.Combine(dir!.FullName, "docs", "openapi.json");
    }
}
