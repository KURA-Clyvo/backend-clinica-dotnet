namespace Kura.Api.Controllers;

using Kura.Api.Extensions;
using Kura.Application.DTOs.Financeiro;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// FD-11 — os KPI financeiros da clínica. É a tela que o gestor abre; a FD-09 deu à clínica
/// uma tabela de preços e a FD-10 pendurou o lançamento no atendimento, mas até aqui ninguém
/// conseguia responder "quanto entrou este mês".
///
/// <para>
/// 🔴 <b>UM endpoint, não quatro, e a razão é de correção.</b> Os 4 KPI saem na
/// <b>mesma</b> resposta, calculados sobre a <b>mesma</b> lista de cobranças. Quatro rotas
/// separadas sobre "o mesmo" período seriam quatro leituras em quatro instantes: uma cobrança
/// lançada no meio faz os cards do app <b>discordarem entre si</b> — receita de um instante,
/// ticket de outro — sem que nenhum deles esteja errado isoladamente, que é o pior formato
/// possível para um bug de relatório.
/// </para>
///
/// <para>
/// 🔴 <b><c>[Authorize(Policy = SomenteGestor)]</c> está no CONTROLLER</b> — molde da FD-09
/// (<c>ServicosPrecoController</c>) e de <c>UsuariosClinicaController</c>: um endpoint novo
/// acrescentado aqui nasce protegido, e desproteger passa a exigir escrever
/// <c>[AllowAnonymous]</c> de propósito. ⚠️ A exceção de autorização <b>mista</b> de
/// <c>CobrancasController</c> (escrita aberta ao veterinário, leitura só do gestor) <b>não se
/// aplica</b> aqui: neste controller tudo é leitura agregada, que é exatamente o que a ruling
/// D-7 restringe ao gestor. Não há gesto de veterinário nenhum a proteger.
/// </para>
///
/// <para>
/// 🔴 <b>Nenhum parâmetro aceita <c>IdClinica</c>.</b> O escopo sai do <c>clinicaId</c> do JWT
/// dentro do service, com o predicado de tenant escrito à mão (o query filter <b>desliga
/// inteiro</b> quando não há JWT, então depender dele seria fazer o resultado variar com o
/// contexto). Num relatório agregado o erro custa mais do que numa leitura de linha: um filtro
/// desligado devolve o faturamento inteiro do concorrente numa chamada, e o número
/// resultante <b>parece plausível</b>.
/// </para>
///
/// <para>
/// ⛔ <b>Escopo negativo (D-1/D-6):</b> sem imposto, repasse ou margem — o campo é
/// <c>receitaBruta</c>, com essas palavras. Sem gateway, status de pagamento, projeção ou
/// previsão. Sem mix por veterinário, sem estoque, sem exportação própria do .NET (as duas
/// rotas montadas pelo package Oracle, S4-DB-06, são a única exceção e só devolvem JSON pronto).
/// </para>
/// </summary>
[Authorize(Policy = PoliticasAutorizacao.SomenteGestor)]
[ApiController]
[Route("api/v1/financeiro")]
public class FinanceiroController : ControllerBase
{
    private readonly IFinanceiroService _service;
    private readonly ICobrancaOracleService _oracle;

    public FinanceiroController(IFinanceiroService service, ICobrancaOracleService oracle)
    {
        _service = service;
        _oracle = oracle;
    }

    /// <summary>
    /// Exportação das cobranças ativas da clínica do token em JSON, <b>montada pelo package
    /// Oracle</b> <c>PKG_COBRANCA.LISTAR_JSON</c> (S4-DB-06). Devolve um array (<c>[]</c> se a
    /// clínica não tem cobrança). O escopo é sempre a clínica do token: não há parâmetro de
    /// clínica. <c>404</c> se a clínica do token não existe ou está inativa; <c>503</c> se a
    /// rotina Oracle está indisponível.
    /// </summary>
    [HttpGet("cobrancas/exportacao-json")]
    [ProducesResponseType(typeof(System.Text.Json.JsonElement), 200, "application/json")]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(503)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ExportarCobrancasJson(CancellationToken ct) =>
        Content(await _oracle.ExportarJsonAsync(ct), "application/json");

    /// <summary>
    /// Relatório de cobranças por forma de pagamento da clínica do token, <b>montado pelo
    /// package Oracle</b> <c>PKG_COBRANCA.RELATORIO_JSON</c> (S4-DB-06): objeto com
    /// <c>grupos</c> (forma e total) e <c>subTotal</c>. O escopo é sempre a clínica do token.
    /// <c>404</c> se a clínica do token não existe ou está inativa; <c>503</c> se a rotina
    /// Oracle está indisponível.
    /// </summary>
    [HttpGet("cobrancas/relatorio-oracle")]
    [ProducesResponseType(typeof(System.Text.Json.JsonElement), 200, "application/json")]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(503)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> RelatorioCobrancasOracle(CancellationToken ct) =>
        Content(await _oracle.RelatorioJsonAsync(ct), "application/json");

