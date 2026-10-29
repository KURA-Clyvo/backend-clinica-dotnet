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
        ParametrosDe(paths, "/api/v2/pets", "get").Should().Contain(Nomes("page", "pageSize", "sort", "nome", "tutorId", "especieId", "porte"));
        ParametrosDe(paths, "/api/v2/tutores", "get").Should().Contain(Nomes("page", "pageSize", "sort", "busca"));
        ParametrosDe(paths, "/api/v2/agendamentos", "get").Should().Contain(Nomes("page", "pageSize", "sort", "dataInicio", "dataFim", "veterinarioId", "status"));
        ParametrosDe(paths, "/api/v1/pets/{idPet}/laudos", "get").Should().Contain(Nomes("page", "pageSize", "sort", "tipo", "de", "ate"));

        // respostas declaradas
        RespostasDe(paths, "/api/v2/pets", "get").Should().Contain(["200", "400"]);
        RespostasDe(paths, "/api/v2/agendamentos", "get").Should().Contain(["200", "400", "422"]);
        RespostasDe(paths, "/api/v1/pets/{idPet}/laudos", "get").Should().Contain(["200", "400", "404", "503"]);
        RespostasDe(paths, "/api/v1/pets/{idPet}/laudos", "post").Should().Contain(["201", "400", "413", "422", "503"]);
        RespostasDe(paths, "/api/v1/laudos/{id}", "delete").Should().Contain(["204", "403", "404"]);
    }

    [Fact]
    public async Task Parametros_de_query_saem_em_camelCase_e_nenhum_em_PascalCase()
    {
        var doc = JsonNode.Parse(await BaixarSwaggerNormalizadoAsync())!;

        var queryParams = doc["paths"]!.AsObject()
            .SelectMany(p => p.Value!.AsObject().Where(v => v.Key is "get" or "post" or "put" or "delete")
                .SelectMany(v => (v.Value!["parameters"]?.AsArray() ?? [])
                    .Where(x => x!["in"]!.GetValue<string>() == "query")
                    .Select(x => $"{v.Key.ToUpperInvariant()} {p.Key} :: {x!["name"]!.GetValue<string>()}")))
            .ToList();

        queryParams.Should().NotBeEmpty("controle positivo: o detector enxerga parâmetros de query");
        queryParams.Where(q => char.IsUpper(q.Split(" :: ")[1][0])).Should().BeEmpty(
            "nenhum parâmetro de query pode começar em maiúscula (G4-m-1)");
    }

    [Theory]
    [InlineData("/api/v1/pets/{idPet}/laudos", "arquivo", new[] { "arquivo", "tipo", "titulo", "dataExame", "laboratorio", "observacao", "idEventoClinico", "dados" })]
    [InlineData("/api/v1/pets/{id}/foto", "thumb", new[] { "thumb", "media" })]
    public async Task Uploads_multipart_documentam_o_requestBody_com_parte_binaria(string rota, string parteBinaria, string[] partes)
    {
        var doc = JsonNode.Parse(await BaixarSwaggerNormalizadoAsync())!;

        var corpo = doc["paths"]![rota]!["post"]!["requestBody"];

        corpo.Should().NotBeNull($"POST {rota} não pode sair com requestBody null (G4-m-2)");
        var schema = corpo!["content"]!["multipart/form-data"]!["schema"]!;
        schema["properties"]![parteBinaria]!["type"]!.GetValue<string>().Should().Be("string");
        schema["properties"]![parteBinaria]!["format"]!.GetValue<string>().Should().Be("binary");
        schema["properties"]!.AsObject().Select(p => p.Key).Should().BeEquivalentTo(partes);
        schema["required"]!.AsArray().Select(r => r!.GetValue<string>()).Should().Contain(parteBinaria);
    }

    private static HashSet<string> ParametrosDe(JsonObject paths, string rota, string verbo)
    {
        var op = paths[rota]?[verbo]
            ?? throw new Xunit.Sdk.XunitException($"Operação {verbo.ToUpperInvariant()} {rota} ausente do OpenAPI.");
        var nomes = (op["parameters"]?.AsArray() ?? [])
            .Select(p => p!["name"]!.GetValue<string>());
        return nomes.ToHashSet();
    }

    private static HashSet<string> RespostasDe(JsonObject paths, string rota, string verbo)
    {
        var op = paths[rota]?[verbo]
            ?? throw new Xunit.Sdk.XunitException($"Operação {verbo.ToUpperInvariant()} {rota} ausente do OpenAPI.");
        return op["responses"]!.AsObject().Select(r => r.Key).ToHashSet();
    }

    // S4-NET-07 (G4-m-1): comparação EXATA (com caixa) — o OpenAPI tem de dizer os mesmos nomes
    // camelCase que os `_links` e o README usam.
    private static string[] Nomes(params string[] nomes) => nomes;

    private async Task<string> BaixarSwaggerNormalizadoAsync()
    {
        var resposta = await _fabrica.CreateClient().GetAsync("/swagger/v1/swagger.json");
        resposta.EnsureSuccessStatusCode();
        return Normalizar(await resposta.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// As descrições do Swagger vêm dos comentários XML do código-fonte, cuja quebra de linha depende do checkout:
    /// CRLF no Windows (autocrlf) e LF no Linux do CI. Sem normalizar os VALORES de texto, o mesmo comentário vira
    /// <c>"\r\n"</c> num lugar e <c>"\n"</c> no outro, e o documento versionado nunca bate no CI.
    /// </summary>
    private static string Normalizar(string json)
    {
        var no = NormalizarQuebrasDeLinha(JsonNode.Parse(json)!)!;
        var texto = no.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        return texto.Replace("\r\n", "\n").TrimEnd() + "\n";
    }

    private static JsonNode? NormalizarQuebrasDeLinha(JsonNode? no)
    {
        switch (no)
        {
            case JsonObject objeto:
                foreach (var chave in objeto.Select(p => p.Key).ToList())
                    objeto[chave] = NormalizarQuebrasDeLinha(objeto[chave]?.DeepClone());
                return objeto;
            case JsonArray lista:
                for (var i = 0; i < lista.Count; i++)
                    lista[i] = NormalizarQuebrasDeLinha(lista[i]?.DeepClone());
                return lista;
            case JsonValue valor when valor.TryGetValue<string>(out var texto):
                return JsonValue.Create(texto.Replace("\r\n", "\n"));
            default:
                return no;
        }
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
