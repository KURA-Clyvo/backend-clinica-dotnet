namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Pet;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using System.Linq.Expressions;

public class PetServiceTests
{
    private readonly Mock<IPetRepository> _petRepoMock = new();
    private readonly Mock<ITutorPetRepository> _tutorPetRepoMock = new();
    private readonly Mock<IRepository<Especie>> _especieRepoMock = new();
    private readonly Mock<IRepository<Raca>> _racaRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IClinicaContext> _clinicaMock = new();
    private readonly Mock<ITutorRepository> _tutorRepoMock = new();
    private readonly Mock<IGeradorUrlFotoPet> _geradorUrlFotoPetMock = new();
    private readonly PetService _sut;

    public PetServiceTests()
    {
        _clinicaMock.Setup(c => c.IdClinica).Returns(1L);
        _sut = new PetService(
            _petRepoMock.Object, _tutorPetRepoMock.Object,
            _especieRepoMock.Object, _racaRepoMock.Object,
            _uowMock.Object, _clinicaMock.Object, _tutorRepoMock.Object,
            _geradorUrlFotoPetMock.Object);
    }

    private PetCreateDto ValidCreateDto(string? dsVinculo = null) => new()
    {
        IdTutor = 20L, IdEspecie = 1L, IdRaca = 1L,
        IdVeterinarioResp = 5L, NmPet = "Rex",
        DtNascimento = new DateTime(2022, 1, 1),
        SgSexo = 'M', SgPorte = 'M', StPrincipal = true,
        DsVinculo = dsVinculo ?? "PROPRIETARIO"
    };

    private void SetupTutor(long id) =>
        _tutorRepoMock.Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync(new Tutor { Id = id, NmTutor = "João" });

    private void SetupPet(long id) =>
        _petRepoMock.Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync(new Pet { Id = id, NmPet = "Rex", IdClinica = 1 });

    [Fact]
    public async Task CreateAsync_TutorValido_CriaPetETutorPet()
    {
        // Arrange
        SetupTutor(20L);
        _tutorPetRepoMock.Setup(r => r.AddAsync(It.IsAny<TutorPet>())).Returns(Task.CompletedTask);

        // Act
        await _sut.CreateAsync(ValidCreateDto());

        // Assert
        _tutorPetRepoMock.Verify(r => r.AddAsync(It.IsAny<TutorPet>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_TutorInexistente_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        _tutorRepoMock.Setup(r => r.GetByIdAsync(99L)).ReturnsAsync((Tutor?)null);
        var dto = new PetCreateDto
        {
            IdTutor = 99L, IdEspecie = 1L, IdRaca = 1L,
            IdVeterinarioResp = 5L, NmPet = "Rex",
            DtNascimento = new DateTime(2022, 1, 1),
            SgSexo = 'M', SgPorte = 'M', StPrincipal = true
        };

        // Act
        var act = async () => await _sut.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task AdicionarTutorAsync_SegundoTutor_CriaTutorPetStPrincipalN()
    {
        // Arrange
        SetupPet(1L);
        _tutorPetRepoMock.Setup(r => r.ExistsAsync(30L, 1L)).ReturnsAsync(false);
        TutorPet? criado = null;
        _tutorPetRepoMock.Setup(r => r.AddAsync(It.IsAny<TutorPet>()))
            .Callback<TutorPet>(tp => criado = tp)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.AdicionarTutorAsync(1L, new AdicionarTutorPetDto { IdTutor = 30L });

        // Assert
        criado!.StPrincipal.Should().Be(false);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task AdicionarTutorAsync_TutorJaVinculado_LancaRegraDeNegocio()
    {
        // Arrange
        SetupPet(1L);
        _tutorPetRepoMock.Setup(r => r.ExistsAsync(20L, 1L)).ReturnsAsync(true);

        // Act
        var act = async () => await _sut.AdicionarTutorAsync(1L, new AdicionarTutorPetDto { IdTutor = 20L });

        // Assert
        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().Be("Tutor já vinculado a este pet.");
    }

    [Fact]
    public async Task SoftDeleteAsync_PetExiste_ChamaSoftDelete()
    {
        // Arrange
        SetupPet(1L);

        // Act
        await _sut.SoftDeleteAsync(1L);

        // Assert
        _petRepoMock.Verify(r => r.SoftDelete(It.IsAny<Pet>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_DsVinculoVazioOuWhitespace_ColescaParaProprietario(string dsVinculoBruto)
    {
        // Arrange
        // TASK-60: TUTOR_PET.DS_VINCULO é NOT NULL (V1:156, migration imutável) e o Oracle trata
        // VARCHAR2 vazio como NULL. PetCreateDto.DsVinculo já tem default "PROPRIETARIO" (não
        // string.Empty) — mas um cliente que manda dsVinculo:"" explicitamente ainda estoura
        // ORA-01400 (500) sem este coalesce.
        SetupTutor(20L);
        TutorPet? criado = null;
        _tutorPetRepoMock.Setup(r => r.AddAsync(It.IsAny<TutorPet>()))
            .Callback<TutorPet>(tp => criado = tp)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CreateAsync(ValidCreateDto(dsVinculo: dsVinculoBruto));

        // Assert
        criado.Should().NotBeNull();
        criado!.DsVinculo.Should().Be("PROPRIETARIO");
    }

    [Fact]
    public async Task CreateAsync_DsVinculoPreenchido_NaoSobrescreveComDefault()
    {
        // Arrange
        SetupTutor(20L);
        TutorPet? criado = null;
        _tutorPetRepoMock.Setup(r => r.AddAsync(It.IsAny<TutorPet>()))
            .Callback<TutorPet>(tp => criado = tp)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CreateAsync(ValidCreateDto(dsVinculo: "RESPONSAVEL_FINANCEIRO"));

        // Assert
        criado.Should().NotBeNull();
        criado!.DsVinculo.Should().Be("RESPONSAVEL_FINANCEIRO");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AdicionarTutorAsync_DsVinculoVazioOuWhitespace_ColescaParaCuidador(string dsVinculoBruto)
    {
        // Arrange
        // Mesmo gap de PetCreateDto.DsVinculo — AdicionarTutorPetDto.DsVinculo tem default
        // "CUIDADOR", mas dsVinculo:"" explícito também estoura ORA-01400.
        SetupPet(1L);
        _tutorPetRepoMock.Setup(r => r.ExistsAsync(30L, 1L)).ReturnsAsync(false);
        TutorPet? criado = null;
        _tutorPetRepoMock.Setup(r => r.AddAsync(It.IsAny<TutorPet>()))
            .Callback<TutorPet>(tp => criado = tp)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.AdicionarTutorAsync(1L, new AdicionarTutorPetDto { IdTutor = 30L, DsVinculo = dsVinculoBruto });

        // Assert
        criado.Should().NotBeNull();
        criado!.DsVinculo.Should().Be("CUIDADOR");
    }
}
