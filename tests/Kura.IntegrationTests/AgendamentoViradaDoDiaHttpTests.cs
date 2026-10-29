namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// S4-NET-FIX-MEIANOITE — a regra "check-in/início só no DIA do agendamento"
/// (<c>AgendaService.CheckinAsync</c>/<c>IniciarAtendimentoAsync</c>, guarda
/// <c>IRelogioClinica.Hoje() != DtAgendamento.Date</c> ⇒ 422) agora é documentada por teste, com o
/// relógio do host posicionado de propósito em volta da meia-noite de São Paulo. É a regra que fazia
/// os testes de recepção "falharem entre 00h e 00h30": o produto está certo, o teste dependia do
/// relógio de parede.
///
/// <para>Cada caso usa um host próprio (<see cref="KuraApiFactoryRelogioFixo"/>) porque o relógio é
/// por host; ids distintos por caso.</para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class AgendamentoViradaDoDiaHttpTests
{
    private static DateTime Hoje() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).Date;

    private static async Task<HttpResponseMessage> ExecutarAsync(
        string rota, DateTime relogioSp, Func<DateTime, DateTime> agendamento, long id)
    {
        using var factory = new KuraApiFactoryRelogioFixo();
        factory.DefinirAgoraSp(relogioSp);

        using (var escopo = factory.Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();
            db.Agendamentos.Add(new Agendamento
            {
                Id = id,
                IdClinica = KuraApiFactory.IdClinicaSemeada,
                IdVeterinario = KuraApiFactory.IdVeterinarioSemeado,
                DtAgendamento = agendamento(relogioSp),
                NrDuracaoMinutos = 30,
                DsTipoConsulta = "CONSULTA",
                StStatus = "AGENDADO",
                NrVersion = 0,
                StAtiva = true,
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));
        return await client.PostAsJsonAsync($"/api/v1/agendamentos/{id}/{rota}", new { nrVersion = 0 });
    }

    [Theory]
    [InlineData("checkin", 9301L)]
    [InlineData("inicio-atendimento", 9302L)]
    public async Task As0015_AgendamentoDeHoje0005_Aceita(string rota, long id)
    {
        var resposta = await ExecutarAsync(rota, Hoje().AddMinutes(15), r => r.Date.AddMinutes(5), id);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("checkin", 9303L)]
    [InlineData("inicio-atendimento", 9304L)]
    public async Task As0015_AgendamentoDeOntem2350_Recusa422(string rota, long id)
    {
        var resposta = await ExecutarAsync(
            rota, Hoje().AddMinutes(15), r => r.Date.AddDays(-1).AddHours(23).AddMinutes(50), id);
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "20 minutos atrás, mas é ONTEM: a guarda é por dia, não por janela de minutos");
    }

    [Theory]
    [InlineData("checkin", 9305L)]
    [InlineData("inicio-atendimento", 9306L)]
    public async Task As2350_AgendamentoDeHoje2340_Aceita(string rota, long id)
    {
        var resposta = await ExecutarAsync(
            rota, Hoje().AddHours(23).AddMinutes(50), r => r.Date.AddHours(23).AddMinutes(40), id);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
