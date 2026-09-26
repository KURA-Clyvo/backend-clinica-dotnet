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
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IClinicaContext> _clinicaContextMock = new();
    private readonly Mock<IGeradorUrlFotoPet> _geradorUrlFotoPetMock = new();
    private readonly Mock<IGeradorLinkConvite> _geradorLinkConviteMock = new();
    private readonly TutorService _sut;

    public TutorServiceTests()
    {
        _tutorRepoMock.Setup(r => r.AddAsync(It.IsAny<Tutor>())).Returns(Task.CompletedTask);
        _inviteRepoMock.Setup(r => r.AddAsync(It.IsAny<InviteTutor>())).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);
        _clinicaContextMock.Setup(c => c.IdClinica).Returns(1L);

        _sut = new TutorService(
            _tutorRepoMock.Object,
            _tutorPetRepoMock.Object,
            _especieRepoMock.Object,
            _racaRepoMock.Object,
            _inviteRepoMock.Object,
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

    // REC-01 (G0 item 4): recomendação do maestro — o PUT continua NÃO exigindo telefone (não
    // quebrar edição parcial). TASK-60 (sentinela quando vazio) continua valendo sem mudança.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_NrTelefoneVazioOuWhitespace_ColescaParaSentinela(string nrTelefoneBruto)
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

        // Assert
        tutorExistente.NrTelefone.Should().Be("Não informado");
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
    public async Task UpdateAsync_TelefoneNaoMudaEraMesmoNumero_DsWhatsappPermaneceLigadoAoAntigo()
    {
        // Guarda contra "+Não informado": se o telefone novo vier vazio (sentinela), o ramo 2
        // não deve tentar acompanhar um valor não-normalizável.
        var tutorExistente = new Tutor
        {
            Id = 45L, NmTutor = "Maria", NrTelefone = "5511900000000", DsWhatsapp = "+5511900000000"
        };
        _tutorRepoMock.Setup(r => r.GetByIdAsync(45L)).ReturnsAsync(tutorExistente);

        var dto = new TutorUpdateDto
        {
            NmTutor = "Maria", NrCpf = "12345678901", DsEmail = "maria@email.com",
            NrTelefone = "" // sentinela
        };

        await _sut.UpdateAsync(45L, dto);

        tutorExistente.NrTelefone.Should().Be("Não informado");
        tutorExistente.DsWhatsapp.Should().Be("+5511900000000", "telefone não mudou de verdade (voltou a vazio) — DsWhatsapp não deve virar '+Não informado'");
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
        _tutorRepoMock.Setup(r => r.GetByTelefoneAsync("11999990000")).ReturnsAsync((Tutor?)null);
        _tutorRepoMock.Setup(r => r.GetByTelefoneAsync("5511999990000")).ReturnsAsync(tutor);
        _tutorPetRepoMock.Setup(r => r.GetByTutorIdAsync(tutor.Id)).ReturnsAsync(new List<TutorPet>());

        // Act
        var result = await _sut.BuscarContextoPorTelefoneAsync("11999990000");

        // Assert
        result.Should().NotBeNull();
        _tutorRepoMock.Verify(r => r.GetByTelefoneAsync("11999990000"), Times.Once);
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
        _tutorRepoMock.Setup(r => r.GetByTelefoneAsync("14155550100")).ReturnsAsync(tutor);
        _tutorPetRepoMock.Setup(r => r.GetByTutorIdAsync(tutor.Id)).ReturnsAsync(new List<TutorPet>());

        // Act
        var result = await _sut.BuscarContextoPorTelefoneAsync("14155550100");

        // Assert
        result.Should().NotBeNull();
        result!.IdTutor.Should().Be(10);
        _tutorRepoMock.Verify(r => r.GetByTelefoneAsync("14155550100"), Times.Once);
        _tutorRepoMock.Verify(r => r.GetByTelefoneAsync("5514155550100"), Times.Never);
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
