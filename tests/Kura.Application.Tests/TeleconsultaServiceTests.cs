namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Teleconsulta;
using Kura.Application.Services;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public class TeleconsultaServiceTests
{
    // REC-08 -- "agora" fixo do relógio da clínica, usado pelo teste que prova que
    // DtInicioSessao vem do relógio injetado, não de DateTime.UtcNow real.
    internal static readonly DateTime AgoraFixa = new(2026, 9, 26, 15, 0, 0);

    private readonly Mock<IAgendamentoRepository> _agendamentoRepoMock = new();
    private readonly Mock<IConsentimentoRepository> _consentimentoRepoMock = new();
    private readonly Mock<IDailyService> _dailyServiceMock = new();
    private readonly Mock<IClinicaContext> _clinicaMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly IRelogioClinica _relogio = new RelogioClinicaFixo(AgoraFixa);
    private readonly TeleconsultaService _sut;

    public TeleconsultaServiceTests()
    {
        _clinicaMock.Setup(c => c.IdClinica).Returns(1L);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        _sut = new TeleconsultaService(
            _agendamentoRepoMock.Object,
            _consentimentoRepoMock.Object,
            _dailyServiceMock.Object,
            _clinicaMock.Object,
            _uowMock.Object,
            _relogio);
    }

    private static Agendamento AgendamentoSemSala(long idTutor = 5) => new()
    {
        Id = 10,
        IdClinica = 1,
        IdTutor = idTutor,
        StTeleconsulta = false,
        DsSalaUrl = null
    };

    private static Consentimento ConsentimentoAceito() => new()
    {
        Id = 1,
        IdTutor = 5,
        DsTipo = "TELEORIENTACAO",
        StAceito = 'S',
        NrVersaoTermo = "1.0",
        DtConsentimento = DateTime.UtcNow.AddDays(-1)
    };

    private static Consentimento ConsentimentoRecusado() => new()
    {
        Id = 1,
        IdTutor = 5,
        DsTipo = "TELEORIENTACAO",
        StAceito = 'N',
        NrVersaoTermo = "1.0",
        DtConsentimento = DateTime.UtcNow.AddDays(-1)
    };

    // ---------- CriarOuObterSalaAsync ----------

    [Fact]
    public async Task CriarOuObterSalaAsync_ComConsentimentoAceito_CriaSalaEPersiste()
    {
        // Arrange
        var agendamento = AgendamentoSemSala();
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);
        _consentimentoRepoMock.Setup(r => r.GetMaisRecenteAsync(5L, "TELEORIENTACAO"))
            .ReturnsAsync(ConsentimentoAceito());
        _dailyServiceMock.Setup(d => d.CriarSalaAsync("kura-agendamento-10"))
            .ReturnsAsync(DailyRoomResult.ComSucesso("https://kura.daily.co/room-10"));

        // Act
        var result = await _sut.CriarOuObterSalaAsync(10L);

        // Assert
        result.DsSalaUrl.Should().Be("https://kura.daily.co/room-10");
        result.DsProvedorVideo.Should().Be("DAILY");
        result.StFallbackManual.Should().BeFalse();
        agendamento.StTeleconsulta.Should().BeTrue();
        // REC-08 (call site #5 do G0 item 3) -- DtInicioSessao vem do relógio INJETADO, não de
        // DateTime.UtcNow real. AgoraFixa está longe do UtcNow real da máquina que roda a
        // suíte, então esta asserção só passa se o valor vier de fato do IRelogioClinica.
        agendamento.DtInicioSessao.Should().Be(AgoraFixa);
        _agendamentoRepoMock.Verify(r => r.Update(agendamento), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task CriarOuObterSalaAsync_SemConsentimentoRegistrado_LancaRegraDeNegocioENaoChamaDaily()
    {
        // Arrange
        var agendamento = AgendamentoSemSala();
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);
        _consentimentoRepoMock.Setup(r => r.GetMaisRecenteAsync(5L, "TELEORIENTACAO"))
            .ReturnsAsync((Consentimento?)null);

        // Act
        var act = async () => await _sut.CriarOuObterSalaAsync(10L);

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>();
        _dailyServiceMock.Verify(d => d.CriarSalaAsync(It.IsAny<string>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task CriarOuObterSalaAsync_ConsentimentoRecusado_LancaRegraDeNegocio()
    {
        // Arrange
        var agendamento = AgendamentoSemSala();
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);
        _consentimentoRepoMock.Setup(r => r.GetMaisRecenteAsync(5L, "TELEORIENTACAO"))
            .ReturnsAsync(ConsentimentoRecusado());

        // Act
        var act = async () => await _sut.CriarOuObterSalaAsync(10L);

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>();
        _dailyServiceMock.Verify(d => d.CriarSalaAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CriarOuObterSalaAsync_AgendamentoSemTutor_TrataComoSemConsentimento()
    {
        // Arrange
        var agendamento = AgendamentoSemSala(idTutor: 0);
        agendamento.IdTutor = null;
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);

        // Act
        var act = async () => await _sut.CriarOuObterSalaAsync(10L);

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>();
        _consentimentoRepoMock.Verify(
            r => r.GetMaisRecenteAsync(It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CriarOuObterSalaAsync_DailyFalha_RetornaFallbackManualSemPersistir()
    {
        // Arrange
        var agendamento = AgendamentoSemSala();
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);
        _consentimentoRepoMock.Setup(r => r.GetMaisRecenteAsync(5L, "TELEORIENTACAO"))
            .ReturnsAsync(ConsentimentoAceito());
        _dailyServiceMock.Setup(d => d.CriarSalaAsync(It.IsAny<string>()))
            .ReturnsAsync(DailyRoomResult.Falha());

        // Act
        var result = await _sut.CriarOuObterSalaAsync(10L);

        // Assert
        result.StFallbackManual.Should().BeTrue();
        result.DsSalaUrl.Should().BeNull();
        agendamento.StTeleconsulta.Should().BeFalse();
        _agendamentoRepoMock.Verify(r => r.Update(It.IsAny<Agendamento>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task CriarOuObterSalaAsync_SalaJaCriada_RetornaExistenteSemChamarDaily()
    {
        // Arrange
        var agendamento = AgendamentoSemSala();
        agendamento.StTeleconsulta = true;
        agendamento.DsSalaUrl = "https://kura.daily.co/room-10";
        agendamento.DsProvedorVideo = "DAILY";
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);
        _consentimentoRepoMock.Setup(r => r.GetMaisRecenteAsync(5L, "TELEORIENTACAO"))
            .ReturnsAsync(ConsentimentoAceito());

        // Act
        var result = await _sut.CriarOuObterSalaAsync(10L);

        // Assert
        result.DsSalaUrl.Should().Be("https://kura.daily.co/room-10");
        _dailyServiceMock.Verify(d => d.CriarSalaAsync(It.IsAny<string>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task CriarOuObterSalaAsync_AgendamentoInexistente_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(99L, 1L)).ReturnsAsync((Agendamento?)null);

        // Act
        var act = async () => await _sut.CriarOuObterSalaAsync(99L);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    // ---------- ObterSalaAsync ----------

    [Fact]
    public async Task ObterSalaAsync_SalaJaCriada_RetornaEstadoAtualSemChamarDailyOuConsentimento()
    {
        // Arrange
        var agendamento = AgendamentoSemSala();
        agendamento.StTeleconsulta = true;
        agendamento.DsSalaUrl = "https://kura.daily.co/room-10";
        agendamento.DsProvedorVideo = "DAILY";
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);

        // Act
        var result = await _sut.ObterSalaAsync(10L);

        // Assert
        result.DsSalaUrl.Should().Be("https://kura.daily.co/room-10");
        _dailyServiceMock.Verify(d => d.CriarSalaAsync(It.IsAny<string>()), Times.Never);
        _consentimentoRepoMock.Verify(
            r => r.GetMaisRecenteAsync(It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ObterSalaAsync_AgendamentoInexistente_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(99L, 1L)).ReturnsAsync((Agendamento?)null);

        // Act
        var act = async () => await _sut.ObterSalaAsync(99L);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }
}
