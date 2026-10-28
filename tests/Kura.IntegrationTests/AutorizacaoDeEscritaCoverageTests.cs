namespace Kura.IntegrationTests;

using System.Reflection;
using FluentAssertions;
using Kura.Api.Controllers;
using Kura.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;

/// <summary>
/// R-3 da revisão G2 da FD-15 — trava por reflection a propriedade que a FD-15
/// <b>deliberadamente abriu mão</b>.
///
/// <para>
/// <b>Por que existe, e por que admitir no doc-comment não bastava.</b> Antes da FD-15 a
/// política <c>SomenteGestor</c> morava no <b>controller</b> de
/// <see cref="ServicosPrecoController"/>, e isso dava uma garantia estrutural: endpoint novo
/// <b>nascia protegido</b>, e desproteger exigia escrever algo de propósito. A ruling D-13
/// obrigou a inverter (controller <c>[Authorize]</c> simples, política em cada verbo de
/// escrita) porque atributos <c>[Authorize]</c> de controller e de método <b>se somam</b> e
/// nunca se sobrepõem — não havia como afrouxar só os <c>GET</c> mantendo a política na
/// classe. A inversão é correta e é a única que funciona; o que ela cobra é <b>esta</b>
/// garantia: um verbo de escrita novo que esqueça o atributo nasce <b>aberto a qualquer
/// autenticado</b>, respondendo <c>200</c> em silêncio.
/// </para>
///
/// <para>
/// 🔴 <b>Medido por mutação na revisão G2, não suposto.</b> Acrescentando ao
/// <see cref="ServicosPrecoController"/> um <c>[HttpPost("{id}/duplicacao")]</c> sem o
/// atributo — um verbo que remarca a tabela de preços, aberto a qualquer veterinário — os
/// <b>139 testes HTTP existentes continuaram VERDES</b>. Nenhum instrumento da suíte
/// enxergava o buraco. É a regra de ouro v7 do projeto (<i>inventário escrito à mão apodrece
/// em silêncio</i>) e a regra do alcance do detector (<i>"achou 0" é afirmação sobre o
/// instrumento, não prova de ausência</i>) na mesma mutação.
/// </para>
///
/// <para>
/// Mesmo padrão de <see cref="ConvencaoDeTestesCoverageTests"/> e de
/// <c>TenantFilterCoverageTests</c> (em <c>Kura.Infrastructure.Tests</c>): derivar a lista
/// <b>do assembly</b> em vez de mantê-la à mão.
/// </para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class AutorizacaoDeEscritaCoverageTests
{
    private static readonly string[] VerbosDeEscrita = ["POST", "PUT", "PATCH", "DELETE"];

    private static List<MethodInfo> VerbosDeEscritaDe(Type controller) =>
        controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>(inherit: true)
                         .SelectMany(a => a.HttpMethods)
                         .Any(v => VerbosDeEscrita.Contains(v)))
            .ToList();

    [Fact]
    public void Todo_verbo_de_ESCRITA_de_ServicosPrecoController_declara_SomenteGestor()
    {
        var escrita = VerbosDeEscritaDe(typeof(ServicosPrecoController));

        // Controle positivo — e é `>=`, não `==`, DE PROPÓSITO. Com `HaveCount(4)` um quinto
        // verbo CORRETAMENTE protegido derrubaria o teste por cardinalidade, e a lição que o
        // mantenedor aprenderia seria "ajuste o número" — exatamente o reflexo que faria ele
        // ajustar o número no dia em que o verbo novo estivesse ERRADO. O que precisa falhar
        // é a ausência do atributo, nunca a contagem.
        escrita.Should().HaveCountGreaterThanOrEqualTo(
            4,
            "Criar, Atualizar, Reativar e Desativar são os 4 verbos de escrita da FD-09/FD-15");

        escrita
            .Where(m => !m.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                          .Any(a => a.Policy == PoliticasAutorizacao.SomenteGestor))
            .Select(m => m.Name)
            .Should()
            .BeEmpty(
                "a FD-15 tirou SomenteGestor do CONTROLLER (ruling D-13, para os 2 GET "
                + "ficarem legíveis ao veterinário): um verbo de escrita sem o atributo "
                + "nasce ABERTO a qualquer autenticado e responde 200 em silêncio — preço "
                + "é decisão comercial, e quem remarca a tabela é o GESTOR");
    }

    [Fact]
    public void Os_dois_GET_de_ServicosPrecoController_continuam_exigindo_autenticacao()
    {
        // A outra metade da ruling D-13, e ela também precisa de trava: "catálogo
        // operacional" significa QUALQUER AUTENTICADO DA CLÍNICA, nunca anônimo.
        // ⚠️ Um [AllowAnonymous] aqui não deixaria a rota de fato pública hoje (o
        // ClinicaContext lança por falta da claim clinicaId e o middleware devolve 401 assim
        // mesmo), mas passaria a barreira de DECLARADA para acidente de implementação — some
        // no dia em que um caminho de leitura novo não tocar o contexto de clínica.
        typeof(ServicosPrecoController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any())
            .Select(m => m.Name)
            .Should()
            .BeEmpty("nenhum endpoint da tabela de preços é anônimo — ver o doc-comment do controller");

        typeof(ServicosPrecoController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Should()
            .NotBeEmpty("o controller inteiro exige autenticação; a FD-15 afrouxou o PAPEL, não o login");
    }

    [Fact]
    public void Todo_verbo_de_UsuariosClinicaController_continua_sob_SomenteGestor()
    {
        // Contraste deliberado com o teste acima: a FD-15 é SÓ de servicos-preco. Este
        // controller mantém a política na CLASSE, e este teste falha se alguém "uniformizar"
        // os dois controllers por simetria estética — que seria abrir o CRUD de identidade
        // (e-mail, papel, senha) a qualquer autenticado.
        typeof(UsuariosClinicaController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Should()
            .Contain(
                a => a.Policy == PoliticasAutorizacao.SomenteGestor,
                "usuarios-clinica não foi tocado pela D-13 — só a tabela de preços virou catálogo operacional");
    }

    [Fact]
    public void LaudosController_ExcluirExigeSomenteGestor_ECriarContinuaAbertoAoVeterinario()
    {
        // S4-NET-03: GESTOR e VETERINARIO enviam laudo; so o GESTOR exclui. A politica mora no
        // METODO (o controller e [Authorize] simples), entao esquecer o atributo num DELETE novo
        // o deixaria aberto a qualquer autenticado - mesmo risco da FD-15.
        var excluir = typeof(LaudosController).GetMethod(nameof(LaudosController.Excluir))!;
        excluir.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Should().Contain(a => a.Policy == PoliticasAutorizacao.SomenteGestor);

        VerbosDeEscritaDe(typeof(LaudosController))
            .Where(m => m.Name != nameof(LaudosController.Criar))
            .Where(m => !m.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                          .Any(a => a.Policy == PoliticasAutorizacao.SomenteGestor))
            .Select(m => m.Name)
            .Should().BeEmpty("todo verbo de escrita de laudo, exceto o envio, e do GESTOR");

        VerbosDeEscritaDe(typeof(LaudosController)).Should().Contain(m => m.Name == nameof(LaudosController.Criar));
    }
}
