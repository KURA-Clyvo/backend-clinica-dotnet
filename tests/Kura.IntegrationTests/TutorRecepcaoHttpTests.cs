namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

/// <summary>
/// G2 fix wave (KURA_BACKLOG_RECEPCAO.md, REC-01) — achado Important #2, medido pela sonda HTTP
/// do revisor (<c>g2-rec01.md</c>, M6/F2c): <c>dsWhatsapp</c> com <c>"+"</c> + 30 dígitos
/// (E.164 de 31 chars) passava o validator e devolvia <c>201</c> no pipeline REAL (InMemory) —
/// no Oracle real, <c>DS_WHATSAPP VARCHAR2(20)</c> devolveria <c>ORA-12899</c> (<c>500</c>).
///
/// <para>Vive em HTTP, não só no validator/service (que já têm cobertura própria em
/// <c>TutorCreateValidatorTests</c>/<c>NormalizadorTelefoneTests</c>): é o único jeito de provar
/// o STATUS CODE que o cliente de verdade recebe, exercitando o pipeline completo
/// (<c>AddFluentValidationAutoValidation</c> → 400 automático), igual ao que a sonda do G2
/// mediu.</para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class TutorRecepcaoHttpTests : IClassFixture<KuraApiFactory>
{
    private readonly KuraApiFactory _factory;

    public TutorRecepcaoHttpTests(KuraApiFactory factory) => _factory = factory;

    private async Task<HttpClient> ClienteAutenticadoAsync()
    {
        var client = _factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));
        return client;
    }

    private static object Corpo(string cpf, string email, string? dsWhatsapp, string nrTelefone = "11912340001") => new
    {
        nmTutor = "Teste I2",
        nrCpf = cpf,
        dsEmail = email,
        nrTelefone,
        dsWhatsapp,
        stAvisoPrivacidadeInformado = true,
        dsCanalConvite = "WHATSAPP",
    };

    [Fact]
    public async Task Post_DsWhatsappComTrintaDigitosMaisDdi_Retorna400()
    {
        // Mordida (I2): tirar o piso/teto de NormalizadorTelefone (ou o Must do validator) faz
        // este teste voltar a 201 — reprodução exata da sonda M6/F2c do G2.
        var client = await ClienteAutenticadoAsync();

        var resposta = await client.PostAsJsonAsync(
            "/api/v1/tutores",
            Corpo("90100000001", "i2a@teste.com", "+" + new string('9', 30)));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_DsWhatsappComFormatoValido_Retorna201()
    {
        // Controle positivo: o mesmo endpoint aceita um DsWhatsapp válido normalmente — o
        // teste acima falha por TAMANHO, não porque o campo ficou bloqueado por completo.
        var client = await ClienteAutenticadoAsync();

        var resposta = await client.PostAsJsonAsync(
            "/api/v1/tutores",
            Corpo("90100000002", "i2b@teste.com", "+14155550100"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_NrTelefoneComDdiExplicitoAbaixoDoPiso_Retorna400()
    {
        // Achado Minor #6 (G2): "+1" gravava "1" (1 dígito) — "nunca grava lixo" não cumpria.
        var client = await ClienteAutenticadoAsync();

        var resposta = await client.PostAsJsonAsync(
            "/api/v1/tutores",
            Corpo("90100000003", "i2c@teste.com", null, nrTelefone: "+1"));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
