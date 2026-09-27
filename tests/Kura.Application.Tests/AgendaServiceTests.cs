namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Agenda;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Microsoft.Extensions.Logging;

public class AgendaServiceTests
{
    private readonly Mock<IAgendamentoReadRepository> _readRepoMock = new();
    private readonly Mock<IAgendamentoRepository> _agendamentoRepoMock = new();
    private readonly Mock<IClinicaContext> _clinicaMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IGeradorUrlFotoPet> _geradorUrlFotoPetMock = new();
    private readonly Mock<ILogger<AgendaService>> _loggerMock = new();
    private readonly Mock<ITutorRepository> _tutorRepoMock = new();
    private readonly Mock<IPetRepository> _petRepoMock = new();
    private readonly Mock<IVeterinarioRepository> _veterinarioRepoMock = new();
    private readonly Mock<ITriagemLunaRepository> _triagemLunaRepoMock = new();
    private readonly Mock<IRelogioClinica> _relogioClinicaMock = new();
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
            _geradorUrlFotoPetMock.Object,
            _loggerMock.Object,
            _tutorRepoMock.Object,
            _petRepoMock.Object,
            _veterinarioRepoMock.Object,
            _triagemLunaRepoMock.Object,
            _relogioClinicaMock.Object);
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

    // ---------- REC-09/A-3: CalcularEtapaRecepcao (tabela-verdade) ----------

    /// <summary>
    /// Tabela-verdade completa da precedência A-3 — uma linha por combinação relevante de
    /// (status, DtCheckin, DtInicioAtendimento). <c>CalcularEtapaRecepcao</c> é função pura
    /// (nenhum mock necessário), testável diretamente.
    /// </summary>
    [Theory]
    // 1. status terminal manda, mesmo com timestamps preenchidos.
    [InlineData("REALIZADO", null, null, "FINALIZADO")]
    [InlineData("REALIZADO", "2026-09-26T09:00", "2026-09-26T09:05", "FINALIZADO")]
    [InlineData("CANCELADO", null, null, "CANCELADO")]
    // caso explícito do aceite: check-in batido num agendamento cancelado NÃO reabre a etapa.
    [InlineData("CANCELADO", "2026-09-26T09:00", null, "CANCELADO")]
    [InlineData("NAO_COMPARECEU", null, null, "NAO_COMPARECEU")]
    [InlineData("NAO_COMPARECEU", "2026-09-26T09:00", "2026-09-26T09:05", "NAO_COMPARECEU")]
    // 2. DT_INICIO_ATENDIMENTO sem DT_CHECKIN -- caso explícito do aceite (walk-in).
    [InlineData("AGENDADO", null, "2026-09-26T09:05", "EM_ATENDIMENTO")]
    [InlineData("CONFIRMADO", null, "2026-09-26T09:05", "EM_ATENDIMENTO")]
    [InlineData("AGENDADO", "2026-09-26T09:00", "2026-09-26T09:05", "EM_ATENDIMENTO")]
    // 3. só DT_CHECKIN.
    [InlineData("AGENDADO", "2026-09-26T09:00", null, "CHEGOU")]
    [InlineData("CONFIRMADO", "2026-09-26T09:00", null, "CHEGOU")]
    // 4. sem timestamps: o próprio status.
    [InlineData("AGENDADO", null, null, "AGENDADO")]
    [InlineData("CONFIRMADO", null, null, "CONFIRMADO")]
    // defensivo: INTENCAO/status nulo nunca aparecem em produção (FD-06), mas a função é
    // total -- cai no fallback AGENDADO em vez de lançar (GET /agenda não pode 500 por isso).
    [InlineData("INTENCAO", null, null, "AGENDADO")]
    [InlineData(null, null, null, "AGENDADO")]
    // G2/m-3: status FORA do CHECK do Oracle (linha corrompida, ou mapa que envelheceu) --
    // sem estas linhas, um fallback que vazasse o status cru ("FOO") ficaria verde, porque a
    // única linha com status desconhecido (INTENCAO) cai no MESMO valor (AGENDADO) que um
    // fallback correto produziria por outro motivo. Nunca pode sair um valor fora do domínio
    // de 7 (aceite (a) original + esta ressalva) -- a derivação continua pelas datas.
    [InlineData("FOO", null, null, "AGENDADO")]
    [InlineData("FOO", "2026-09-26T09:00", null, "CHEGOU")]
    // status terminal ganha de início de atendimento também -- não só de check-in (a linha
    // "CANCELADO"+checkin já existia; faltava a mesma garantia para DT_INICIO_ATENDIMENTO).
    [InlineData("CANCELADO", null, "2026-09-26T09:05", "CANCELADO")]
    public void CalcularEtapaRecepcao_TabelaVerdade(
        string? stStatus, string? dtCheckinStr, string? dtInicioStr, string esperado)
    {
        // Arrange
        DateTime? dtCheckin = dtCheckinStr is null ? null : DateTime.Parse(dtCheckinStr);
        DateTime? dtInicio = dtInicioStr is null ? null : DateTime.Parse(dtInicioStr);

        // Act
        var etapa = AgendaService.CalcularEtapaRecepcao(stStatus, dtCheckin, dtInicio);

        // Assert
        etapa.Should().Be(esperado);
    }

    /// <summary>
    /// GetAgendaAsync projeta DsEtapaRecepcao usando a mesma função -- prova a fiação, não só
    /// a função isolada.
    /// </summary>
    [Fact]
    public async Task GetAgendaAsync_ProjetaDsEtapaRecepcao()
    {
        // Arrange
        var agendamentos = new List<Agendamento>
        {
            new()
            {
                Id = 1,
                IdClinica = 1,
                DtAgendamento = Inicio.AddHours(9),
                StStatus = "AGENDADO",
                DtCheckin = Inicio.AddHours(8).AddMinutes(50),
                StAtiva = true
            }
        };
        _readRepoMock.Setup(r => r.GetByIntervaloAsync(1L, Inicio, Fim, null))
            .ReturnsAsync(agendamentos);

        // Act
        var result = await _sut.GetAgendaAsync(Inicio, Fim, null);

        // Assert
        result.Agendamentos[0].DsEtapaRecepcao.Should().Be("CHEGOU");
    }

    // ---------- REC-09 aceite (d): DsFotoThumbUrl reaproveita o gerador da FT-04 ----------

    /// <summary>
    /// G2 (m-6): a chave usada aqui tem que ser uma que o gerador REAL aceitaria
    /// (<c>ChaveFotoPet.Base()</c> sempre produz <c>.../{uuid}.{ext}</c>, COM ponto/extensão)
    /// — a versão anterior deste teste usava <c>"clinica/1/pet/5/foto"</c> (sem extensão), que
    /// <c>ChaveFotoPet.Variante</c> RECUSA com <see cref="ArgumentException"/> na implementação
    /// real; o mock escondia isso porque nunca chama a fórmula de verdade. Ver
    /// <see cref="GetAgendaAsync_ChaveDeFotoMalformada_DsFotoThumbUrlNuloSemDerrubarAAgenda"/>
    /// para o caso da chave sem extensão.
    /// </summary>
    [Fact]
    public async Task GetAgendaAsync_ComFotoDoPet_UsaGeradorUrlFotoPetComSufixoThumb()
    {
        // Arrange
        var agendamentos = new List<Agendamento>
        {
            new()
            {
                Id = 1,
                IdClinica = 1,
                DtAgendamento = Inicio.AddHours(9),
                StStatus = "AGENDADO",
                StAtiva = true,
                Pet = new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, DsFotoChave = "clinica/1/pet/5/abc123.webp" }
            }
        };
        _readRepoMock.Setup(r => r.GetByIntervaloAsync(1L, Inicio, Fim, null))
            .ReturnsAsync(agendamentos);
        _geradorUrlFotoPetMock
            .Setup(g => g.GerarUrl("clinica/1/pet/5/abc123.webp", Kura.Domain.Storage.ChaveFotoPet.SufixoThumb))
            .Returns("https://kura.example/api/v1/fotos/clinica/1/pet/5/abc123_256.webp?exp=1&sig=abc");

        // Act
        var result = await _sut.GetAgendaAsync(Inicio, Fim, null);

        // Assert
        result.Agendamentos[0].DsFotoThumbUrl.Should().Be(
            "https://kura.example/api/v1/fotos/clinica/1/pet/5/abc123_256.webp?exp=1&sig=abc");
        _geradorUrlFotoPetMock.Verify(
            g => g.GerarUrl("clinica/1/pet/5/abc123.webp", Kura.Domain.Storage.ChaveFotoPet.SufixoThumb),
            Times.Once);
    }

    /// <summary>
    /// G2/m-6 — medido ao vivo pela sonda HTTP do revisor: uma chave sem extensão faz
    /// <c>ChaveFotoPet.Variante</c> (chamada de dentro do gerador REAL) lançar
    /// <see cref="ArgumentException"/>, e antes deste fix isso derrubava o <c>GET /agenda</c>
    /// inteiro com <c>500</c> — não só a foto daquele card. <c>ToItemDto</c> agora protege a
    /// chamada: o pet daquela linha fica sem foto (mesmo tratamento de "pet sem foto"), e o
    /// resto da agenda continua de pé. Mordida real (não simulada): o mock aqui devolve o
    /// MESMO comportamento do gerador real (lança para chave sem ponto) — sem o try/catch em
    /// <c>GerarFotoThumbUrlSeguro</c>, este teste propagaria a exceção e falharia.
    /// </summary>
    [Fact]
    public async Task GetAgendaAsync_ChaveDeFotoMalformada_DsFotoThumbUrlNuloSemDerrubarAAgenda()
    {
        // Arrange
        var agendamentos = new List<Agendamento>
        {
            new()
            {
                Id = 42,
                IdClinica = 1,
                IdPet = 5,
                DtAgendamento = Inicio.AddHours(9),
                StStatus = "AGENDADO",
                StAtiva = true,
                Pet = new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, DsFotoChave = "clinica/1/pet/5/foto-sem-extensao" }
            }
        };
        _readRepoMock.Setup(r => r.GetByIntervaloAsync(1L, Inicio, Fim, null))
            .ReturnsAsync(agendamentos);
        _geradorUrlFotoPetMock
            .Setup(g => g.GerarUrl("clinica/1/pet/5/foto-sem-extensao", Kura.Domain.Storage.ChaveFotoPet.SufixoThumb))
            .Throws(new ArgumentException(
                "Chave base 'clinica/1/pet/5/foto-sem-extensao' não tem extensão.", "chaveBase"));

        // Act
        var result = await _sut.GetAgendaAsync(Inicio, Fim, null);

        // Assert -- a agenda inteira não cai; só a foto daquela linha fica null.
        result.Agendamentos.Should().ContainSingle();
        result.Agendamentos[0].DsFotoThumbUrl.Should().BeNull();

        // Assert -- WARN registrado, SEM a chave (nem qualquer PII) no log; só ids numéricos.
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("42") &&
                    state.ToString()!.Contains("5") &&
                    !state.ToString()!.Contains("foto-sem-extensao")),
                It.IsAny<ArgumentException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Mordida do aceite (d): pet sem foto (DsFotoChave nula) precisa continuar devolvendo
    /// null -- não uma URL para uma chave vazia. Reaproveita o contrato já documentado em
    /// IGeradorUrlFotoPet.GerarUrl (nunca lança para chave nula/vazia).
    /// </summary>
    [Fact]
    public async Task GetAgendaAsync_PetSemFoto_DsFotoThumbUrlNulo()
    {
        // Arrange
        var agendamentos = new List<Agendamento>
        {
            new()
            {
                Id = 1,
                IdClinica = 1,
                DtAgendamento = Inicio.AddHours(9),
                StStatus = "AGENDADO",
                StAtiva = true,
                Pet = new Pet { Id = 5, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, DsFotoChave = null }
            }
        };
        _readRepoMock.Setup(r => r.GetByIntervaloAsync(1L, Inicio, Fim, null))
            .ReturnsAsync(agendamentos);
        _geradorUrlFotoPetMock
            .Setup(g => g.GerarUrl(null, Kura.Domain.Storage.ChaveFotoPet.SufixoThumb))
            .Returns((string?)null);

        // Act
        var result = await _sut.GetAgendaAsync(Inicio, Fim, null);

        // Assert
        result.Agendamentos[0].DsFotoThumbUrl.Should().BeNull();
    }

    // ---------- G2/I-1: projeção completa dos 9 campos novos ----------

    /// <summary>
    /// G2 (I-1, Important) — o aceite (b) da REC-09 provava a navegação
    /// <c>TriagemOrigem</c> no REPOSITÓRIO (InMemory), mas nenhum teste do SERVICE montava um
    /// <c>Agendamento</c> com os 9 campos novos preenchidos e conferia o DTO campo a campo. A
    /// G2 mostrou 2 mutações que ficavam verdes: <c>DsNivelUrgenciaOrigem</c> lendo
    /// <c>DS_DESCRICAO</c> (texto livre da conversa do tutor com a Luna, dado clínico) em vez
    /// de <c>DS_NIVEL_URGENCIA</c>, e <c>DsRespostaConfirmacao</c> zerado. Este teste usa
    /// <c>DsNivelUrgencia</c> e <c>DsDescricao</c> DIFERENTES de propósito na triagem de
    /// origem — se o service trocar um pelo outro, a asserção de valor pega.
    /// </summary>
    [Fact]
    public async Task GetAgendaAsync_AgendamentoTotalmentePreenchido_ProjetaOsNoveCamposNovosUmAUm()
    {
        // Arrange
        var triagemOrigem = new TriagemLuna
        {
            Id = 800,
            IdClinica = 1, // mesma clínica do agendamento -- não é o cenário do aceite (b)
            DsNivelUrgencia = "ALTA",
            DsDescricao = "Vômito recorrente há 2 dias, sem apetite", // DIFERENTE de DsNivelUrgencia de propósito
            DtTriagem = Inicio.AddDays(-1)
        };
        var agendamentos = new List<Agendamento>
        {
            new()
            {
                Id = 77,
                IdClinica = 1,
                IdPet = 501,
                IdTutor = 601,
                IdVeterinario = 10,
                DtAgendamento = Inicio.AddHours(9),
                NrDuracaoMinutos = 30,
                DsTipoConsulta = "Consulta",
                StStatus = "CONFIRMADO",
                NrVersion = 3,
                StAtiva = true,
                DsOrigem = "TRIAGEM_LUNA",
                DtCheckin = Inicio.AddHours(8).AddMinutes(55),
                DtInicioAtendimento = null,
                IdTriagemOrigem = 800,
                DsRespostaConfirmacao = "SIM",
                TriagemOrigem = triagemOrigem,
                Pet = new Pet { Id = 501, NmPet = "Rex", IdClinica = 1, IdEspecie = 1, DsFotoChave = "clinica/1/pet/501/abc.webp" },
                Tutor = new Tutor { Id = 601, NmTutor = "João" },
                Veterinario = new Veterinario { Id = 10, NmVeterinario = "Dr. Ana", IdClinica = 1, NrCrmv = "1234" }
            }
        };
        _readRepoMock.Setup(r => r.GetByIntervaloAsync(1L, Inicio, Fim, null))
            .ReturnsAsync(agendamentos);
        _geradorUrlFotoPetMock
            .Setup(g => g.GerarUrl("clinica/1/pet/501/abc.webp", Kura.Domain.Storage.ChaveFotoPet.SufixoThumb))
            .Returns("https://kura.example/api/v1/fotos/clinica/1/pet/501/abc_256.webp?exp=1&sig=xyz");

        // Act
        var result = await _sut.GetAgendaAsync(Inicio, Fim, null);

        // Assert -- os 9 campos novos, um a um.
        var item = result.Agendamentos[0];
        item.IdPet.Should().Be(501);
        item.IdTutor.Should().Be(601);
        item.DtCheckin.Should().Be(Inicio.AddHours(8).AddMinutes(55));
        item.DtInicioAtendimento.Should().BeNull();
        item.DsOrigem.Should().Be("TRIAGEM_LUNA");
        item.DsNivelUrgenciaOrigem.Should().Be("ALTA", "tem que ler DS_NIVEL_URGENCIA, nunca DS_DESCRICAO (dado clínico livre)");
        item.DsRespostaConfirmacao.Should().Be("SIM");
        item.DsEtapaRecepcao.Should().Be("CHEGOU"); // CONFIRMADO + checkin, sem início
        item.DsFotoThumbUrl.Should().Be("https://kura.example/api/v1/fotos/clinica/1/pet/501/abc_256.webp?exp=1&sig=xyz");
    }

    // ---------- CriarAsync tests (REC-10) ----------

    private static AgendamentoCreateDto BuildCreateDto(long? idTriagemOrigem = null) => new()
    {
        IdTutor = 601,
        IdPet = 501,
        IdVeterinario = 10,
        DtAgendamento = new DateTime(2026, 10, 7, 9, 0, 0),
        DsTipo = "CONSULTA",
        IdTriagemOrigem = idTriagemOrigem
    };

    private static Tutor BuildTutor() => new() { Id = 601, IdClinica = 1, NmTutor = "João" };

    private static Pet BuildPetVinculado() => new()
    {
        Id = 501,
        IdClinica = 1,
        NmPet = "Rex",
        IdEspecie = 1,
        TutorPets = [new TutorPet { IdTutor = 601, IdPet = 501 }]
    };

    private static Veterinario BuildVeterinario() =>
        new() { Id = 10, IdClinica = 1, NmVeterinario = "Dr. Ana", NrCrmv = "1234" };

    private void SetupCaminhoFeliz(TriagemLuna? triagem = null)
    {
        _tutorRepoMock.Setup(r => r.GetByIdAsync(601L, 1L)).ReturnsAsync(BuildTutor());
        _petRepoMock.Setup(r => r.GetByIdComVinculosAsync(501L, 1L)).ReturnsAsync(BuildPetVinculado());
        _veterinarioRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync(BuildVeterinario());
        _relogioClinicaMock.Setup(r => r.Agora()).Returns(new DateTime(2026, 10, 7, 9, 0, 0));
        if (triagem is not null)
            _triagemLunaRepoMock.Setup(r => r.GetByIdAsync(triagem.Id, 1L)).ReturnsAsync(triagem);
    }

    [Fact]
    public async Task CriarAsync_TutorNaoEncontrado_LancaEntidadeNaoEncontrada()
    {
        // Arrange -- tutor da clínica errada (ou inexistente): o repositório escopado por
        // idClinica devolve null pros dois casos, resposta idêntica (REC-10, sem oráculo).
        _tutorRepoMock.Setup(r => r.GetByIdAsync(601L, 1L)).ReturnsAsync((Tutor?)null);

        // Act
        var act = async () => await _sut.CriarAsync(BuildCreateDto());

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        _agendamentoRepoMock.Verify(r => r.AddAsync(It.IsAny<Agendamento>()), Times.Never);
    }

    [Fact]
    public async Task CriarAsync_PetNaoEncontrado_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        _tutorRepoMock.Setup(r => r.GetByIdAsync(601L, 1L)).ReturnsAsync(BuildTutor());
        _petRepoMock.Setup(r => r.GetByIdComVinculosAsync(501L, 1L)).ReturnsAsync((Pet?)null);

        // Act
        var act = async () => await _sut.CriarAsync(BuildCreateDto());

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task CriarAsync_PetNaoVinculadoAoTutor_LancaRegraDeNegocio()
    {
        // Arrange -- pet existe na mesma clínica, mas o vínculo TutorPets é com OUTRO tutor.
        _tutorRepoMock.Setup(r => r.GetByIdAsync(601L, 1L)).ReturnsAsync(BuildTutor());
        var petDeOutroTutor = new Pet
        {
            Id = 501,
            IdClinica = 1,
            NmPet = "Rex",
            TutorPets = [new TutorPet { IdTutor = 999, IdPet = 501 }]
        };
        _petRepoMock.Setup(r => r.GetByIdComVinculosAsync(501L, 1L)).ReturnsAsync(petDeOutroTutor);

        // Act
        var act = async () => await _sut.CriarAsync(BuildCreateDto());

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>();
    }

    [Fact]
    public async Task CriarAsync_VeterinarioNaoEncontrado_LancaEntidadeNaoEncontrada()
    {
        // Arrange -- G0 item 9: o Java NÃO valida isto; o REC-10 é mais estrito de propósito.
        _tutorRepoMock.Setup(r => r.GetByIdAsync(601L, 1L)).ReturnsAsync(BuildTutor());
        _petRepoMock.Setup(r => r.GetByIdComVinculosAsync(501L, 1L)).ReturnsAsync(BuildPetVinculado());
        _veterinarioRepoMock.Setup(r => r.GetByIdAsync(10L, 1L)).ReturnsAsync((Veterinario?)null);

        // Act
        var act = async () => await _sut.CriarAsync(BuildCreateDto());

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task CriarAsync_TriagemNaoEncontrada_LancaEntidadeNaoEncontrada()
    {
        // Arrange -- triagem de outra clínica cai aqui também (mesmo repositório, mesmo
        // predicado idClinica) -- resposta idêntica à inexistente, sem oráculo.
        SetupCaminhoFeliz();
        _triagemLunaRepoMock.Setup(r => r.GetByIdAsync(800L, 1L)).ReturnsAsync((TriagemLuna?)null);

        // Act
        var act = async () => await _sut.CriarAsync(BuildCreateDto(idTriagemOrigem: 800));

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task CriarAsync_TriagemDeOutroTutorDaMesmaClinica_LancaRegraDeNegocio()
    {
        // Arrange -- G2/m-4: a FK não amarra o tutor, quem amarra é o service. A triagem
        // JÁ passou pelo filtro de clínica (existe e é da clínica 1) -- só o tutor diverge.
        var triagemDeOutroTutor = new TriagemLuna
        {
            Id = 800,
            IdClinica = 1,
            IdTutor = 999,
            DsNivelUrgencia = "ALTA",
            DsDescricao = "SEGREDO"
        };
        SetupCaminhoFeliz(triagemDeOutroTutor);

        // Act
        var act = async () => await _sut.CriarAsync(BuildCreateDto(idTriagemOrigem: 800));

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>();
    }

    [Fact]
    public async Task CriarAsync_DataDezesseisMinutosNoPassado_LancaRegraDeNegocio()
    {
        // Arrange -- ruling de encaixe: até 15 min no passado é tolerado, nunca mais.
        // DtAgendamento fixo em 09:00:00 -- 16 min atrás do "agora" mockado (09:16:00).
        SetupCaminhoFeliz();
        _relogioClinicaMock.Setup(r => r.Agora()).Returns(new DateTime(2026, 10, 7, 9, 16, 0));

        // Act
        var act = async () => await _sut.CriarAsync(new AgendamentoCreateDto
        {
            IdTutor = 601,
            IdPet = 501,
            IdVeterinario = 10,
            DtAgendamento = new DateTime(2026, 10, 7, 9, 0, 0),
            DsTipo = "CONSULTA"
        });

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>();
        _agendamentoRepoMock.Verify(r => r.AddAsync(It.IsAny<Agendamento>()), Times.Never);
    }

    [Fact]
    public async Task CriarAsync_DataQuatorzeMinutosNoPassado_Cria()
    {
        // Arrange -- dentro da tolerância de encaixe (15 min).
        SetupCaminhoFeliz();
        _relogioClinicaMock.Setup(r => r.Agora()).Returns(new DateTime(2026, 10, 7, 9, 14, 0));
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var dto = new AgendamentoCreateDto
        {
            IdTutor = 601,
            IdPet = 501,
            IdVeterinario = 10,
            DtAgendamento = new DateTime(2026, 10, 7, 9, 0, 0),
            DsTipo = "CONSULTA"
        };

        // Act
        var result = await _sut.CriarAsync(dto);

        // Assert
        result.DsStatus.Should().Be("AGENDADO");
        _agendamentoRepoMock.Verify(r => r.AddAsync(It.IsAny<Agendamento>()), Times.Once);
    }

    [Fact]
    public async Task CriarAsync_SemTriagemOrigem_GravaOrigemRecepcaoENrVersionZero()
    {
        // Arrange
        SetupCaminhoFeliz();
        Agendamento? capturado = null;
        _agendamentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Agendamento>()))
            .Callback<Agendamento>(a => capturado = a)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CriarAsync(BuildCreateDto());

        // Assert -- m-1: DsOrigem SEMPRE explícito (nunca null -- NOT NULL DEFAULT 'PORTAL',
        // e um NULL explícito no INSERT dá ORA-01400).
        capturado.Should().NotBeNull();
        capturado!.DsOrigem.Should().Be("RECEPCAO");
        capturado.IdTriagemOrigem.Should().BeNull();
        capturado.NrVersion.Should().Be(0);
        capturado.IdClinica.Should().Be(1L, "IdClinica vem do IClinicaContext, nunca do corpo (o DTO nem declara o campo)");
        capturado.StStatus.Should().Be("AGENDADO");
        capturado.DtCriacao.Should().Be(new DateTime(2026, 10, 7, 9, 0, 0), "DtCriacao vem do IRelogioClinica.Agora() -- G2-REC10/m-3");
        result.DsOrigem.Should().Be("RECEPCAO");
    }

    /// <summary>
    /// G2-REC10/m-3 — dedicado, com o relógio fixado numa data BEM diferente de "agora" real
    /// (2030, futuro distante) para que a asserção não possa ser satisfeita por coincidência
    /// nem por <c>DateTime.UtcNow</c>/<c>DateTime.Now</c> do momento em que o teste roda. A
    /// G2 mediu (mutação MI): trocar <c>DtCriacao = agora</c> por
    /// <c>DtCriacao = DateTime.UtcNow</c> deixava a suíte inteira verde -- nada travava que
    /// <c>DT_CRIACAO</c> vem do relógio de SP (REC-08/A-5), não do relógio do processo. O
    /// Java devolve este campo ao app do tutor (<c>AgendamentoResponse.fromEntity</c>) --
    /// um `DtCriacao` em UTC gravado como se fosse hora de SP é +3h de erro visível lá.
    /// </summary>
    [Fact]
    public async Task CriarAsync_DtCriacao_VemDoRelogioClinica_NaoDeUtcNow()
    {
        // Arrange
        SetupCaminhoFeliz();
        var agoraFixoDoRelogio = new DateTime(2030, 3, 15, 14, 22, 0);
        _relogioClinicaMock.Setup(r => r.Agora()).Returns(agoraFixoDoRelogio);
        // Sobrescreve a data do DTO para não colidir com o encaixe (o relógio mockado
        // acima já cobre "agora"; a data do agendamento continua no "futuro" relativo a ele).
        var dto = new AgendamentoCreateDto
        {
            IdTutor = 601,
            IdPet = 501,
            IdVeterinario = 10,
            DtAgendamento = agoraFixoDoRelogio.AddDays(1),
            DsTipo = "CONSULTA"
        };
        Agendamento? capturado = null;
        _agendamentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Agendamento>()))
            .Callback<Agendamento>(a => capturado = a)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CriarAsync(dto);

        // Assert
        capturado.Should().NotBeNull();
        capturado!.DtCriacao.Should().Be(agoraFixoDoRelogio);
        capturado.DtCriacao.Should().NotBeCloseTo(DateTime.UtcNow, TimeSpan.FromDays(1),
            "prova de que a data não veio de DateTime.UtcNow do momento do teste");
    }

    [Fact]
    public async Task CriarAsync_ComTriagemOrigem_GravaOrigemTriagemLuna()
    {
        // Arrange -- F-3: a recepção agenda pelo card da triagem.
        var triagem = new TriagemLuna
        {
            Id = 800,
            IdClinica = 1,
            IdTutor = 601,
            DsNivelUrgencia = "ALTA",
            DsDescricao = "SEGREDO-NAO-VAZA-NO-DTO"
        };
        SetupCaminhoFeliz(triagem);
        Agendamento? capturado = null;
        _agendamentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Agendamento>()))
            .Callback<Agendamento>(a => capturado = a)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CriarAsync(BuildCreateDto(idTriagemOrigem: 800));

        // Assert
        capturado.Should().NotBeNull();
        capturado!.DsOrigem.Should().Be("TRIAGEM_LUNA");
        capturado.IdTriagemOrigem.Should().Be(800L);
        result.DsOrigem.Should().Be("TRIAGEM_LUNA");
        result.DsNivelUrgenciaOrigem.Should().Be("ALTA");
    }

    [Fact]
    public async Task CriarAsync_DuracaoAusente_UsaDefault30()
    {
        // Arrange -- mesmo default do Java (Agendamento.criar/.comDuracao).
        SetupCaminhoFeliz();
        Agendamento? capturado = null;
        _agendamentoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Agendamento>()))
            .Callback<Agendamento>(a => capturado = a)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CriarAsync(BuildCreateDto());

        // Assert
        capturado!.NrDuracaoMinutos.Should().Be(30);
    }
}