    /// <summary>
    /// Resumo financeiro da clínica do token no período: <b>receita bruta</b>, <b>ticket
    /// médio</b>, <b>mix por serviço</b> e <b>comparação com o período anterior</b>.
    ///
    /// <para>
    /// 🔴 <b><c>de</c> e <c>ate</c> são OBRIGATÓRIOS e INCLUSIVOS</b>, no formato
    /// <c>YYYY-MM-DD</c> (data, sem hora). Não há default de servidor: um cliente que
    /// esquecesse o período receberia <c>200</c> com números plausíveis de <b>outro</b>
    /// período, que é o defeito que este ciclo persegue. <c>de == ate</c> é válido (relatório
    /// de um dia).
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>A borda superior conta INTEIRA.</b> O servidor converte as duas datas para o
    /// intervalo semiaberto <c>[de 00:00, ate+1d 00:00)</c>, então uma cobrança das
    /// <c>23:59</c> do dia <c>ate</c> <b>entra</b> no relatório. A resposta devolve o
    /// intervalo que de fato usou (<c>periodo.inicioUtc</c> / <c>periodo.fimExclusivoUtc</c>)
    /// para que o app confira em vez de acreditar.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>FUSO: UTC, sem conversão — limitação DECLARADA, não esquecida.</b>
    /// <c>DT_COBRANCA</c> é gravada em UTC (FD-10) e o filtro compara em UTC. Consequência
    /// para uma clínica em <c>America/Sao_Paulo</c>: o "dia" deste relatório é o dia
    /// <b>UTC</b> — as 3 primeiras horas de cada dia local caem no dia anterior do relatório.
    /// A convenção de fuso de exibição do projeto é item em aberto e deliberadamente <b>não
    /// nasce aqui</b>.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>A agregação é por <c>DT_COBRANCA</c> — a data do FATO, nunca a data de criação da
    /// linha.</b> As duas divergem no caso real que a FD-10 aceita: o fechamento do dia
    /// anterior lançado na manhã seguinte. ⚠️ <b>Limite herdado e declarado</b> (revisão G2 da
    /// FD-10): o validator de lançamento aceita <c>dtCobranca</c> até <b>+1 dia no futuro</b>,
    /// para absorver fuso de cliente — então uma cobrança lançada em <b>31/01</b> no limite da
    /// tolerância cai no balde de <b>fevereiro</b>. É comportamento declarado, não bug: a
    /// tolerância existe para não recusar o "agora" de um cliente que serializa sem offset.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Duas divisões, e as duas devolvem <c>null</c> em vez de <c>0</c>:</b>
    /// <c>ticketMedio</c> é <c>null</c> quando não houve atendimento cobrado, e
    /// <c>variacaoPercentual</c> é <c>null</c> quando a receita do período anterior é zero
    /// (crescer do zero não tem porcentagem). Os números crus
    /// (<c>receitaBrutaPeriodoAnterior</c>, <c>nrAtendimentosCobrados</c>) vão na resposta de
    /// qualquer jeito, para o app dizer algo honesto com a porcentagem nula. <b>Zero para
    /// "não medimos" seria mentira.</b>
    /// </para>
    ///
    /// <para>
    /// <b>Arredondamento declarado:</b> <c>receitaBruta</c> e as receitas do mix são
    /// <b>somas exatas</b>, não arredondadas (cada parcela já é <c>NUMBER(10,2)</c>);
    /// <c>ticketMedio</c> e <c>variacaoPercentual</c> saem com <b>2 casas</b>,
    /// <c>MidpointRounding.AwayFromZero</c> — e não o <c>ToEven</c> padrão do .NET, que
    /// surpreenderia quem confere na calculadora.
    /// </para>
    ///
    /// <para>
    /// <b>O mix RECONCILIA:</b> a soma das receitas dos baldes é igual à
    /// <c>receitaBruta</c>, exata. Lançamento avulso (sem serviço tabelado, D-2) tem balde
    /// próprio com <c>idServicoPreco: null</c>, e serviço <b>desativado</b> continua no mix
    /// com o nome dele — a receita aconteceu, desativar o item do catálogo depois não a
    /// desfaz.
    /// </para>
    /// </summary>
    /// <response code="200">Resumo do período. Período sem nenhuma cobrança devolve 200 com estado vazio declarado (<c>receitaBruta: 0</c>, <c>ticketMedio: null</c>, <c>mixPorServico: []</c>) — nunca 404.</response>
    /// <response code="400">Período ausente, mal formado (<c>?de=ontem</c>), ou invertido (<c>de &gt; ate</c>).</response>
    /// <response code="401">Sem token, ou token inválido/expirado (inclui token de GESTOR sem a claim <c>clinicaId</c>, que degrada fechado).</response>
    /// <response code="403">Token válido cujo perfil não é GESTOR (inclui token pré-FD-03, sem a claim <c>perfil</c>).</response>
    [HttpGet("resumo")]
    [ProducesResponseType(typeof(ResumoFinanceiroResponseDto), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> ObterResumo([FromQuery] ResumoFinanceiroQueryDto filtro) =>
        // O `!` é seguro por construção: ResumoFinanceiroQueryValidator recusa com 400 o
        // filtro sem `de` ou sem `ate`, e o auto-validation do FluentValidation roda antes
        // desta linha. Está travado por teste HTTP (parâmetro ausente -> 400), e não por
        // leitura: se o validator sumir, o teste cai antes de qualquer NullReference.
        Ok(await _service.ObterResumoAsync(filtro.De!.Value, filtro.Ate!.Value));
}
