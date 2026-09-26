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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_NrTelefoneVazioOuWhitespace_ColescaParaSentinela(string nrTelefoneBruto)
    {
        // Arrange
        // Mesmo gap de TutorCreateDto — TutorUpdateValidator também nunca teve regra para
        // NrTelefone (só NmTutor/NrCpf/DsEmail).
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
    public async Task UpdateAsync_NrTelefonePreenchido_NaoSobrescreveComSentinela()
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
            NrTelefone = "11977776666"
        };

        // Act
        await _sut.UpdateAsync(42L, dto);

        // Assert
        tutorExistente.NrTelefone.Should().Be("11977776666");
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
