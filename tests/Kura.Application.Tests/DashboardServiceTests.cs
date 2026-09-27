namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;

public class DashboardServiceTests
{
    private const long IdClinicaContexto = 1;

    // REC-08 -- "agora" fixo do relógio da clínica para esta suíte inteira (não
    // DateTime.UtcNow: o valor precisa ser ESTÁVEL para casar com o Setup que compara exato em
    // GetHojeAsync_ChamaContarTeleorientacoesHojeComIdClinicaDoContextoEPropagaParaODto). Longe
    // de meia-noite de propósito, para não ficar flaky perto da virada de dia.
    private static readonly DateTime AgoraFixa = new(2026, 9, 26, 15, 0, 0);

    private readonly Mock<IEventoClinicoRepository> _eventoMock = new();
    private readonly Mock<IRepository<AlertaTemperatura>> _alertaMock = new();
    private readonly Mock<IRepository<Pet>> _petMock = new();
    private readonly Mock<IRepository<Vacina>> _vacinaMock = new();
    private readonly Mock<IAgendamentoRepository> _agendamentoMock = new();
    private readonly Mock<IClinicaContext> _clinicaContextMock = new();
    private readonly IRelogioClinica _relogio = new RelogioClinicaFixo(AgoraFixa);
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _clinicaContextMock.Setup(c => c.IdClinica).Returns(IdClinicaContexto);
        // Sem setup explícito, Moq (loose mock) devolve default(int) = 0 para
        // ContarTeleorientacoesHojeAsync -- suficiente para os testes que não avaliam esse campo.
        _sut = new DashboardService(
            _eventoMock.Object, _alertaMock.Object,
            _petMock.Object, _vacinaMock.Object,
            _agendamentoMock.Object, _clinicaContextMock.Object, _relogio);
    }

    [Fact]
    public async Task GetHojeAsync_ComUmEventoEUmAlertaAtivoHoje_RetornaDtoComMetricas()
    {
        // Arrange
        var hoje = DateTime.UtcNow.Date;
        _eventoMock.Setup(r => r.GetByFiltersAsync(null, null, null, null, null))
            .ReturnsAsync(new List<EventoClinico>
            {
                new() { Id = 1, IdPet = 10, IdVeterinario = 1, IdTipoEvento = 1,
                         DtEvento = hoje, DsObservacao = "ok", IdClinica = 1 }
            });
        _alertaMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<AlertaTemperatura>
            {
                new() { Id = 1, StResolvido = false, DsTipoAlerta = "T", VlLimite = 8, DsMensagem = "M", IdLeituraTemperatura = 1 }
            });
        _agendamentoMock.Setup(r => r.GetProximosDoDiaAsync(IdClinicaContexto, It.IsAny<DateTime>(), 3))
            .ReturnsAsync(new List<Agendamento>());

        // Act
        var result = await _sut.GetHojeAsync();

        // Assert
        result.TotalConsultasHoje.Should().Be(1);
        result.TotalAlertasAtivos.Should().Be(1);
    }

    [Fact]
    public async Task GetHojeAsync_ComDoisPetsDistintosAtendidosHoje_TotalPacientesAtendidosHojeContaOsDois()
    {
        // Arrange -- FD-17 item 2: mais de 5 eventos hoje não pode saturar em 5 como
        // UltimosPetsAtendidos satura; aqui usamos 2 para o teste ficar legível, mas o ponto é
        // que o contador não tem .Take() nenhum.
        var hoje = DateTime.UtcNow.Date;
        var ontem = hoje.AddDays(-1);
        _eventoMock.Setup(r => r.GetByFiltersAsync(null, null, null, null, null))
            .ReturnsAsync(new List<EventoClinico>
            {
                new() { Id = 1, IdPet = 10, IdVeterinario = 1, IdTipoEvento = 1, DtEvento = hoje, DsObservacao = "ok", IdClinica = 1 },
                new() { Id = 2, IdPet = 11, IdVeterinario = 1, IdTipoEvento = 1, DtEvento = hoje, DsObservacao = "ok", IdClinica = 1 },
                new() { Id = 3, IdPet = 10, IdVeterinario = 1, IdTipoEvento = 1, DtEvento = hoje, DsObservacao = "ok", IdClinica = 1 }, // mesmo pet 10, mesmo dia -- não duplica
                new() { Id = 4, IdPet = 12, IdVeterinario = 1, IdTipoEvento = 1, DtEvento = ontem, DsObservacao = "ok", IdClinica = 1 }, // ontem -- não conta
            });
        _alertaMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AlertaTemperatura>());
        _agendamentoMock.Setup(r => r.GetProximosDoDiaAsync(IdClinicaContexto, It.IsAny<DateTime>(), 3))
            .ReturnsAsync(new List<Agendamento>());

        // Act
        var result = await _sut.GetHojeAsync();

        // Assert
        result.TotalPacientesAtendidosHoje.Should().Be(2); // pets 10 e 11, distintos, hoje
    }

    /// <summary>
    /// 🔴 <b>G2 da FD-17 — buraco de gate medido, não hipotético.</b> Antes deste teste, mutar
    /// <b>só</b> o call site de <c>GetProximosDoDiaAsync</c> em <c>DashboardService</c>
    /// (<c>idClinica</c> → <c>999L</c>) deixava a suíte <b>inteira verde</b>
    /// (396/396, EXIT=0): os <c>Setup</c> existentes casavam <c>IdClinicaContexto</c> mas
    /// devolviam lista <b>vazia</b>, e nenhuma asserção olhava <c>ProximosAgendamentos</c> —
    /// então "Setup não casou, Moq devolveu o default" era indistinguível de "Setup casou".
    /// Este teste fecha os dois lados: fixture <b>não vazia</b> (o default do Moq deixa de ser
    /// igual ao esperado) + <c>Verify</c> do <c>idClinica</c> exato.
    /// </summary>
    [Fact]
    public async Task GetHojeAsync_PropagaProximosAgendamentosEUsaIdClinicaDoContexto()
    {
        // Arrange
        _eventoMock.Setup(r => r.GetByFiltersAsync(null, null, null, null, null))
            .ReturnsAsync(new List<EventoClinico>());
        _alertaMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AlertaTemperatura>());
        _agendamentoMock.Setup(r => r.GetProximosDoDiaAsync(IdClinicaContexto, It.IsAny<DateTime>(), 3))
            .ReturnsAsync(new List<Agendamento>
            {
                new()
                {
                    Id = 77,
                    IdClinica = IdClinicaContexto,
                    NmPaciente = "Rex",
                    DsServico = "Consulta",
                    StStatus = "AGENDADO",
                    DtAgendamento = new DateTime(2099, 1, 1, 10, 0, 0, DateTimeKind.Utc)
                }
            });

        // Act
        var result = await _sut.GetHojeAsync();

        // Assert -- a fixture NAO vazia e o que distingue "Setup casou" de "Moq devolveu default".
        result.ProximosAgendamentos.Should().HaveCount(1);
        result.ProximosAgendamentos[0].Id.Should().Be(77);
        result.ProximosAgendamentos[0].NmPaciente.Should().Be("Rex");
        _agendamentoMock.Verify(
            r => r.GetProximosDoDiaAsync(IdClinicaContexto, It.IsAny<DateTime>(), 3), Times.Once);
    }

    /// <summary>
    /// G2 REC-08, achado I-1 (Important): antes desta fix wave, esta chamada era verificada só
    /// com <c>It.IsAny&lt;DateTime&gt;()</c> -- reverter <c>DashboardService.cs</c> (call site
    /// #1) de volta para <c>DateTime.UtcNow</c> passava com a suíte inteira verde, porque
    /// nenhum teste conferia QUAL valor chegava ao repositório. <c>AgoraFixa</c> está longe do
    /// <c>UtcNow</c> real da máquina que roda a suíte -- só passa se o valor vier do
    /// <see cref="IRelogioClinica"/> injetado.
    /// </summary>
    [Fact]
    public async Task GetHojeAsync_ChamaGetProximosDoDiaComOAgoraExatoDoRelogio_NaoUtcNowReal()
    {
        // Arrange
        _eventoMock.Setup(r => r.GetByFiltersAsync(null, null, null, null, null))
            .ReturnsAsync(new List<EventoClinico>());
        _alertaMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AlertaTemperatura>());
        _agendamentoMock.Setup(r => r.GetProximosDoDiaAsync(IdClinicaContexto, AgoraFixa, 3))
            .ReturnsAsync(new List<Agendamento>());

        // Act
        await _sut.GetHojeAsync();

        // Assert -- Verify checa a invocação REAL, não depende do Setup ter casado: se o
        // produtor mandar outro valor (ex. DateTime.UtcNow real), esta linha falha.
        _agendamentoMock.Verify(r => r.GetProximosDoDiaAsync(IdClinicaContexto, AgoraFixa, 3), Times.Once);
    }

    [Fact]
    public async Task GetHojeAsync_ChamaContarTeleorientacoesHojeComIdClinicaDoContextoEPropagaParaODto()
    {
        // Arrange -- FD-17 item 3. REC-08: "hoje" aqui tem que ser o DO RELÓGIO (agora.Date),
        // não DateTime.UtcNow.Date -- é o que DashboardService.GetHojeAsync agora passa para
        // ContarTeleorientacoesHojeAsync.
        var hoje = AgoraFixa.Date;
        _eventoMock.Setup(r => r.GetByFiltersAsync(null, null, null, null, null))
            .ReturnsAsync(new List<EventoClinico>());
        _alertaMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AlertaTemperatura>());
        _agendamentoMock.Setup(r => r.GetProximosDoDiaAsync(IdClinicaContexto, It.IsAny<DateTime>(), 3))
            .ReturnsAsync(new List<Agendamento>());
        _agendamentoMock.Setup(r => r.ContarTeleorientacoesHojeAsync(IdClinicaContexto, hoje))
            .ReturnsAsync(3);

        // Act
        var result = await _sut.GetHojeAsync();

        // Assert
        result.TotalTeleorientacoesHoje.Should().Be(3);
        _agendamentoMock.Verify(r => r.ContarTeleorientacoesHojeAsync(IdClinicaContexto, hoje), Times.Once);
    }

    [Fact]
    public async Task GetAlertasAsync_ComAlertaAtivoEVacinaProximaEm30Dias_RetornaAlertasAtivosEVacinasVencendo()
    {
        // Arrange
        var proximos30Dias = DateTime.UtcNow.AddDays(15).Date;
        _alertaMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<AlertaTemperatura, bool>>>()))
            .ReturnsAsync(new List<AlertaTemperatura>
            {
                new() { Id = 1, StResolvido = false, DsTipoAlerta = "ACIMA_LIMITE", VlLimite = 8, DsMensagem = "Temp alta", IdLeituraTemperatura = 1, DtCriacao = DateTime.UtcNow }
            });
        _vacinaMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Vacina, bool>>>()))
            .ReturnsAsync(new List<Vacina>
            {
                new() { Id = 5, NmVacina = "Raiva", DtProximaDose = proximos30Dias, NrLote = "L1", DsFabricante = "F", IdEventoClinico = 1, DtCriacao = DateTime.UtcNow }
            });

        // Act
        var result = (await _sut.GetAlertasAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRecentesAsync_RetornaAgendamentosPassadosMapeados_NaoOResumoDeHoje()
    {
        // Arrange
        var referencia = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc);
        _agendamentoMock.Setup(r => r.GetRecentesAsync(IdClinicaContexto, It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Agendamento>
            {
                new()
                {
                    Id = 42,
                    NmPaciente = "Rex",
                    DtAgendamento = referencia.AddDays(-1),
                    DsServico = "Consulta de rotina",
                    StStatus = "REALIZADO"
                }
            });

        // Act
        var result = (await _sut.GetRecentesAsync()).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(42);
        result[0].NmPaciente.Should().Be("Rex");
        result[0].DsServico.Should().Be("Consulta de rotina");
        result[0].StStatus.Should().Be("REALIZADO");
        result[0].DtAgendamento.Should().Be(referencia.AddDays(-1));

        _agendamentoMock.Verify(r => r.GetRecentesAsync(IdClinicaContexto, It.IsAny<DateTime>(), It.IsAny<int>()), Times.Once);
        _agendamentoMock.Verify(r => r.GetProximosDoDiaAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Never);
    }

    /// <summary>
    /// G2 REC-08, achado I-1 (Important): mesma lacuna do teste de <c>GetProximosDoDiaAsync</c>
    /// acima, para <c>GetRecentesAsync</c> (call site #3, <c>DashboardService.cs:138</c>).
    /// Reverter para <c>DateTime.UtcNow</c> passava com a suíte inteira verde antes desta fix
    /// wave -- nenhum teste conferia o valor exato do argumento.
    /// </summary>
    [Fact]
    public async Task GetRecentesAsync_ChamaRepositorioComOAgoraExatoDoRelogio_NaoUtcNowReal()
    {
        // Arrange -- 10 == DashboardService.LimiteAgendamentosRecentes (private const, valor
        // copiado aqui; se o produtor mudar o limite, este teste avisa via falha de Verify).
        _agendamentoMock.Setup(r => r.GetRecentesAsync(IdClinicaContexto, AgoraFixa, 10))
            .ReturnsAsync(new List<Agendamento>());

        // Act
        await _sut.GetRecentesAsync();

        // Assert
        _agendamentoMock.Verify(r => r.GetRecentesAsync(IdClinicaContexto, AgoraFixa, 10), Times.Once);
    }
}
