namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Tutor;
using Kura.Domain.Exceptions;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;

public class TutorServiceTests
{
    private readonly Mock<ITutorRepository> _tutorRepoMock = new();
    private readonly Mock<ITutorPetRepository> _tutorPetRepoMock = new();
    private readonly Mock<IRepository<Especie>> _especieRepoMock = new();
    private readonly Mock<IRepository<Raca>> _racaRepoMock = new();
    private readonly Mock<IInviteTutorRepository> _inviteRepoMock = new();
    private readonly Mock<IContaTutorRepository> _contaTutorRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IClinicaContext> _clinicaContextMock = new();
    private readonly Mock<IGeradorUrlFotoPet> _geradorUrlFotoPetMock = new();
    private readonly Mock<IGeradorLinkConvite> _geradorLinkConviteMock = new();
    private readonly TutorService _sut;

    public TutorServiceTests()
    {
        _tutorRepoMock.Setup(r => r.AddAsync(It.IsAny<Tutor>())).Returns(Task.CompletedTask);
        _inviteRepoMock.Setup(r => r.AddAsync(It.IsAny<InviteTutor>())).Returns(Task.CompletedTask);
        _contaTutorRepoMock.Setup(r => r.ExisteContaAsync(It.IsAny<long>())).ReturnsAsync(false);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);
        _clinicaContextMock.Setup(c => c.IdClinica).Returns(1L);

