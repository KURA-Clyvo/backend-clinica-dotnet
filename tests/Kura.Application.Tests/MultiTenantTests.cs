namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Pet;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;

public class MultiTenantTests
{
    [Fact]
    public async Task PetService_CreateAsync_SetaIdClinicaDoContexto()
    {
        // Arrange
        var clinicaId = 42L;
        var repoMock = new Mock<IPetRepository>();
        var tutorPetMock = new Mock<ITutorPetRepository>();
        var especieMock = new Mock<IRepository<Especie>>();
        var racaMock = new Mock<IRepository<Raca>>();
        var uowMock = new Mock<IUnitOfWork>();
        var ctxMock = new Mock<IClinicaContext>();
        var tutorRepoMock = new Mock<ITutorRepository>();

        ctxMock.Setup(c => c.IdClinica).Returns(clinicaId);
        tutorRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<long>()))
            .ReturnsAsync(new Tutor { Id = 1, NmTutor = "João" });
        especieMock.Setup(r => r.GetByIdAsync(It.IsAny<long>()))
            .ReturnsAsync(new Especie { Id = 1, NmEspecie = "Cão" });
        racaMock.Setup(r => r.GetByIdAsync(It.IsAny<long>()))
            .ReturnsAsync(new Raca { Id = 1, NmRaca = "Labrador" });
        tutorPetMock.Setup(r => r.GetByPetIdAsync(It.IsAny<long>()))
            .ReturnsAsync(new List<TutorPet>());

        Pet? petCriado = null;
        tutorPetMock.Setup(r => r.AddAsync(It.IsAny<TutorPet>()))
            .Callback<TutorPet>(tp => petCriado = tp.Pet)
            .Returns(Task.CompletedTask);

        var sut = new PetService(repoMock.Object, tutorPetMock.Object,
            especieMock.Object, racaMock.Object, uowMock.Object, ctxMock.Object, tutorRepoMock.Object,
            Mock.Of<IGeradorUrlFotoPet>());

        // Act
        await sut.CreateAsync(new PetCreateDto
        {
            IdEspecie = 1,
            IdRaca = 1,
            IdTutor = 1,
            NmPet = "Rex",
            DtNascimento = DateTime.Today.AddYears(-2),
            SgSexo = 'M',
            SgPorte = 'G',
            DsVinculo = "Dono",
            StPrincipal = true
        });

        // Assert
        petCriado.Should().NotBeNull();
        petCriado!.IdClinica.Should().Be(clinicaId);
    }
}
