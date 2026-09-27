namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Agenda;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public class AgendaServiceTests
{
    private readonly Mock<IAgendamentoReadRepository> _readRepoMock = new();
    private readonly Mock<IAgendamentoRepository> _agendamentoRepoMock = new();
    private readonly Mock<IClinicaContext> _clinicaMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IGeradorUrlFotoPet> _geradorUrlFotoPetMock = new();
    private readonly AgendaService _sut;

    public AgendaServiceTests()
    {
        _clinicaMock.Setup(c => c.IdClinica).Returns(1L);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        _sut = new AgendaService(
            _readRepoMock.Object,
            _clinicaMock.Object,
            _agendamentoRepoMock.Object,
            _uowMock.Object,
            _geradorUrlFotoPetMock.Object);
    }

    private static DateTime Inicio => new(2026, 5, 6);
    private static DateTime Fim => new(2026, 5, 12);

    // ---------- GetAgenda tests ----------

    [Fact]
    public async Task GetAgendaAsync_IntervaloValido_RetornaAgendamentosMapeados()
    {
        // Arrange
        var agendamentos = new List<Agendamento>
        {
            new()
            {
                Id = 1,
                IdClinica = 1,
                IdVeterinario = 10,
                DtAgendamento = Inicio.AddHours(9),
                NrDuracaoMinutos = 30,
                DsTipoConsulta = "Consulta",
                StStatus = "CONFIRMADO",
                NrVersion = 0,
                StAtiva = true,
                Pet = new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1 },
                Tutor = new Tutor { Id = 3, NmTutor = "João" },
                Veterinario = new Veterinario { Id = 10, NmVeterinario = "Dr. Ana", IdClinica = 1, NrCrmv = "1234" }
            }
        };

        _readRepoMock.Setup(r => r.GetByIntervaloAsync(1L, Inicio, Fim, null))
            .ReturnsAsync(agendamentos);

        // Act
        var result = await _sut.GetAgendaAsync(Inicio, Fim, null);

        // Assert
        result.Should().NotBeNull();
        result.DataInicio.Should().Be(Inicio);
        result.DataFim.Should().Be(Fim);
        result.Agendamentos.Should().HaveCount(1);

        var item = result.Agendamentos[0];
        item.IdAgendamento.Should().Be(1);
        item.NmPet.Should().Be("Rex");
        item.NmTutor.Should().Be("João");
        item.NmVeterinario.Should().Be("Dr. Ana");
        item.IdVeterinario.Should().Be(10);
        item.DsTipoConsulta.Should().Be("Consulta");
        item.DsStatus.Should().Be("CONFIRMADO");
        item.DuracaoMinutos.Should().Be(30);
    }

    [Fact]
    public async Task GetAgendaAsync_DataFimAnteriorDataInicio_LancaRegraDeNegocio()
    {
        // Act
        var act = async () => await _sut.GetAgendaAsync(Fim, Inicio, null);

        // Assert
        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().Be("DataFim não pode ser anterior à DataInicio.");
    }

    [Fact]
    public async Task GetAgendaAsync_IntervaloMaiorQue31Dias_LancaRegraDeNegocio()
    {
        // Arrange
        var inicio = new DateTime(2026, 1, 1);
        var fimFora = inicio.AddDays(32);

        // Act
        var act = async () => await _sut.GetAgendaAsync(inicio, fimFora, null);

        // Assert
        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().Be("Intervalo máximo de 31 dias.");
    }

    [Fact]
    public async Task GetAgendaAsync_ComVeterinarioId_PassaFiltroAoRepository()
    {
        // Arrange
        _readRepoMock.Setup(r => r.GetByIntervaloAsync(1L, Inicio, Fim, 10L))
            .ReturnsAsync(new List<Agendamento>());

        // Act
        await _sut.GetAgendaAsync(Inicio, Fim, 10L);

        // Assert
        _readRepoMock.Verify(r => r.GetByIntervaloAsync(1L, Inicio, Fim, 10L), Times.Once);
    }

    // ---------- AtualizarStatus tests ----------

    private static Agendamento AgendamentoAtivo(string stStatus = "CONFIRMADO", long version = 2) => new()
    {
        Id = 10,
        IdClinica = 1,
        StStatus = stStatus,
        NrVersion = version,
        StAtiva = true
    };

    [Fact]
    public async Task AtualizarStatusAsync_SemConflito_CommitERetornaStatusAtualizado()
    {
        // Arrange
        var agendamento = AgendamentoAtivo(version: 2);
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);

        var dto = new AtualizarStatusAgendamentoDto { DsStatus = "REALIZADO", NrVersion = 2 };
        // Act
        var result = await _sut.AtualizarStatusAsync(10L, dto);

        // Assert
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
        result.DsStatus.Should().Be("REALIZADO");
        agendamento.NrVersion.Should().Be(3);
    }

    [Fact]
    public async Task AtualizarStatusAsync_VersionDesatualizada_LancaConflitoConcorrencia()
    {
        // Arrange
        var agendamento = AgendamentoAtivo(version: 5);
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);

        var dto = new AtualizarStatusAgendamentoDto { DsStatus = "CANCELADO", NrVersion = 3 };
        // Act
        var act = async () => await _sut.AtualizarStatusAsync(10L, dto);

        // Assert
        await act.Should().ThrowAsync<ConflitoConcorrenciaException>();
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task AtualizarStatusAsync_CommitLancaConcurrencyException_PropagaConflito()
    {
        // Arrange
        var agendamento = AgendamentoAtivo(version: 2);
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);
        _uowMock.Setup(u => u.CommitAsync()).ThrowsAsync(new ConflitoConcorrenciaException());

        var dto = new AtualizarStatusAgendamentoDto { DsStatus = "REALIZADO", NrVersion = 2 };
        // Act
        var act = async () => await _sut.AtualizarStatusAsync(10L, dto);

        // Assert
        await act.Should().ThrowAsync<ConflitoConcorrenciaException>();
    }

    [Fact]
    public async Task AtualizarStatusAsync_StatusFinal_LancaRegraDeNegocio()
    {
        // Arrange
        var agendamento = AgendamentoAtivo(stStatus: "REALIZADO", version: 1);
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);

        var dto = new AtualizarStatusAgendamentoDto { DsStatus = "CANCELADO", NrVersion = 1 };
        // Act
        var act = async () => await _sut.AtualizarStatusAsync(10L, dto);

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>();
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task AtualizarStatusAsync_AgendamentoInexistente_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(99L, 1L)).ReturnsAsync((Agendamento?)null);

        var dto = new AtualizarStatusAgendamentoDto { DsStatus = "REALIZADO", NrVersion = 0 };
        // Act
        var act = async () => await _sut.AtualizarStatusAsync(99L, dto);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    // ---------- FD-06: máquina de estados ----------

    /// <summary>
    /// Toda transição que a FD-06 declara legítima. O par <c>AGENDADO → CONFIRMADO</c> é o que
    /// copia a guarda do Java (<c>confirmar()</c> exige status exatamente <c>AGENDADO</c>).
    /// </summary>
    [Theory]
    [InlineData("INTENCAO", "CANCELADO")]
    [InlineData("AGENDADO", "CONFIRMADO")]
    [InlineData("AGENDADO", "REALIZADO")]
    [InlineData("AGENDADO", "CANCELADO")]
    [InlineData("AGENDADO", "NAO_COMPARECEU")]
    [InlineData("CONFIRMADO", "REALIZADO")]
    [InlineData("CONFIRMADO", "CANCELADO")]
    [InlineData("CONFIRMADO", "NAO_COMPARECEU")]
    public async Task AtualizarStatusAsync_TransicaoPermitida_Persiste(string origem, string destino)
    {
        // Arrange
        var agendamento = AgendamentoAtivo(stStatus: origem, version: 2);
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);

        var dto = new AtualizarStatusAgendamentoDto { DsStatus = destino, NrVersion = 2 };

        // Act
        var result = await _sut.AtualizarStatusAsync(10L, dto);

        // Assert
        result.DsStatus.Should().Be(destino);
        agendamento.StStatus.Should().Be(destino);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    /// <summary>
    /// 🔴 As recusas que só existem por causa desta task. <c>INTENCAO → REALIZADO</c> e
    /// <c>INTENCAO → NAO_COMPARECEU</c> passariam se <c>StatusFinais</c> fosse a única regra;
    /// <c>CONFIRMADO → CONFIRMADO</c> e <c>REALIZADO → ...</c> mostram os dois motivos
    /// diferentes de recusa (origem errada e origem terminal).
    /// </summary>
    [Theory]
    [InlineData("INTENCAO", "REALIZADO")]
    [InlineData("INTENCAO", "NAO_COMPARECEU")]
    [InlineData("INTENCAO", "CONFIRMADO")]
    [InlineData("CONFIRMADO", "CONFIRMADO")]
    [InlineData("REALIZADO", "CANCELADO")]
    [InlineData("CANCELADO", "REALIZADO")]
    [InlineData("NAO_COMPARECEU", "REALIZADO")]
    [InlineData("NAO_COMPARECEU", "CANCELADO")]
    public async Task AtualizarStatusAsync_TransicaoRecusada_NaoCommitaENaoMutaAEntidade(
        string origem, string destino)
    {
        // Arrange
        var agendamento = AgendamentoAtivo(stStatus: origem, version: 2);
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);

        var dto = new AtualizarStatusAgendamentoDto { DsStatus = destino, NrVersion = 2 };

        // Act
        var act = async () => await _sut.AtualizarStatusAsync(10L, dto);

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>();
        agendamento.StStatus.Should().Be(origem, "uma transição recusada não pode deixar rastro");
        agendamento.NrVersion.Should().Be(2);
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    /// <summary>
    /// 🔴 <b>O risco central da FD-06.</b> Marcar falta e depois «corrigir» para realizado
    /// apagaria o registro da ausência — e é sobre esse dado que a trilha financeira do ciclo
    /// FIN fatura. Este caso é o que obriga <c>NAO_COMPARECEU</c> a ser terminal.
    /// </summary>
    [Fact]
    public async Task AtualizarStatusAsync_NaoCompareceuEhTerminal_MensagemDizEstadoFinal()
    {
        // Arrange
        var agendamento = AgendamentoAtivo(stStatus: "NAO_COMPARECEU", version: 1);
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);

        var dto = new AtualizarStatusAgendamentoDto { DsStatus = "REALIZADO", NrVersion = 1 };

        // Act
        var act = async () => await _sut.AtualizarStatusAsync(10L, dto);

        // Assert
        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().Contain("estado final")
          .And.Contain("NAO_COMPARECEU");
    }

    /// <summary>
    /// A transição é conferida ANTES do <c>NrVersion</c>: um pedido impossível é impossível
    /// independentemente de o cliente estar com a versão em dia. Documenta a ordem das guardas
    /// para que trocá-la quebre um teste em vez de mudar silenciosamente qual erro o app recebe.
    /// </summary>
    [Fact]
    public async Task AtualizarStatusAsync_TransicaoInvalidaEVersaoStale_PrecedeOConflito()
    {
        // Arrange
        var agendamento = AgendamentoAtivo(stStatus: "INTENCAO", version: 9);
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);

        var dto = new AtualizarStatusAgendamentoDto { DsStatus = "REALIZADO", NrVersion = 1 };

        // Act
        var act = async () => await _sut.AtualizarStatusAsync(10L, dto);

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>();
    }

    /// <summary>
    /// Status de origem fora do <c>CHECK</c> do Oracle (linha corrompida ou mapa envelhecido):
    /// falha fechado. Antes da FD-06 esse caso caía direto na escrita.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("FATURADO")]
    public async Task AtualizarStatusAsync_OrigemDesconhecida_FalhaFechado(string? origem)
    {
        // Arrange
        var agendamento = AgendamentoAtivo(stStatus: origem!, version: 2);
        _agendamentoRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(agendamento);

        var dto = new AtualizarStatusAgendamentoDto { DsStatus = "REALIZADO", NrVersion = 2 };

        // Act
        var act = async () => await _sut.AtualizarStatusAsync(10L, dto);

        // Assert
        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().Contain("não reconhecido");
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }
}