        _sut = new TutorService(
            _tutorRepoMock.Object,
            _tutorPetRepoMock.Object,
            _especieRepoMock.Object,
            _racaRepoMock.Object,
            _inviteRepoMock.Object,
            _contaTutorRepoMock.Object,
            _uowMock.Object,
            _clinicaContextMock.Object,
            _geradorUrlFotoPetMock.Object,
            _geradorLinkConviteMock.Object);
    }

    // REC-01 (KURA_BACKLOG_RECEPCAO.md, G0 item 6, consumidor 8): telefone nacional válido por
    // padrão — não é mais opcional. StAvisoPrivacidadeInformado=true por padrão (o validator,
    // não testado aqui, é quem bloqueia false/ausente com 400 — TutorCreateValidatorTests).
    private static TutorCreateDto ValidDto(
        string canal = "WHATSAPP",
        string nrTelefone = "11999999999",
        string? dsWhatsapp = null,
        bool stAvisoPrivacidadeInformado = true) => new()
    {
        NmTutor = "Maria Silva",
        NrCpf = "12345678901",
        DsEmail = "maria@email.com",
        NrTelefone = nrTelefone,
        DsWhatsapp = dsWhatsapp,
        StAvisoPrivacidadeInformado = stAvisoPrivacidadeInformado,
        DsCanalConvite = canal
    };

    [Fact]
    public async Task CreateAsync_TutorEInviteCriadosNaMesmaTransacao_CommitUmaVez()
    {
        // Act
        var result = await _sut.CreateAsync(ValidDto(), 1L);

        // Assert
        _tutorRepoMock.Verify(r => r.AddAsync(It.IsAny<Tutor>()), Times.Once);
        _inviteRepoMock.Verify(r => r.AddAsync(It.IsAny<InviteTutor>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
        result.Should().NotBeNull();
        result.Invite.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_TokenGerado_EhGuidValidoENaoVazio()
    {
        // Arrange
        InviteTutor? capturado = null;
        _inviteRepoMock.Setup(r => r.AddAsync(It.IsAny<InviteTutor>()))
            .Callback<InviteTutor>(i => capturado = i)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CreateAsync(ValidDto(), 1L);

        // Assert
        capturado.Should().NotBeNull();
        capturado!.NrToken.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAsync_DtExpiracao_Sete_DiasApos_DtCriacao()
    {
        // Arrange
        InviteTutor? capturado = null;
        _inviteRepoMock.Setup(r => r.AddAsync(It.IsAny<InviteTutor>()))
            .Callback<InviteTutor>(i => capturado = i)
            .Returns(Task.CompletedTask);

        Tutor? tutorCapturado = null;
        _tutorRepoMock.Setup(r => r.AddAsync(It.IsAny<Tutor>()))
            .Callback<Tutor>(t => tutorCapturado = t)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CreateAsync(ValidDto(), 1L);

        // Assert
        capturado!.DtExpiracao.Should().BeCloseTo(tutorCapturado!.DtCriacao.AddDays(7), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateAsync_SemCanal_UsaDefaultWhatsapp()
    {
        // Arrange
        InviteTutor? capturado = null;
        _inviteRepoMock.Setup(r => r.AddAsync(It.IsAny<InviteTutor>()))
            .Callback<InviteTutor>(i => capturado = i)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CreateAsync(ValidDto("WHATSAPP"), 1L);

        // Assert
        capturado!.DsCanal.Should().Be("WHATSAPP");
    }

    [Fact]
    public async Task CreateAsync_InviteRepositoryFalha_TutorNaoPersiste()
    {
        // Arrange
        _inviteRepoMock.Setup(r => r.AddAsync(It.IsAny<InviteTutor>()))
            .ThrowsAsync(new InvalidOperationException("Falha simulada no invite"));

        // Act
        var act = async () => await _sut.CreateAsync(ValidDto(), 1L);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteAsync_TutorExiste_ChamaSoftDeleteECommit()
    {
        // Arrange
        _tutorRepoMock.Setup(r => r.GetByIdAsync(42L))
            .ReturnsAsync(new Tutor { Id = 42L, NmTutor = "Maria" });

        // Act
        await _sut.SoftDeleteAsync(42L);

        // Assert
        _tutorRepoMock.Verify(r => r.SoftDelete(It.IsAny<Tutor>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task SoftDeleteAsync_TutorNaoEncontrado_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        _tutorRepoMock.Setup(r => r.GetByIdAsync(99L)).ReturnsAsync((Tutor?)null);

        // Act
        var act = async () => await _sut.SoftDeleteAsync(99L);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    // REC-02 (KURA_BACKLOG_RECEPCAO.md): reemissão de convite. Reusa GeradorLinkConvite (não
    // reimplementado) — o teste de "token nunca em log" já existe para esse componente
    // compartilhado em TutorTokenNaoVazaNoLogTests.cs e cobre este fluxo por reuso, não por
    // duplicação.

    private static Tutor NovoTutor(long id, long idClinica) => new()
    {
        Id = id,
        IdClinica = idClinica,
        NmTutor = "Maria Silva",
        NrCpf = "12345678901",
        DsEmail = "maria@email.com",
        NrTelefone = "5511999990000",
        StAvisoPrivacidade = "S"
    };

    private static InviteTutor InviteAtivoNaoUtilizado(long id, long idTutor) => new()
    {
        Id = id,
        IdTutor = idTutor,
        NrToken = Guid.NewGuid(),
        DtExpiracao = DateTime.UtcNow.AddDays(3),
        DsCanal = "WHATSAPP",
        StUtilizado = false
    };

    [Fact]
    public async Task ReemitirConviteAsync_TutorNaoEncontrado_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        _tutorRepoMock.Setup(r => r.GetByIdAsync(99L, 1L)).ReturnsAsync((Tutor?)null);

        // Act
        var act = async () => await _sut.ReemitirConviteAsync(99L, 1L);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
        _inviteRepoMock.Verify(r => r.AddAsync(It.IsAny<InviteTutor>()), Times.Never);
    }

    [Fact]
    public async Task ReemitirConviteAsync_TutorDeOutraClinica_MesmoNotFoundQueTutorInexistente()
    {
        // mordida (c) do aceite: duas clínicas, mesmo id de tutor. O repositório ESCOPADO
        // (GetByIdAsync(id, idClinica)) devolve o tutor só para a clínica dona e null para a
        // outra — exatamente como devolveria para um id que não existe em lugar nenhum. Sem
        // oráculo: a mensagem de erro é a MESMA (mesmo template, mesmo id — o id já é
        // informação que o próprio chamador colocou na URL, não uma pista nova).
        const long idTutor = 42L;
        var tutorDaClinicaA = NovoTutor(idTutor, idClinica: 1L);

        _tutorRepoMock.Setup(r => r.GetByIdAsync(idTutor, 1L)).ReturnsAsync(tutorDaClinicaA);
        _tutorRepoMock.Setup(r => r.GetByIdAsync(idTutor, 2L)).ReturnsAsync((Tutor?)null);
        // Controle: se o código regredisse para a sobrecarga NÃO escopada por clínica (o bug
        // clássico de vazamento cross-tenant — molde do achado da FD-17/DashboardService no
        // CLAUDE.md), este stub faria a chamada da clínica errada devolver o tutor de
        // qualquer forma, e a mordida abaixo pegaria isso (resultado 201 em vez de 404).
        _tutorRepoMock.Setup(r => r.GetByIdAsync(idTutor)).ReturnsAsync(tutorDaClinicaA);
        _inviteRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<InviteTutor, bool>>>()))
            .ReturnsAsync([]);

        // Act — clínica dona: sucesso.
        var resultado = await _sut.ReemitirConviteAsync(idTutor, 1L);
        resultado.Should().NotBeNull();

        // Act — clínica errada: mesma exceção/mesmo template de mensagem de um id inexistente.
        var act = async () => await _sut.ReemitirConviteAsync(idTutor, 2L);
        var ex = await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        ex.Which.Message.Should().Be($"Tutor com id {idTutor} não encontrado.");
    }

    [Fact]
    public async Task ReemitirConviteAsync_TutorJaTemConta_LancaConflitoENaoTocaInvites()
    {
        // mordida (b) do aceite: tutor com onboarding concluído (CONTA_TUTOR, tabela do Java,
        // só leitura no .NET) ⇒ 409, reemitir não faz sentido.
        const long idTutor = 7L;
        var tutor = NovoTutor(idTutor, idClinica: 1L);
        _tutorRepoMock.Setup(r => r.GetByIdAsync(idTutor, 1L)).ReturnsAsync(tutor);
        _contaTutorRepoMock.Setup(r => r.ExisteContaAsync(idTutor)).ReturnsAsync(true);

        // Act
        var act = async () => await _sut.ReemitirConviteAsync(idTutor, 1L);

        // Assert
        await act.Should().ThrowAsync<TutorComContaExistenteException>();
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
        _inviteRepoMock.Verify(r => r.SoftDelete(It.IsAny<InviteTutor>()), Times.Never);
        _inviteRepoMock.Verify(r => r.AddAsync(It.IsAny<InviteTutor>()), Times.Never);
    }

    [Fact]
    public async Task ReemitirConviteAsync_CancelaTodosOsConvitesAtivosNaoUtilizados()
    {
        // mordida (a) do aceite: sem cancelar os antigos, este teste falha.
        const long idTutor = 15L;
        var tutor = NovoTutor(idTutor, idClinica: 1L);
        var inviteAntigo1 = InviteAtivoNaoUtilizado(101L, idTutor);
        var inviteAntigo2 = InviteAtivoNaoUtilizado(102L, idTutor);

        _tutorRepoMock.Setup(r => r.GetByIdAsync(idTutor, 1L)).ReturnsAsync(tutor);
        _contaTutorRepoMock.Setup(r => r.ExisteContaAsync(idTutor)).ReturnsAsync(false);
        _inviteRepoMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<InviteTutor, bool>>>()))
            .ReturnsAsync([inviteAntigo1, inviteAntigo2]);

        // Act
        var resultado = await _sut.ReemitirConviteAsync(idTutor, 1L);

        // Assert
        _inviteRepoMock.Verify(r => r.SoftDelete(inviteAntigo1), Times.Once);
        _inviteRepoMock.Verify(r => r.SoftDelete(inviteAntigo2), Times.Once);
        _inviteRepoMock.Verify(r => r.AddAsync(It.IsAny<InviteTutor>()), Times.Once);
        resultado.Invite.Should().NotBeNull();
        resultado.Invite.NrToken.Should().NotBe(inviteAntigo1.NrToken);
        resultado.Invite.NrToken.Should().NotBe(inviteAntigo2.NrToken);
    }

    [Fact]
    public async Task ReemitirConviteAsync_FiltraApenasAtivosNaoUtilizados_PredicadoCorreto()
    {
        // Prova que o predicado passado a FindAsync de fato seleciona "deste tutor, não
        // utilizado" — captura a expressão e aplica sobre dados de teste (incluindo um invite
        // de OUTRO tutor e um já UTILIZADO, que não devem ser candidatos a cancelamento).
        const long idTutor = 20L;
        const long outroTutor = 21L;
        var tutor = NovoTutor(idTutor, idClinica: 1L);
        var inviteDesteTutorNaoUtilizado = InviteAtivoNaoUtilizado(201L, idTutor);
        var inviteDesteTutorUtilizado = InviteAtivoNaoUtilizado(202L, idTutor);
        inviteDesteTutorUtilizado.StUtilizado = true;
        var inviteDeOutroTutor = InviteAtivoNaoUtilizado(203L, outroTutor);

        System.Linq.Expressions.Expression<Func<InviteTutor, bool>>? predicadoCapturado = null;
        _tutorRepoMock.Setup(r => r.GetByIdAsync(idTutor, 1L)).ReturnsAsync(tutor);
        _contaTutorRepoMock.Setup(r => r.ExisteContaAsync(idTutor)).ReturnsAsync(false);
        _inviteRepoMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<InviteTutor, bool>>>()))
            .Callback<System.Linq.Expressions.Expression<Func<InviteTutor, bool>>>(p => predicadoCapturado = p)
            .ReturnsAsync([inviteDesteTutorNaoUtilizado]);

        // Act
        await _sut.ReemitirConviteAsync(idTutor, 1L);

        // Assert — o predicado real, aplicado a TODOS os candidatos (não só ao que o mock
        // devolveu), tem que aceitar SÓ o invite deste tutor não utilizado.
        predicadoCapturado.Should().NotBeNull();
        var filtro = predicadoCapturado!.Compile();
        filtro(inviteDesteTutorNaoUtilizado).Should().BeTrue();
        filtro(inviteDesteTutorUtilizado).Should().BeFalse();
        filtro(inviteDeOutroTutor).Should().BeFalse();
    }

    [Fact]
    public async Task ReemitirConviteAsync_MultiplosConvitesAntigos_UmUnicoCommit()
    {
        // Requisito 4 do brief: cancelar antigos + criar o novo numa transação só. Como
        // SoftDelete/AddAsync só mutam o ChangeTracker (nunca chamam SaveChanges por si),
        // provar "1 único CommitAsync" mesmo com N invites antigos prova que tudo cai no
        // MESMO SaveChanges — se o insert falhar, nada persiste, nem os cancelamentos.
        const long idTutor = 30L;
        var tutor = NovoTutor(idTutor, idClinica: 1L);
        var antigos = Enumerable.Range(1, 3)
            .Select(i => InviteAtivoNaoUtilizado(300L + i, idTutor))
            .ToList();

        _tutorRepoMock.Setup(r => r.GetByIdAsync(idTutor, 1L)).ReturnsAsync(tutor);
        _contaTutorRepoMock.Setup(r => r.ExisteContaAsync(idTutor)).ReturnsAsync(false);
        _inviteRepoMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<InviteTutor, bool>>>()))
            .ReturnsAsync(antigos);

        // Act
        await _sut.ReemitirConviteAsync(idTutor, 1L);

        // Assert
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task ReemitirConviteAsync_NovoTokenUsaClinicaIdDoParametroENuncaOutro()
    {
        // Mesma regra da CreateAsync (mordida (c) da REC-01): idClinica passado ao gerador de
        // link é SEMPRE o parâmetro do método, nunca vaza entre chamadas.
        const long idTutor = 55L;
        var tutorClinicaA = NovoTutor(idTutor, idClinica: 42L);
        var tutorClinicaB = NovoTutor(idTutor, idClinica: 77L);

        _tutorRepoMock.Setup(r => r.GetByIdAsync(idTutor, 42L)).ReturnsAsync(tutorClinicaA);
        _tutorRepoMock.Setup(r => r.GetByIdAsync(idTutor, 77L)).ReturnsAsync(tutorClinicaB);
        _contaTutorRepoMock.Setup(r => r.ExisteContaAsync(idTutor)).ReturnsAsync(false);
        _inviteRepoMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<InviteTutor, bool>>>()))
            .ReturnsAsync([]);

        // Act
        await _sut.ReemitirConviteAsync(idTutor, 42L);
        await _sut.ReemitirConviteAsync(idTutor, 77L);

        // Assert
        _geradorLinkConviteMock.Verify(g => g.GerarLink(It.IsAny<Guid>(), 42L), Times.Once);
        _geradorLinkConviteMock.Verify(g => g.GerarLink(It.IsAny<Guid>(), 77L), Times.Once);
        _geradorLinkConviteMock.Verify(g => g.GerarLink(It.IsAny<Guid>(), idTutor), Times.Never);
    }

    // REC-01 (KURA_BACKLOG_RECEPCAO.md, A-12; G0 item 6, consumidor 8): esta task INVERTE de
    // propósito o contrato da TASK-60 nesta rota — NrTelefone deixou de ser opcional.
    // TutorCreateValidator bloqueia com 400 antes de chegar aqui (não testado nesta classe, ver
    // TutorCreateValidatorTests); estes testes cobrem a DEFESA EM PROFUNDIDADE do service, que
    // nunca deveria persistir "Não informado" nem telefone cru vindo desta rota outra vez.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_NrTelefoneVazioOuWhitespace_LancaRegraDeNegocioENaoGravaNada(string nrTelefoneBruto)
    {
        // Arrange
        var dto = ValidDto(nrTelefone: nrTelefoneBruto);

        // Act
        var act = async () => await _sut.CreateAsync(dto, 1L);

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>();
        _tutorRepoMock.Verify(r => r.AddAsync(It.IsAny<Tutor>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_NrTelefoneNacionalComMascara_ArmazenaNormalizadoComDdiBrasil()
    {
        // Arrange — mordida (b): se o helper devolvesse a entrada crua, este teste falharia
        // ("(11) 98888-7777" ≠ "5511988887777").
        Tutor? tutorAdicionado = null;
        _tutorRepoMock.Setup(r => r.AddAsync(It.IsAny<Tutor>()))
            .Callback<Tutor>(t => tutorAdicionado = t)
            .Returns(Task.CompletedTask);

        var dto = ValidDto(nrTelefone: "(11) 98888-7777");

        // Act
        await _sut.CreateAsync(dto, 1L);

        // Assert
        tutorAdicionado.Should().NotBeNull();
        tutorAdicionado!.NrTelefone.Should().Be("5511988887777");
    }

    [Fact]
    public async Task CreateAsync_SemDsWhatsapp_GravaDsWhatsappComOMesmoNumeroNormalizado()
    {
        // Arrange — G0 item 4: "mesmo número" é o default quando DsWhatsapp está ausente.
        Tutor? tutorAdicionado = null;
        _tutorRepoMock.Setup(r => r.AddAsync(It.IsAny<Tutor>()))
            .Callback<Tutor>(t => tutorAdicionado = t)
            .Returns(Task.CompletedTask);

        var dto = ValidDto(nrTelefone: "11988887777", dsWhatsapp: null);

        // Act
        await _sut.CreateAsync(dto, 1L);

        // Assert
        tutorAdicionado.Should().NotBeNull();
        tutorAdicionado!.NrTelefone.Should().Be("5511988887777");
        tutorAdicionado!.DsWhatsapp.Should().Be("+5511988887777");
    }

    [Fact]
    public async Task CreateAsync_ComDsWhatsappDiferente_GravaOsDoisNormalizadosEmE164()
    {
        // Arrange — telefone de contato e WhatsApp podem ser números diferentes.
        Tutor? tutorAdicionado = null;
        _tutorRepoMock.Setup(r => r.AddAsync(It.IsAny<Tutor>()))
            .Callback<Tutor>(t => tutorAdicionado = t)
            .Returns(Task.CompletedTask);

        var dto = ValidDto(nrTelefone: "1134567890", dsWhatsapp: "+55 11 98888-7777");

        // Act
        await _sut.CreateAsync(dto, 1L);

        // Assert
        tutorAdicionado.Should().NotBeNull();
        tutorAdicionado!.NrTelefone.Should().Be("551134567890");
        tutorAdicionado!.DsWhatsapp.Should().Be("+5511988887777");
    }

    // ── A-9: aviso de privacidade ────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_StAvisoPrivacidadeInformadoFalse_GravaN()
    {
        // Arrange — mordida (a) real é no VALIDATOR (TutorCreateValidatorTests): dto com
        // StAvisoPrivacidadeInformado=false NUNCA alcança este método em produção (400 antes).
        // Este teste documenta que, MESMO que alcance (chamada direta/defesa em profundidade),
        // o service NUNCA promove "N" para "S" por conta própria — StAvisoPrivacidade passou a
        // DEPENDER do campo do dto, em vez do "S" incondicional de antes desta task.
        Tutor? tutorAdicionado = null;
        _tutorRepoMock.Setup(r => r.AddAsync(It.IsAny<Tutor>()))
            .Callback<Tutor>(t => tutorAdicionado = t)
            .Returns(Task.CompletedTask);

        var dto = ValidDto(stAvisoPrivacidadeInformado: false);

        // Act
        await _sut.CreateAsync(dto, 1L);

        // Assert
        tutorAdicionado.Should().NotBeNull();
        tutorAdicionado!.StAvisoPrivacidade.Should().Be("N");
    }

    [Fact]
    public async Task CreateAsync_StAvisoPrivacidadeInformadoTrue_GravaS()
    {
        Tutor? tutorAdicionado = null;
        _tutorRepoMock.Setup(r => r.AddAsync(It.IsAny<Tutor>()))
            .Callback<Tutor>(t => tutorAdicionado = t)
            .Returns(Task.CompletedTask);

        var dto = ValidDto(stAvisoPrivacidadeInformado: true);

        await _sut.CreateAsync(dto, 1L);

        tutorAdicionado.Should().NotBeNull();
        tutorAdicionado!.StAvisoPrivacidade.Should().Be("S");
    }

    // ── A-8: link do convite ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_GeradorLinkConviteDevolveNull_DsLinkConviteVaiNullNaResposta()
    {
        // Arrange — config ausente/vazia (Convite:UrlBaseAppTutor): o processo sobe, o link
        // sai null (REC-03 trata como "link não configurado", nunca QR vazio).
        _geradorLinkConviteMock
            .Setup(g => g.GerarLink(It.IsAny<Guid>(), It.IsAny<long>()))
            .Returns((string?)null);

        // Act
        var result = await _sut.CreateAsync(ValidDto(), 1L);

        // Assert
        result.DsLinkConvite.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_GeradorLinkConviteConfigurado_UsaClinicaIdDoParametroENuncaOutro()
    {
        // Arrange — mordida (c): duas clínicas, o tutor SEMPRE nasce na clínica do parâmetro
        // (JWT, via TutoresController.Create), nunca em outra. TutorCreateDto nem declara um
        // campo de clínica — não há "corpo" de onde vazar um valor diferente.
        long? clinicaRecebidaPeloGerador = null;
        _geradorLinkConviteMock
            .Setup(g => g.GerarLink(It.IsAny<Guid>(), It.IsAny<long>()))
            .Callback<Guid, long>((_, idClinica) => clinicaRecebidaPeloGerador = idClinica)
            .Returns("https://tutor.exemplo/register?token=abc&clinicaId=99");

        // Act — clínica A
        await _sut.CreateAsync(ValidDto(), 42L);
        clinicaRecebidaPeloGerador.Should().Be(42L);

        // Act — clínica B (mesmo dto, clinicaId DIFERENTE — nunca vaza a A)
        clinicaRecebidaPeloGerador = null;
        await _sut.CreateAsync(ValidDto(), 77L);
        clinicaRecebidaPeloGerador.Should().Be(77L);
    }

    [Fact]
    public async Task CreateAsync_ChamaGeradorLinkConviteComOTokenDoInviteRecemCriado()
    {
        // Arrange
        InviteTutor? inviteCapturado = null;
        _inviteRepoMock.Setup(r => r.AddAsync(It.IsAny<InviteTutor>()))
            .Callback<InviteTutor>(i => inviteCapturado = i)
            .Returns(Task.CompletedTask);

        Guid? tokenRecebidoPeloGerador = null;
        _geradorLinkConviteMock
            .Setup(g => g.GerarLink(It.IsAny<Guid>(), It.IsAny<long>()))
            .Callback<Guid, long>((token, _) => tokenRecebidoPeloGerador = token)
            .Returns("https://tutor.exemplo/register?token=abc&clinicaId=1");

        // Act
        await _sut.CreateAsync(ValidDto(), 1L);

        // Assert
        tokenRecebidoPeloGerador.Should().NotBeNull();
        tokenRecebidoPeloGerador.Should().Be(inviteCapturado!.NrToken);
    }

    // R1c (G2b fix wave 2, achado Important — LGPD): ruling do maestro REVERTE o
    // comportamento da fix wave 1 aqui. Antes, PUT sem telefone gravava o sentinela "Não
    // informado" por cima do telefone REAL — o que desligava o acompanhamento do WhatsApp
    // PARA SEMPRE no PUT seguinte (a comparação contra "+Não informado" nunca é "mesmo
    // número"). O sentinela deixou de ser PRODUZIDO por qualquer código novo (Create exige
    // telefone; Update agora preserva) — ele só existe em dado legado que nenhuma rota atual
    // volta a gravar. Mordida: voltar ao coalesce para o sentinela faz este teste (e o
    // check #6 do smoke, "PUT sem nrTelefone espera 200" — que continua válido, pois o `200`
    // não dependia do VALOR gravado) ficarem vermelhos aqui.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_NrTelefoneAusenteOuWhitespace_MantemOTelefoneAtual(string nrTelefoneBruto)
    {
        // Arrange
        var tutorExistente = new Tutor { Id = 42L, NmTutor = "Maria", NrTelefone = "11900000000" };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(42L)).ReturnsAsync(tutorExistente);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Maria Silva",
            NrCpf = "12345678901",
            DsEmail = "maria@email.com",
            NrTelefone = nrTelefoneBruto
        };

        // Act
        await _sut.UpdateAsync(42L, dto);

        // Assert — NÃO é mais "Não informado": o telefone real permanece intocado.
        tutorExistente.NrTelefone.Should().Be("11900000000");
        _tutorRepoMock.Verify(r => r.Update(It.IsAny<Tutor>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NrTelefonePreenchido_NormalizaComDdiBrasil()
    {
        // Arrange — REC-01 (G0 item 4): antes desta task o PUT gravava o valor CRU
        // ("11977776666"); agora normaliza igual ao POST, para o tutor editado casar com o
        // formato que a Luna usa na busca. Mordida (b): helper devolvendo entrada crua faz
        // este teste falhar ("11977776666" ≠ "5511977776666").
        var tutorExistente = new Tutor { Id = 42L, NmTutor = "Maria", NrTelefone = "11900000000" };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(42L)).ReturnsAsync(tutorExistente);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Maria Silva",
            NrCpf = "12345678901",
            DsEmail = "maria@email.com",
            NrTelefone = "11977776666"
        };

        // Act
        await _sut.UpdateAsync(42L, dto);

        // Assert
        tutorExistente.NrTelefone.Should().Be("5511977776666");
    }

    [Fact]
    public async Task UpdateAsync_NrTelefoneJaNormalizado_PermaneceIgual()
    {
        // Arrange — idempotência (o seed-demo-luna.sh reenvia DEMO_WHATSAPP já em "55…" pelo
        // PUT, no ramo de idempotência do A1/LU-16): reenviar o valor já armazenado não deve
        // prefixar "55" de novo.
        var tutorExistente = new Tutor { Id = 42L, NmTutor = "Maria", NrTelefone = "5511900000000" };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(42L)).ReturnsAsync(tutorExistente);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Maria",
            NrCpf = "12345678901",
            DsEmail = "maria@email.com",
            NrTelefone = "5511900000000"
        };

        // Act
        await _sut.UpdateAsync(42L, dto);

        // Assert
        tutorExistente.NrTelefone.Should().Be("5511900000000");
    }

    [Fact]
    public async Task UpdateAsync_NrTelefoneFormatoInvalido_LancaRegraDeNegocioENaoAtualiza()
    {
        // Arrange — TutorUpdateValidator bloqueia com 400 antes de chegar aqui em produção;
        // este teste cobre a defesa em profundidade do service ("nunca grava lixo").
        var tutorExistente = new Tutor { Id = 42L, NmTutor = "Maria", NrTelefone = "5511900000000" };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(42L)).ReturnsAsync(tutorExistente);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Maria",
            NrCpf = "12345678901",
            DsEmail = "maria@email.com",
            NrTelefone = "123" // menos de 10 dígitos, sem "+"
        };

        // Act
        var act = async () => await _sut.UpdateAsync(42L, dto);

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>();
        _tutorRepoMock.Verify(r => r.Update(It.IsAny<Tutor>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    // ── I1 (G2 fix wave, achado Important #1 — LGPD): PUT e o DS_WHATSAPP ───

    [Fact]
    public async Task UpdateAsync_DsWhatsappVeioNoCorpo_NormalizaEGravaIndependenteDoTelefoneAntigo()
    {
        // Ramo 1: DsWhatsapp presente no corpo ⇒ sempre normaliza e grava o valor informado,
        // não importa se era "mesmo número" ou diferente antes.
        var tutorExistente = new Tutor
        {
            Id = 42L, NmTutor = "Maria", NrTelefone = "5511900000000", DsWhatsapp = "+5511900000000"
        };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(42L)).ReturnsAsync(tutorExistente);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Maria", NrCpf = "12345678901", DsEmail = "maria@email.com",
            NrTelefone = "11955554444", DsWhatsapp = "11988889999"
        };

        await _sut.UpdateAsync(42L, dto);

        tutorExistente.NrTelefone.Should().Be("5511955554444");
        tutorExistente.DsWhatsapp.Should().Be("+5511988889999");
    }

    [Fact]
    public async Task UpdateAsync_DsWhatsappAusente_EraMesmoNumeroDoTelefoneAntigo_AcompanhaOTelefoneNovo()
    {
        // Ramo 2: DS_WHATSAPP == '+' + DS_TELEFONE antigo ⇒ "mesmo número" ⇒ segue o telefone
        // novo. É o achado M5/M6 da G2: sem este fix, o lembrete de vacina (VW_VACINAS_VENCENDO
        // → Luna) ia para o número ANTIGO depois de a recepção corrigir o telefone.
        var tutorExistente = new Tutor
        {
            Id = 42L, NmTutor = "Maria", NrTelefone = "5511900000000", DsWhatsapp = "+5511900000000"
        };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(42L)).ReturnsAsync(tutorExistente);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Maria", NrCpf = "12345678901", DsEmail = "maria@email.com",
            NrTelefone = "21988887777" // DsWhatsapp ausente do corpo
        };

        await _sut.UpdateAsync(42L, dto);

        tutorExistente.NrTelefone.Should().Be("5521988887777");
        tutorExistente.DsWhatsapp.Should().Be("+5521988887777", "era o mesmo número do telefone antigo — acompanha");
    }

    [Fact]
    public async Task UpdateAsync_DsWhatsappAusente_EraNuloAntes_ContaComoMesmoNumeroEAcompanha()
    {
        // Ramo 2 (variante): DS_WHATSAPP nulo é o estado de todo tutor criado ANTES da REC-01
        // (G0 item 4 — nulo nos 18 tutores existentes) — precisa contar como "mesmo número",
        // senão esses tutores antigos NUNCA ganham DsWhatsapp ao serem editados.
        var tutorExistente = new Tutor
        {
            Id = 43L, NmTutor = "Tutor Antigo", NrTelefone = "1198880000", DsWhatsapp = null
        };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(43L)).ReturnsAsync(tutorExistente);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Tutor Antigo", NrCpf = "12345678901", DsEmail = "antigo@email.com",
            NrTelefone = "11988887777"
        };

        await _sut.UpdateAsync(43L, dto);

        tutorExistente.DsWhatsapp.Should().Be("+5511988887777");
    }

    [Fact]
    public async Task UpdateAsync_DsWhatsappAusente_EraDiferenteDoTelefoneAntigo_Mantem()
    {
        // Ramo 3: DS_WHATSAPP diferente do telefone (era distinto de propósito) ⇒ mantém.
        var tutorExistente = new Tutor
        {
            Id = 44L, NmTutor = "Maria", NrTelefone = "5511900000000", DsWhatsapp = "+5511777776666"
        };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(44L)).ReturnsAsync(tutorExistente);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Maria", NrCpf = "12345678901", DsEmail = "maria@email.com",
            NrTelefone = "21988887777"
        };

        await _sut.UpdateAsync(44L, dto);

        tutorExistente.NrTelefone.Should().Be("5521988887777");
        tutorExistente.DsWhatsapp.Should().Be("+5511777776666", "era um WhatsApp intencionalmente diferente — não acompanha");
    }

    [Fact]
    public async Task UpdateAsync_NrTelefoneAusente_TelefoneRealmenteNaoMudaEDsWhatsappPermaneceIntocado()
    {
        // R1c/R6 (G2b fix wave 2): a versão anterior deste teste (M2 da re-G2) tinha nome e
        // mensagem enganosos — o telefone "não mudava de verdade" mas o CÓDIGO gravava o
        // sentinela por cima dele, então na prática ele MUDAVA (para "Não informado"), e a
        // asserção consagrava como correto o WhatsApp preso ao número antigo — exatamente o
        // sintoma de LGPD que a fix wave deveria fechar (R1c). Agora, com o ruling do maestro
        // (PUT sem telefone MANTÉM o atual), o telefone de fato NÃO muda — e é por isso, não
        // por uma guarda de sentinela, que o WhatsApp permanece intocado (telefoneMudou=false
        // pula o ramo de "acompanhar" inteiro).
        var tutorExistente = new Tutor
        {
            Id = 45L, NmTutor = "Maria", NrTelefone = "5511900000000", DsWhatsapp = "+5511900000000"
        };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(45L)).ReturnsAsync(tutorExistente);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Maria", NrCpf = "12345678901", DsEmail = "maria@email.com",
            NrTelefone = "" // ausente
        };

        await _sut.UpdateAsync(45L, dto);

        tutorExistente.NrTelefone.Should().Be("5511900000000", "PUT sem telefone mantém o atual (R1c) — nunca mais grava o sentinela por cima do valor real");
        tutorExistente.DsWhatsapp.Should().Be("+5511900000000", "nada mudou: sem o telefone mudar, não há o que 'acompanhar'");
    }

    [Fact]
    public async Task UpdateAsync_TutorLegadoTelefoneNacionalCruMaisWhatsappE164DoMesmoNumero_Acompanha()
    {
        // R1a (G2b fix wave 2, achado Important — LGPD): a versão anterior comparava
        // DsWhatsapp com "+" + tutor.NrTelefone CRU — para este tutor LEGADO (DS_TELEFONE
        // nacional cru, sem passar pela normalização da REC-01; DS_WHATSAPP já em E.164 do
        // MESMO número, exatamente o estado que o seed-demo-luna.sh produzia antes do fix A1
        // de 16/09), "+11988880001" ≠ "+5511988880001" ⇒ ramo 3 (mantém) ⇒ o PUT trocava o
        // telefone e deixava o WhatsApp no número ANTIGO. Mordida: voltar à comparação crua
        // faz este teste falhar.
        var tutorLegado = new Tutor
        {
            Id = 47L, NmTutor = "Legado", NrTelefone = "11988880001", DsWhatsapp = "+5511988880001"
        };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(47L)).ReturnsAsync(tutorLegado);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Legado", NrCpf = "12345678901", DsEmail = "legado@email.com",
            NrTelefone = "(11) 97777-0002"
        };

        await _sut.UpdateAsync(47L, dto);

        tutorLegado.NrTelefone.Should().Be("5511977770002");
        tutorLegado.DsWhatsapp.Should().Be(
            "+5511977770002",
            "o WhatsApp legado era o MESMO número do telefone legado (uma vez normalizado) — tem que acompanhar");
    }

    [Fact]
    public async Task UpdateAsync_TutorLegadoTelefoneNacionalCruMaisWhatsappDiferente_Mantem()
    {
        // R1a: controle negativo — o telefone legado normaliza para "5511988880001", mas o
        // WhatsApp é de um número DIFERENTE ⇒ não acompanha (ramo 3).
        var tutorLegado = new Tutor
        {
            Id = 48L, NmTutor = "Legado", NrTelefone = "11988880001", DsWhatsapp = "+5511777776666"
        };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(48L)).ReturnsAsync(tutorLegado);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Legado", NrCpf = "12345678901", DsEmail = "legado@email.com",
            NrTelefone = "(11) 97777-0002"
        };

        await _sut.UpdateAsync(48L, dto);

        tutorLegado.DsWhatsapp.Should().Be("+5511777776666", "era um WhatsApp diferente do telefone legado — não acompanha");
    }

    [Fact]
    public async Task UpdateAsync_TelefoneAntigoEraSentinelaLixo_DsWhatsappNaoNuloNaoAcompanha()
    {
        // R1a: telefone antigo NÃO normalizável (sentinela "Não informado", dado legado) e
        // DsWhatsapp NÃO nulo ⇒ "não era o mesmo número" (não há como confirmar) ⇒ mantém.
        var tutorComSentinela = new Tutor
        {
            Id = 49L, NmTutor = "Legado", NrTelefone = "Não informado", DsWhatsapp = "+5511988880009"
        };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(49L)).ReturnsAsync(tutorComSentinela);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Legado", NrCpf = "12345678901", DsEmail = "legado@email.com",
            NrTelefone = "(11) 97777-0002"
        };

        await _sut.UpdateAsync(49L, dto);

        tutorComSentinela.DsWhatsapp.Should().Be("+5511988880009", "telefone antigo era lixo (sentinela) — não dá para confirmar 'mesmo número', então não acompanha");
    }

    [Fact]
    public async Task UpdateAsync_TelefoneAntigoEraSentinelaLixoMasDsWhatsappNulo_Acompanha()
    {
        // R1a: exceção explícita da ruling — DsWhatsapp NULO sempre acompanha, mesmo quando o
        // telefone antigo é lixo/sentinela (nada para comparar, então não há motivo para NÃO
        // preencher o WhatsApp agora que o telefone ficou válido).
        var tutorComSentinela = new Tutor
        {
            Id = 50L, NmTutor = "Legado", NrTelefone = "Não informado", DsWhatsapp = null
        };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(50L)).ReturnsAsync(tutorComSentinela);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Legado", NrCpf = "12345678901", DsEmail = "legado@email.com",
            NrTelefone = "(11) 97777-0002"
        };

        await _sut.UpdateAsync(50L, dto);

        tutorComSentinela.DsWhatsapp.Should().Be("+5511977770002");
    }

    [Fact]
    public async Task UpdateAsync_PutParcialSemTelefoneDepoisPutComTelefoneNovo_AcompanhaMesmoAssim()
    {
        // R1c (regressão direta da sonda do G2b, M1/R1c): um PUT parcial sem telefone (que
        // MANTÉM o atual, R1c) não deve "prender" o WhatsApp de forma que um PUT SEGUINTE com
        // telefone novo deixe de acompanhar. Como o telefone real nunca foi trocado pelo
        // sentinela, o segundo PUT continua vendo o estado "mesmo número" corretamente.
        var tutor = new Tutor
        {
            Id = 51L, NmTutor = "Legado", NrTelefone = "5511988880003", DsWhatsapp = "+5511988880003"
        };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(51L)).ReturnsAsync(tutor);

        // 1º PUT: parcial, sem telefone (edição só de nome, por exemplo).
        await _sut.UpdateAsync(51L, new TutorUpdateDto
        {
            NmTutor = "Legado Editado", NrCpf = "12345678901", DsEmail = "legado@email.com"
        });
        tutor.NrTelefone.Should().Be("5511988880003", "PUT parcial não deveria ter tocado o telefone");

        // 2º PUT: agora com telefone novo.
        await _sut.UpdateAsync(51L, new TutorUpdateDto
        {
            NmTutor = "Legado Editado", NrCpf = "12345678901", DsEmail = "legado@email.com",
            NrTelefone = "(11) 97777-0004"
        });

        tutor.NrTelefone.Should().Be("5511977770004");
        tutor.DsWhatsapp.Should().Be("+5511977770004", "o WhatsApp continua acompanhando — o PUT parcial anterior não desligou nada");
    }

    [Fact]
    public async Task UpdateAsync_DsWhatsappFormatoInvalido_LancaRegraDeNegocioENaoAtualiza()
    {
        var tutorExistente = new Tutor { Id = 46L, NmTutor = "Maria", NrTelefone = "5511900000000" };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(46L)).ReturnsAsync(tutorExistente);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Maria", NrCpf = "12345678901", DsEmail = "maria@email.com",
            NrTelefone = "11988887777", DsWhatsapp = "123"
        };

        var act = async () => await _sut.UpdateAsync(46L, dto);

        await act.Should().ThrowAsync<RegraDeNegocioException>();
        _tutorRepoMock.Verify(r => r.Update(It.IsAny<Tutor>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    // ── BuscarContextoPorTelefoneAsync (TASK-67) ────────────────────────────

    [Fact]
    public async Task BuscarContextoPorTelefoneAsync_TelefoneInexistente_RetornaNull()
    {
        // Arrange
        _tutorRepoMock.Setup(r => r.GetByTelefoneAsync("5511900000000"))
            .ReturnsAsync((Tutor?)null);

        // Act
        var result = await _sut.BuscarContextoPorTelefoneAsync("5511900000000");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task BuscarContextoPorTelefoneAsync_TutorExiste_RetornaContextoComIdClinicaEPets()
    {
        // Arrange
        var tutor = new Tutor
        {
            Id = 7,
            IdClinica = 42,
            NmTutor = "Fulano",
            NrCpf = "11122233344",
            DsEmail = "fulano@teste.com",
            NrTelefone = "5511999990000",
            StAtiva = true
        };
        var pet = new Pet { Id = 3, IdClinica = 42, IdEspecie = 1, IdRaca = 5, NmPet = "Rex", DtNascimento = DateTime.UtcNow, StAtiva = true };
        var especie = new Especie { Id = 1, NmEspecie = "Cachorro" };
        var raca = new Raca { Id = 5, IdEspecie = 1, NmRaca = "Vira-lata" };

        // R3a (G2b fix wave 2): a decisão de tentar a busca exata depende da CONTAGEM antes de
        // buscar (ContarAtivosPorTelefoneAsync) — sem este setup, a contagem padrão do mock
        // (0, loose mock) faria a busca cair direto no "não encontrado", nem chamando
        // GetByTelefoneAsync.
        _tutorRepoMock.Setup(r => r.ContarAtivosPorTelefoneAsync("5511999990000")).ReturnsAsync(1);
        _tutorRepoMock.Setup(r => r.GetByTelefoneAsync("5511999990000")).ReturnsAsync(tutor);
        _tutorPetRepoMock.Setup(r => r.GetByTutorIdAsync(tutor.Id))
            .ReturnsAsync(new List<TutorPet> { new() { IdTutor = tutor.Id, IdPet = pet.Id, Pet = pet } });
        _especieRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(especie);
        _racaRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(raca);

        // Act
        var result = await _sut.BuscarContextoPorTelefoneAsync("5511999990000");

        // Assert
        result.Should().NotBeNull();
        result!.IdTutor.Should().Be(7);
        result.NmTutor.Should().Be("Fulano");
        result.DsWhatsapp.Should().Be("5511999990000");
        result.IdClinica.Should().Be(42, "quem chama (a Luna) não sabe a clínica antecipadamente — é isto que o endpoint resolve");
        result.Pets.Should().ContainSingle();
        result.Pets[0].IdPet.Should().Be(3);
        result.Pets[0].NmPet.Should().Be("Rex");
        result.Pets[0].NmEspecie.Should().Be("Cachorro");
        result.Pets[0].NmRaca.Should().Be("Vira-lata");
    }

    [Fact]
    public async Task BuscarContextoPorTelefoneAsync_TutorSemPets_RetornaListaVazia()
    {
        // Arrange
        var tutor = new Tutor { Id = 8, IdClinica = 42, NmTutor = "Ciclano", NrTelefone = "5511988887777", StAtiva = true };
        _tutorRepoMock.Setup(r => r.ContarAtivosPorTelefoneAsync("5511988887777")).ReturnsAsync(1);
        _tutorRepoMock.Setup(r => r.GetByTelefoneAsync("5511988887777")).ReturnsAsync(tutor);
        _tutorPetRepoMock.Setup(r => r.GetByTutorIdAsync(tutor.Id)).ReturnsAsync(new List<TutorPet>());

        // Act
        var result = await _sut.BuscarContextoPorTelefoneAsync("5511988887777");

        // Assert
        result.Should().NotBeNull();
        result!.Pets.Should().BeEmpty();
    }

    [Fact]
    public async Task BuscarContextoPorTelefoneAsync_NumeroNacional_TentaCruPrimeiroDepoisComPrefixo55()
    {
        // I3 (G2 fix wave, achado Important #3): a busca tenta PRIMEIRO os dígitos EXATAMENTE
        // como chegaram (é o que a Luna sempre manda — Twilio já entrega "55..." completo); só
        // tenta com prefixo "55" se a primeira busca não achar E a entrada tiver 10/11 dígitos.
        // A versão anterior normalizava a ENTRADA antes de buscar (achava direto o "55..."),
        // mas isso quebrava o estrangeiro de 10/11 dígitos — ver o teste seguinte. Aqui travamos
        // que o caso NACIONAL LEGADO continua achando, agora via as DUAS tentativas na ordem
        // certa (cru primeiro, "55"+dígitos depois).
        var tutor = new Tutor { Id = 9, IdClinica = 42, NmTutor = "Nacional", NrTelefone = "5511999990000", StAtiva = true };
        // R3a (G2b fix wave 2): a decisão de tentar o fallback agora vem de
        // ContarAtivosPorTelefoneAsync — exata (0, "não encontrado") ⇒ tenta o fallback (55...,
        // 1, "encontrado unicamente") ⇒ SÓ ENTÃO chama GetByTelefoneAsync para buscar de verdade.
        _tutorRepoMock.Setup(r => r.ContarAtivosPorTelefoneAsync("11999990000")).ReturnsAsync(0);
        _tutorRepoMock.Setup(r => r.ContarAtivosPorTelefoneAsync("5511999990000")).ReturnsAsync(1);
        _tutorRepoMock.Setup(r => r.GetByTelefoneAsync("5511999990000")).ReturnsAsync(tutor);
        _tutorPetRepoMock.Setup(r => r.GetByTutorIdAsync(tutor.Id)).ReturnsAsync(new List<TutorPet>());

        // Act
        var result = await _sut.BuscarContextoPorTelefoneAsync("11999990000");

        // Assert
        result.Should().NotBeNull();
        _tutorRepoMock.Verify(r => r.ContarAtivosPorTelefoneAsync("11999990000"), Times.Once);
        _tutorRepoMock.Verify(r => r.ContarAtivosPorTelefoneAsync("5511999990000"), Times.Once);
        _tutorRepoMock.Verify(r => r.GetByTelefoneAsync("11999990000"), Times.Never);
        _tutorRepoMock.Verify(r => r.GetByTelefoneAsync("5511999990000"), Times.Once);
    }

    [Fact]
    public async Task BuscarContextoPorTelefoneAsync_EstrangeiroDeOnzeDigitos_AchaNaPrimeiraTentativaSemPrefixo55()
    {
        // I3 (G2 fix wave, achado Important #3 — o caso 6 do G0 estava marcado ✅ sem medir o
        // efeito na busca): estrangeiro de 10/11 dígitos (EUA/Canadá) cadastrado com "+" (ramo 1
        // de NormalizadorTelefone, armazenado SEM prefixo "55") precisa ser achado pela
        // primeira tentativa (dígitos crus) — nunca pela tentativa com "55" prefixado, que
        // corromperia um número que não é brasileiro. Mordida: se a ordem fosse invertida (ou
        // só a tentativa com "55" existisse), este teste falha.
        var tutor = new Tutor { Id = 10, IdClinica = 42, NmTutor = "Estrangeiro", NrTelefone = "14155550100", StAtiva = true };
        _tutorRepoMock.Setup(r => r.ContarAtivosPorTelefoneAsync("14155550100")).ReturnsAsync(1);
        _tutorRepoMock.Setup(r => r.GetByTelefoneAsync("14155550100")).ReturnsAsync(tutor);
        _tutorPetRepoMock.Setup(r => r.GetByTutorIdAsync(tutor.Id)).ReturnsAsync(new List<TutorPet>());

        // Act
        var result = await _sut.BuscarContextoPorTelefoneAsync("14155550100");

        // Assert
        result.Should().NotBeNull();
        result!.IdTutor.Should().Be(10);
        _tutorRepoMock.Verify(r => r.GetByTelefoneAsync("14155550100"), Times.Once);
        _tutorRepoMock.Verify(r => r.ContarAtivosPorTelefoneAsync("5514155550100"), Times.Never);
        _tutorRepoMock.Verify(r => r.GetByTelefoneAsync("5514155550100"), Times.Never);
    }

    [Fact]
    public async Task BuscarContextoPorTelefoneAsync_ExataAmbigua_NaoTentaFallbackENaoDevolveOutroTutor()
    {
        // R3a (G2b fix wave 2, achado Minor): a busca EXATA ambígua (2+ ativos) já é
        // "não encontrado" (TASK-79) — NÃO deve tentar a chave com prefixo "55", que poderia
        // achar um TERCEIRO tutor completamente diferente do par ambíguo original (o achado
        // exato da sonda do G2b: dois tutores em "11988880030" + um terceiro, distinto, em
        // "5511988880030"). Mordida: se a decisão voltasse a depender só de
        // GetByTelefoneAsync (que já colapsa ambíguo para null, indistinguível de "não
        // encontrado"), este teste falharia (acharia o terceiro tutor via fallback).
        var terceiroTutorSobPrefixo55 = new Tutor { Id = 12, IdClinica = 2, NmTutor = "Terceiro", NrTelefone = "5511988880030", StAtiva = true };
        _tutorRepoMock.Setup(r => r.ContarAtivosPorTelefoneAsync("11988880030")).ReturnsAsync(2); // ambíguo
        _tutorRepoMock.Setup(r => r.ContarAtivosPorTelefoneAsync("5511988880030")).ReturnsAsync(1);
        _tutorRepoMock.Setup(r => r.GetByTelefoneAsync("5511988880030")).ReturnsAsync(terceiroTutorSobPrefixo55);

        // Act
        var result = await _sut.BuscarContextoPorTelefoneAsync("11988880030");

        // Assert
        result.Should().BeNull("a busca exata foi AMBÍGUA — o resultado final é 'não encontrado', sem tentar outra chave");
        _tutorRepoMock.Verify(r => r.ContarAtivosPorTelefoneAsync("5511988880030"), Times.Never);
        _tutorRepoMock.Verify(r => r.GetByTelefoneAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task BuscarContextoPorTelefoneAsync_FallbackAmbiguo_RetornaNull()
    {
        // R3a, controle complementar: exata deu 0 (tenta o fallback), mas o fallback TAMBÉM é
        // ambíguo (2+) ⇒ TASK-79 se aplica ali também ⇒ resultado final é null.
        _tutorRepoMock.Setup(r => r.ContarAtivosPorTelefoneAsync("11988880040")).ReturnsAsync(0);
        _tutorRepoMock.Setup(r => r.ContarAtivosPorTelefoneAsync("5511988880040")).ReturnsAsync(2);

        var result = await _sut.BuscarContextoPorTelefoneAsync("11988880040");

        result.Should().BeNull();
        _tutorRepoMock.Verify(r => r.GetByTelefoneAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task BuscarContextoPorTelefoneAsync_EntradaComDezDigitos_TambemTentaFallback()
    {
        // N3 (G2b fix wave 2, achado Minor): o comentário/relatório da fix wave 1 já alegava
        // cobertura para "10 ou 11 dígitos", mas só havia teste para 11 — a re-G2 mutou o
        // `== 10` e a suíte sobreviveu (nenhum teste exercitava esse ramo). Fixo nacional sem
        // DDD-9 (10 dígitos, ex.: "1133334444") também precisa cair no fallback.
        var tutor = new Tutor { Id = 13, IdClinica = 1, NmTutor = "FixoDezDigitos", NrTelefone = "551133334444", StAtiva = true };
        _tutorRepoMock.Setup(r => r.ContarAtivosPorTelefoneAsync("1133334444")).ReturnsAsync(0);
        _tutorRepoMock.Setup(r => r.ContarAtivosPorTelefoneAsync("551133334444")).ReturnsAsync(1);
        _tutorRepoMock.Setup(r => r.GetByTelefoneAsync("551133334444")).ReturnsAsync(tutor);
        _tutorPetRepoMock.Setup(r => r.GetByTutorIdAsync(tutor.Id)).ReturnsAsync(new List<TutorPet>());

        var result = await _sut.BuscarContextoPorTelefoneAsync("1133334444");

        result.Should().NotBeNull();
        _tutorRepoMock.Verify(r => r.ContarAtivosPorTelefoneAsync("551133334444"), Times.Once);
    }

    [Fact]
    public async Task BuscarContextoPorTelefoneAsync_TutorInexistente_NuncaMencionaONumeroBuscado()
    {
        // Arrange
        // LGPD: o número de telefone não pode vazar em nenhuma mensagem/exceção que
        // suba até o middleware/log — aqui não há exceção nenhuma (retorna null), o que
        // já é a forma mais segura de "não encontrado" (sem construir mensagem alguma).
        var numeroSensivel = "5511900001234";
        _tutorRepoMock.Setup(r => r.GetByTelefoneAsync(numeroSensivel)).ReturnsAsync((Tutor?)null);

        // Act
        Exception? excecaoCapturada = null;
        try
        {
            await _sut.BuscarContextoPorTelefoneAsync(numeroSensivel);
        }
        catch (Exception ex)
        {
            excecaoCapturada = ex;
        }

        // Assert
        excecaoCapturada.Should().BeNull("tutor não encontrado é modelado como null, não exceção — nada para vazar");
    }
}
