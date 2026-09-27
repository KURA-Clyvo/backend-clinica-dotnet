namespace Kura.Application.Tests;

using FluentAssertions;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
/// REC-08 (backlog <c>KURA_BACKLOG_RECEPCAO.md</c>, A-5/F-4/aceite "b") — ponta a ponta com
/// componentes REAIS (não mocks): <see cref="RelogioClinica"/> real (só o <c>TimeProvider</c> é
/// fake) + <see cref="AgendamentoRepository"/> real contra InMemory. Prova o cenário exato do
/// G0 item 3: relógio fixo em 22:30 BRT (01:30 UTC do dia SEGUINTE) — um agendamento marcado
/// para as 23:00 LOCAIS do MESMO dia tem que continuar aparecendo em "próximos".
///
/// <para>Sob o código antigo (<c>DateTime.UtcNow</c> direto dentro do repositório), o
/// <c>.Date</c> de "agora" já teria virado o dia seguinte em UTC (27/09), enquanto o
/// agendamento foi gravado com <c>.Date</c> = 26/09 (hora local, sem conversão — é assim que o
/// Java grava) — a comparação de data sozinha já excluiria a linha, mesmo antes de chegar no
/// corte de horário.</para>
/// </summary>
public sealed class AgendaRelogioIntegrationTests
{
    private sealed class TimeProviderFixo(DateTimeOffset utcAgora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcAgora;
    }

    private static IRelogioClinica RelogioEm22h30BrtDe26Set2026()
    {
        // 22:30 de 26/09 em São Paulo (UTC-3) == 01:30 de 27/09 em UTC.
        var utcAgora = new DateTimeOffset(2026, 9, 27, 1, 30, 0, TimeSpan.Zero);
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c[It.IsAny<string>()]).Returns((string?)null);
        var loggerMock = new Mock<ILogger<RelogioClinica>>();
        return new RelogioClinica(new TimeProviderFixo(utcAgora), configMock.Object, loggerMock.Object);
    }

    private static KuraDbContext CreateContext()
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns((long?)null);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    [Fact]
    public async Task GetProximosDoDiaAsync_ComRelogioAs22h30Brt_IncluiAgendamentoDas23hLocaisDoMesmoDia()
    {
        // Arrange -- controle: o relógio REALMENTE devolve 22:30 local (não 01:30 UTC).
        var relogio = RelogioEm22h30BrtDe26Set2026();
        relogio.Agora().Should().Be(new DateTime(2026, 9, 26, 22, 30, 0));

        var ctx = CreateContext();
        ctx.Agendamentos.AddRange(
            new Agendamento { Id = 1, IdClinica = 1, NmPaciente = "Tarde-23h-Clinica1", DtAgendamento = new DateTime(2026, 9, 26, 23, 0, 0) },
            new Agendamento { Id = 2, IdClinica = 2, NmPaciente = "Tarde-23h-Clinica2", DtAgendamento = new DateTime(2026, 9, 26, 23, 0, 0) },
            new Agendamento { Id = 3, IdClinica = 1, NmPaciente = "JaPassou-22h", DtAgendamento = new DateTime(2026, 9, 26, 22, 0, 0) }, // antes de "agora" -- não conta
            new Agendamento { Id = 4, IdClinica = 1, NmPaciente = "DiaSeguinte", DtAgendamento = new DateTime(2026, 9, 27, 8, 0, 0) }); // outro dia -- não conta
        await ctx.SaveChangesAsync();

        var repository = new AgendamentoRepository(ctx);

        // Act
        var resultadoClinica1 = (await repository.GetProximosDoDiaAsync(1, relogio.Agora(), 10)).ToList();

        // Assert -- duas clínicas no setup (escopo), e as 3 guardas do dia/hora.
        resultadoClinica1.Should().ContainSingle();
        resultadoClinica1[0].NmPaciente.Should().Be("Tarde-23h-Clinica1");
    }
}
