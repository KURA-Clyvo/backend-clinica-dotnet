namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Kura.Application.DTOs.Agenda;
using Kura.Domain.Entities;
using Kura.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// FD-06 (ciclo FIN) — <b>máquina de estados de <c>PATCH /api/v1/agendamentos/{id}/status</c></b>,
/// exercitada sobre HTTP real.
///
/// <para>
/// 🔴 <b>A mordida da task:</b> <c>AtualizarStatusAgendamentoValidator</c> aceitava apenas
/// <c>REALIZADO</c> e <c>CANCELADO</c>, enquanto o <c>CHECK</c> do Oracle
/// (<c>V1__initial_schema.sql:283</c>) e o enum <c>StatusAgendamento</c> do backend Java já
/// aceitavam os <b>seis</b> valores. Marcar falta (<c>NAO_COMPARECEU</c>) ou confirmar
/// (<c>CONFIRMADO</c>) devolvia <b>400</b> — e o <c>mobile-clinica-rn</c> já <b>lê</b>
/// <c>NAO_COMPARECEU</c> (<c>agenda.service.ts:51</c>) sem que nada no ecossistema o
/// escrevesse.
/// </para>
///
/// <para>
/// 🔴 <b>Por que estes cenários vivem em HTTP e não só no service.</b> O <b>400</b> de hoje
/// nasce no pipeline de validação do ASP.NET Core (FluentValidation), <b>antes</b> de
/// <c>AgendaService</c> ser chamado. Um teste de service não consegue observá-lo: ele
/// instancia o service direto e pula o validator inteiro. Só a requisição real distingue
/// «o validator recusou o valor» (400) de «a regra de negócio recusou a transição» (422).
/// </para>
///
/// <para>
/// ⚠️ <b>Host PRÓPRIO (<c>IClassFixture</c>), e não a <see cref="ColecaoDeIntegracao"/>.</b>
/// Esta classe <b>escreve</b> status em agendamentos semeados por ela mesma. O banco InMemory
/// é compartilhado por todas as classes de uma mesma collection; um agendamento a mais no
/// banco da collection mudaria o que <see cref="FluxoDeNegocioHttpTests"/> vê. Mesmo
/// raciocínio de <see cref="VeterinariosTenantHttpTests"/>.
/// </para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class AgendamentoStatusHttpTests : IClassFixture<KuraApiFactory>
{
    private readonly KuraApiFactory _factory;

    public AgendamentoStatusHttpTests(KuraApiFactory factory) => _factory = factory;

    private async Task<HttpClient> ClienteAutenticadoAsync()
    {
        var client = _factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));
        return client;
    }

    /// <summary>
    /// REC-11 — hora local de SP (mesma convenção de <c>IRelogioClinica</c>/A-5), não
    /// <c>DateTime.UtcNow</c> cru. Antes da guarda de falta da REC-11 essa diferença de ~3h era
    /// invisível aqui (nada comparava <c>DtAgendamento</c> com o relógio); com a guarda,
    /// semear <c>UtcNow.AddHours(-1)</c> passou a ficar NO FUTURO em relação ao "agora" de SP
    /// real, e <c>Marcar_falta_em_agendamento_AGENDADO_...</c> (que pede NAO_COMPARECEU) virava
    /// 422 em vez de 200 — mordida real da própria REC-11 contra um teste pré-existente.
    /// </summary>
    private static DateTime AgoraSp()
    {
        var sp = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, sp);
    }

    /// <summary>
    /// Semeia um agendamento na clínica do token. <c>Agendamento</c> NÃO tem
    /// <c>HasQueryFilter</c> — o escopo de tenant dele é manual, em
    /// <c>AgendaService</c>/<c>AgendamentoRepository</c>; por isso a semeadura enxerga tudo.
    /// </summary>
    private async Task SemearAgendamentoAsync(long id, string status, long nrVersion = 0)
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();

        db.Agendamentos.Add(new Agendamento
        {
            Id = id,
            IdClinica = KuraApiFactory.IdClinicaSemeada,
            IdVeterinario = KuraApiFactory.IdVeterinarioSemeado,
            DtAgendamento = AgoraSp().AddHours(-1),
            NrDuracaoMinutos = 30,
            DsTipoConsulta = "CONSULTA",
            StStatus = status,
            NrVersion = nrVersion,
            StAtiva = true,
        });

        await db.SaveChangesAsync();
    }

    private async Task<string?> LerStatusPersistidoAsync(long id)
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();

        var agendamento = await db.Agendamentos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        return agendamento?.StStatus;
    }

    private static Task<HttpResponseMessage> PatchStatusAsync(
        HttpClient client, long id, string status, long nrVersion)
        => client.PatchAsJsonAsync(
            $"/api/v1/agendamentos/{id}/status",
            new { dsStatus = status, nrVersion });

    // ───────────────────────────────────────────────────────────────────────────────
    // 1. A MORDIDA CENTRAL — 400 antes do fix, 200 depois, com o valor persistido.
    // ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 🔴 <b>A prova de mordida da FD-06.</b> Antes do fix esta requisição devolve <b>400</b>
    /// («'DsStatus' deve ser REALIZADO ou CANCELADO.»); depois devolve <b>200</b> e o valor
    /// fica no banco.
    /// </summary>
    [Fact]
    public async Task Marcar_falta_em_agendamento_AGENDADO_devolve_200_e_persiste_NAO_COMPARECEU()
    {
        // Arrange
        const long id = 9101;
        await SemearAgendamentoAsync(id, "AGENDADO", nrVersion: 0);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await PatchStatusAsync(client, id, "NAO_COMPARECEU", nrVersion: 0);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpo = await resposta.Content.ReadFromJsonAsync<AgendamentoItemDto>();
        corpo.Should().NotBeNull();
        corpo!.DsStatus.Should().Be("NAO_COMPARECEU");
        corpo.NrVersion.Should().Be(1, "o optimistic locking incrementa a versão");

        // Persistência: a resposta poderia estar certa e o banco errado.
        (await LerStatusPersistidoAsync(id)).Should().Be("NAO_COMPARECEU");
    }

    /// <summary>
    /// Segundo valor liberado pela D-5. O Java só confirma a partir de <c>AGENDADO</c>
    /// (<c>Agendamento.java:126-130</c>) e o <c>.NET</c> passa a dizer o mesmo.
    /// </summary>
    [Fact]
    public async Task Confirmar_agendamento_AGENDADO_devolve_200_e_persiste_CONFIRMADO()
    {
        // Arrange
        const long id = 9102;
        await SemearAgendamentoAsync(id, "AGENDADO", nrVersion: 3);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await PatchStatusAsync(client, id, "CONFIRMADO", nrVersion: 3);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LerStatusPersistidoAsync(id)).Should().Be("CONFIRMADO");
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // 2. NAO_COMPARECEU É TERMINAL — o risco que o backlog nomeou.
    // ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 🔴 <b>O risco que a FD-06 cria e tem de fechar no mesmo commit.</b> Com o validator
    /// aceitando <c>NAO_COMPARECEU</c> e <c>StatusFinais</c> SEM ele, um agendamento marcado
    /// como falta viraria <c>REALIZADO</c> depois — <b>dado falso com cara de dado certo</b>,
    /// e é sobre esse dado que a trilha financeira do ciclo FIN vai faturar.
    ///
    /// <para>Este teste falha das <b>duas</b> maneiras erradas: sem o fix do validator o
    /// agendamento nem chega a ficar em <c>NAO_COMPARECEU</c>; sem o fix de
    /// <c>StatusFinais</c> ele chega e <b>aceita</b> virar <c>REALIZADO</c> com 200.</para>
    /// </summary>
    [Fact]
    public async Task Agendamento_em_NAO_COMPARECEU_nao_pode_virar_REALIZADO()
    {
        // Arrange
        const long id = 9103;
        await SemearAgendamentoAsync(id, "NAO_COMPARECEU", nrVersion: 0);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await PatchStatusAsync(client, id, "REALIZADO", nrVersion: 0);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "NAO_COMPARECEU é estado final: reescrevê-lo apaga o registro da falta");
        (await LerStatusPersistidoAsync(id)).Should().Be("NAO_COMPARECEU");
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // 3. TRANSIÇÕES RECUSADAS — sem elas, StatusFinais seria a única regra.
    // ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 🔴 Sem a máquina de estados, <c>INTENCAO</c> (lead que nunca virou agendamento) pularia
    /// direto para <c>REALIZADO</c> com 200 — atendimento faturável nascido de um estado que
    /// nunca foi agendado.
    /// </summary>
    [Fact]
    public async Task Agendamento_em_INTENCAO_nao_pode_pular_direto_para_REALIZADO()
    {
        // Arrange
        const long id = 9104;
        await SemearAgendamentoAsync(id, "INTENCAO", nrVersion: 0);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await PatchStatusAsync(client, id, "REALIZADO", nrVersion: 0);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerStatusPersistidoAsync(id)).Should().Be("INTENCAO");
    }

    /// <summary>
    /// Confirmar o que já está confirmado é recusado — o Java exige status <b>exatamente</b>
    /// <c>AGENDADO</c> em <c>confirmar()</c>, e os dois donos da tabela compartilhada dizem a
    /// mesma coisa.
    /// </summary>
    [Fact]
    public async Task Confirmar_agendamento_ja_CONFIRMADO_devolve_422()
    {
        // Arrange
        const long id = 9105;
        await SemearAgendamentoAsync(id, "CONFIRMADO", nrVersion: 0);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await PatchStatusAsync(client, id, "CONFIRMADO", nrVersion: 0);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerStatusPersistidoAsync(id)).Should().Be("CONFIRMADO");
    }

    /// <summary>
    /// <c>INTENCAO</c> ainda pode ser cancelado — o lead que não vira agendamento morre como
    /// <c>CANCELADO</c>, exatamente como o <c>cancelar()</c> do Java permite. Este teste existe
    /// para que a máquina de estados não seja lida como «tudo que não é AGENDADO é recusado».
    /// </summary>
    [Fact]
    public async Task Agendamento_em_INTENCAO_pode_ser_CANCELADO()
    {
        // Arrange
        const long id = 9106;
        await SemearAgendamentoAsync(id, "INTENCAO", nrVersion: 0);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await PatchStatusAsync(client, id, "CANCELADO", nrVersion: 0);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LerStatusPersistidoAsync(id)).Should().Be("CANCELADO");
    }

    /// <summary>
    /// Controle de que o validator continua fechado: um valor que o <c>CHECK</c> do Oracle
    /// rejeitaria não pode passar. Sem este caso, «aceitar os quatro» degradaria em «aceitar
    /// qualquer string» sem ninguém perceber.
    /// </summary>
    [Fact]
    public async Task Status_fora_do_CHECK_do_Oracle_continua_devolvendo_400()
    {
        // Arrange
        const long id = 9107;
        await SemearAgendamentoAsync(id, "AGENDADO", nrVersion: 0);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await PatchStatusAsync(client, id, "FATURADO", nrVersion: 0);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LerStatusPersistidoAsync(id)).Should().Be("AGENDADO");
    }

    /// <summary>
    /// <c>INTENCAO</c> e <c>AGENDADO</c> são estados de <b>partida</b>, nunca de destino: o
    /// <c>.NET</c> só faz o agendamento avançar. Sem esta recusa, um agendamento já realizado
    /// poderia ser «desfeito» de volta para agendado por quem mandasse o valor certo.
    /// </summary>
    [Fact]
    public async Task Voltar_agendamento_CONFIRMADO_para_AGENDADO_devolve_400()
    {
        // Arrange
        const long id = 9108;
        await SemearAgendamentoAsync(id, "CONFIRMADO", nrVersion: 0);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await PatchStatusAsync(client, id, "AGENDADO", nrVersion: 0);

        // Assert — o validator barra antes da máquina de estados: AGENDADO não é destino
        // válido para NENHUMA origem, então ele nem chega ao service.
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LerStatusPersistidoAsync(id)).Should().Be("CONFIRMADO");
    }
}
