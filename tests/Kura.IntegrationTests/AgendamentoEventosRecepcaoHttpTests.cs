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
/// REC-11 — <c>POST /api/v1/agendamentos/{id}/checkin</c>,
/// <c>POST /api/v1/agendamentos/{id}/inicio-atendimento</c> e a guarda de falta de
/// <c>PATCH .../status</c>, exercitados sobre HTTP real.
///
/// <para>
/// 🔴 <b>Por que estes endpoints precisam de teste HTTP e não só de service.</b> A REC-10 mediu
/// (G2/m-4) que um endpoint SEM teste HTTP aceita rota e status trocados sem que a suíte
/// perceba — mutar <c>/api/v1/agendamentos</c> para <c>-x</c> e o status de <c>201</c> para
/// <c>200</c> deixava a suíte inteira verde. Esta classe cobre a mesma classe de mordida para
/// os 2 endpoints novos da REC-11: rota exata (controle: rota errada continua <c>404</c>) e
/// status exato (<c>200</c>, não <c>201</c> — estes endpoints não CRIAM recurso, só atualizam
/// timestamps de um agendamento existente).
/// </para>
///
/// <para>
/// ⚠️ <b>Host PRÓPRIO (<see cref="IClassFixture{TFixture}"/>), não a
/// <see cref="ColecaoDeIntegracao"/></b> — mesmo raciocínio de <see cref="AgendamentoStatusHttpTests"/>
/// e <see cref="AgendamentoCriarHttpTests"/>: esta classe ESCREVE em agendamentos semeados por
/// ela mesma, e o InMemory de uma <c>ColecaoDeIntegracao</c> é compartilhado entre classes.
/// </para>
///
/// <para>
/// <b>Relógio:</b> nenhuma fábrica de teste substitui <see cref="Kura.Domain.Interfaces.IRelogioClinica"/> —
/// o host real usa <c>TimeProvider.System</c> (hora local de SP de verdade). A guarda de falta
/// (antes/depois do horário marcado) é provada aqui com margem folgada (minutos, não segundos) em
/// vez do boundary exato de 1 minuto — o boundary exato (9:59 recusa, 10:01 aceita) já está
/// coberto com relógio controlado em <c>AgendaServiceTests</c> (unitário, sem HTTP).
/// </para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class AgendamentoEventosRecepcaoHttpTests : IClassFixture<KuraApiFactory>
{
    private readonly KuraApiFactory _factory;

    public AgendamentoEventosRecepcaoHttpTests(KuraApiFactory factory) => _factory = factory;

    private async Task<HttpClient> ClienteAutenticadoAsync()
    {
        var client = _factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));
        return client;
    }

    private static DateTime AgoraSp()
    {
        var sp = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, sp);
    }

    private async Task SemearAgendamentoAsync(
        long id,
        string status,
        long nrVersion = 0,
        DateTime? dtAgendamento = null,
        DateTime? dtCheckin = null,
        long idClinica = KuraApiFactory.IdClinicaSemeada)
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();

        db.Agendamentos.Add(new Agendamento
        {
            Id = id,
            IdClinica = idClinica,
            IdVeterinario = KuraApiFactory.IdVeterinarioSemeado,
            DtAgendamento = dtAgendamento ?? AgoraSp().AddMinutes(-30),
            NrDuracaoMinutos = 30,
            DsTipoConsulta = "CONSULTA",
            StStatus = status,
            NrVersion = nrVersion,
            DtCheckin = dtCheckin,
            StAtiva = true,
        });

        await db.SaveChangesAsync();
    }

    private async Task<Agendamento?> LerAgendamentoAsync(long id)
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();
        return await db.Agendamentos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
    }

    private static Task<HttpResponseMessage> CheckinAsync(HttpClient client, long id, long nrVersion)
        => client.PostAsJsonAsync($"/api/v1/agendamentos/{id}/checkin", new { nrVersion });

    private static Task<HttpResponseMessage> IniciarAtendimentoAsync(HttpClient client, long id, long nrVersion)
        => client.PostAsJsonAsync($"/api/v1/agendamentos/{id}/inicio-atendimento", new { nrVersion });

    private static Task<HttpResponseMessage> MarcarFaltaAsync(HttpClient client, long id, long nrVersion)
        => client.PatchAsJsonAsync($"/api/v1/agendamentos/{id}/status", new { dsStatus = "NAO_COMPARECEU", nrVersion });

    // ───────────────────────────────────────────────────────────────────────────────
    // CHECK-IN — rota/status exatos, mordida central.
    // ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Checkin_ComPayloadValido_Devolve200EGravaDtCheckinDoRelogio()
    {
        // Arrange
        const long id = 9201;
        var antes = AgoraSp();
        await SemearAgendamentoAsync(id, "AGENDADO", nrVersion: 0);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await CheckinAsync(client, id, nrVersion: 0);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpo = await resposta.Content.ReadFromJsonAsync<AgendamentoItemDto>();
        corpo.Should().NotBeNull();
        corpo!.DtCheckin.Should().NotBeNull();
        corpo.DtCheckin!.Value.Should().BeOnOrAfter(antes).And.BeOnOrBefore(AgoraSp().AddMinutes(1));
        corpo.NrVersion.Should().Be(1, "check-in incrementa NrVersion");
        corpo.DsEtapaRecepcao.Should().Be("CHEGOU");
        corpo.DsStatus.Should().Be("AGENDADO", "check-in NÃO muda ST_STATUS (A-2)");

        var persistido = await LerAgendamentoAsync(id);
        persistido!.DtCheckin.Should().Be(corpo.DtCheckin);
        persistido.NrVersion.Should().Be(1);
    }

    /// <summary>Controle da mordida: uma rota que NÃO é a real continua <c>404</c>.</summary>
    [Fact]
    public async Task Checkin_RotaInexistente_Devolve404()
    {
        // Arrange
        const long id = 9202;
        await SemearAgendamentoAsync(id, "AGENDADO", nrVersion: 0);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await client.PostAsJsonAsync($"/api/v1/agendamentos/{id}/checkin-x", new { nrVersion = 0 });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(92031L, "CANCELADO")]
    [InlineData(92032L, "REALIZADO")]
    [InlineData(92033L, "NAO_COMPARECEU")]
    public async Task Checkin_EmStatusNaoElegivel_Devolve422SemMudarALinha(long id, string status)
    {
        // Arrange
        await SemearAgendamentoAsync(id, status, nrVersion: 0);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await CheckinAsync(client, id, nrVersion: 0);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerAgendamentoAsync(id))!.DtCheckin.Should().BeNull();
    }

    [Fact]
    public async Task Checkin_VersaoDesatualizada_Devolve409SemMudarALinha()
    {
        // Arrange
        const long id = 9204;
        await SemearAgendamentoAsync(id, "AGENDADO", nrVersion: 5);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await CheckinAsync(client, id, nrVersion: 3);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var persistido = await LerAgendamentoAsync(id);
        persistido!.DtCheckin.Should().BeNull();
        persistido.NrVersion.Should().Be(5);
    }

    [Fact]
    public async Task Checkin_Idempotente_SegundaChamadaDevolveMesmoHorarioSemIncrementarVersao()
    {
        // Arrange
        const long id = 9205;
        await SemearAgendamentoAsync(id, "AGENDADO", nrVersion: 0);
        var client = await ClienteAutenticadoAsync();

        // Act — 1ª chamada grava; 2ª chamada (versão que a 1ª devolveu) é idempotente.
        var resposta1 = await CheckinAsync(client, id, nrVersion: 0);
        var corpo1 = await resposta1.Content.ReadFromJsonAsync<AgendamentoItemDto>();
        var resposta2 = await CheckinAsync(client, id, nrVersion: corpo1!.NrVersion);
        var corpo2 = await resposta2.Content.ReadFromJsonAsync<AgendamentoItemDto>();

        // Assert
        resposta2.StatusCode.Should().Be(HttpStatusCode.OK);
        corpo2!.DtCheckin.Should().Be(corpo1.DtCheckin, "2º check-in devolve o horário do 1º");
        corpo2.NrVersion.Should().Be(corpo1.NrVersion, "chamada idempotente não incrementa a versão");

        (await LerAgendamentoAsync(id))!.NrVersion.Should().Be(corpo1.NrVersion);
    }

    [Fact]
    public async Task Checkin_AgendamentoDeOutraClinica_Devolve404()
    {
        // Arrange -- A-7: agendamento existe, mas em outra clínica. Resposta idêntica ao inexistente.
        const long id = 9206;
        await SemearAgendamentoAsync(id, "AGENDADO", nrVersion: 0, idClinica: KuraApiFactory.IdClinicaOutroTenant);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await CheckinAsync(client, id, nrVersion: 0);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // INÍCIO DE ATENDIMENTO — rota/status exatos + "não inventa DtCheckin".
    // ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IniciarAtendimento_SemCheckinPrevio_Devolve200ENaoGravaDtCheckin()
    {
        // Arrange -- walk-in: entra direto, sem check-in.
        const long id = 9211;
        var antes = AgoraSp();
        await SemearAgendamentoAsync(id, "AGENDADO", nrVersion: 0);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await IniciarAtendimentoAsync(client, id, nrVersion: 0);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpo = await resposta.Content.ReadFromJsonAsync<AgendamentoItemDto>();
        corpo!.DtInicioAtendimento.Should().NotBeNull();
        corpo.DtInicioAtendimento!.Value.Should().BeOnOrAfter(antes).And.BeOnOrBefore(AgoraSp().AddMinutes(1));
        corpo.DtCheckin.Should().BeNull("início sem check-in prévio não pode inventar DtCheckin");
        corpo.NrVersion.Should().Be(1);
        corpo.DsEtapaRecepcao.Should().Be("EM_ATENDIMENTO");

        var persistido = await LerAgendamentoAsync(id);
        persistido!.DtCheckin.Should().BeNull();
        persistido.DtInicioAtendimento.Should().Be(corpo.DtInicioAtendimento);
    }

    /// <summary>Controle da mordida: rota errada continua <c>404</c>.</summary>
    [Fact]
    public async Task IniciarAtendimento_RotaInexistente_Devolve404()
    {
        // Arrange
        const long id = 9212;
        await SemearAgendamentoAsync(id, "AGENDADO", nrVersion: 0);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await client.PostAsJsonAsync($"/api/v1/agendamentos/{id}/inicio-atendimento-x", new { nrVersion = 0 });

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task IniciarAtendimento_ComCheckinPrevio_MantemDtCheckinEGravaInicio()
    {
        // Arrange -- fluxo normal: check-in já registrado, agora inicia o atendimento.
        const long id = 9213;
        var checkin = AgoraSp().AddMinutes(-10);
        await SemearAgendamentoAsync(id, "CONFIRMADO", nrVersion: 1, dtCheckin: checkin);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await IniciarAtendimentoAsync(client, id, nrVersion: 1);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await resposta.Content.ReadFromJsonAsync<AgendamentoItemDto>();
        corpo!.DtCheckin.Should().Be(checkin, "início não pode sobrescrever o check-in já registrado");
        corpo.DtInicioAtendimento.Should().NotBeNull();
        corpo.NrVersion.Should().Be(2);
    }

    [Theory]
    [InlineData(92141L, "CANCELADO")]
    [InlineData(92142L, "REALIZADO")]
    [InlineData(92143L, "NAO_COMPARECEU")]
    public async Task IniciarAtendimento_EmStatusNaoElegivel_Devolve422(long id, string status)
    {
        // Arrange
        await SemearAgendamentoAsync(id, status, nrVersion: 0);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await IniciarAtendimentoAsync(client, id, nrVersion: 0);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerAgendamentoAsync(id))!.DtInicioAtendimento.Should().BeNull();
    }

    [Fact]
    public async Task IniciarAtendimento_VersaoDesatualizada_Devolve409()
    {
        // Arrange
        const long id = 9215;
        await SemearAgendamentoAsync(id, "AGENDADO", nrVersion: 4);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await IniciarAtendimentoAsync(client, id, nrVersion: 1);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LerAgendamentoAsync(id))!.NrVersion.Should().Be(4);
    }

    [Fact]
    public async Task IniciarAtendimento_AgendamentoDeOutraClinica_Devolve404()
    {
        // Arrange -- A-7, mesma resposta do inexistente.
        const long id = 9216;
        await SemearAgendamentoAsync(id, "AGENDADO", nrVersion: 0, idClinica: KuraApiFactory.IdClinicaOutroTenant);
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await IniciarAtendimentoAsync(client, id, nrVersion: 0);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // GUARDA DE FALTA — fecha a lacuna declarada em AgendaService.cs.
    // ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Falta_AntesDoHorarioMarcado_Devolve422()
    {
        // Arrange -- agendamento marcado para daqui a 30 minutos: "agora" ainda não chegou lá.
        const long id = 9221;
        await SemearAgendamentoAsync(id, "AGENDADO", nrVersion: 0, dtAgendamento: AgoraSp().AddMinutes(30));
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await MarcarFaltaAsync(client, id, nrVersion: 0);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerAgendamentoAsync(id))!.StStatus.Should().Be("AGENDADO");
    }

    [Fact]
    public async Task Falta_DepoisDoHorarioMarcado_Devolve200()
    {
        // Arrange -- agendamento marcado para 30 minutos atrás: já passou da hora.
        const long id = 9222;
        await SemearAgendamentoAsync(id, "AGENDADO", nrVersion: 0, dtAgendamento: AgoraSp().AddMinutes(-30));
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await MarcarFaltaAsync(client, id, nrVersion: 0);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LerAgendamentoAsync(id))!.StStatus.Should().Be("NAO_COMPARECEU");
    }

    [Fact]
    public async Task Falta_DepoisDeCheckin_Devolve422MesmoComHorarioJaPassado()
    {
        // Arrange -- horário já passou, MAS o paciente chegou (check-in registrado): não faltou.
        const long id = 9223;
        await SemearAgendamentoAsync(
            id, "AGENDADO", nrVersion: 0,
            dtAgendamento: AgoraSp().AddMinutes(-30),
            dtCheckin: AgoraSp().AddMinutes(-25));
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await MarcarFaltaAsync(client, id, nrVersion: 0);

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerAgendamentoAsync(id))!.StStatus.Should().Be("AGENDADO");
    }
}
