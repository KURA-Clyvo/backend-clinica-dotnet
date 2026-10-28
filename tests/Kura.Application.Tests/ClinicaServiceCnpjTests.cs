namespace Kura.Application.Tests;

using FluentAssertions;
using Kura.Application.DTOs.Clinica;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Moq;

/// <summary>S4-FIX-02 (E67): ClinicaService.CreateAsync grava a forma canônica mascarada.</summary>
public class ClinicaServiceCnpjTests
{
    private readonly Mock<IClinicaRepository> _repo = new();
    private readonly Mock<IVeterinarioRepository> _vet = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    [Theory]
    [InlineData("58001200000149")]
    [InlineData("58.001.200/0001-49")]
    public async Task CreateAsync_GravaCnpjCanonicoMascarado(string digitado)
    {
        // Arrange
        Clinica? salvo = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<Clinica>())).Callback<Clinica>(c => salvo = c).Returns(Task.CompletedTask);
        var sut = new ClinicaService(_repo.Object, _vet.Object, _uow.Object);

        // Act
        await sut.CreateAsync(new ClinicaCreateDto { NmClinica = "X", NrCnpj = digitado, DsEndereco = "R", DsEmail = "x@x.com" });

        // Assert
        salvo!.NrCnpj.Should().Be("58.001.200/0001-49");
    }

    [Fact]
    public async Task CreateAsync_CnpjJaCadastrado_LancaRegraDeNegocio()
    {
        // Arrange
        _repo.Setup(r => r.ExisteComCnpjAsync("58.001.200/0001-49")).ReturnsAsync(true);
        var sut = new ClinicaService(_repo.Object, _vet.Object, _uow.Object);

        // Act
        var act = () => sut.CreateAsync(new ClinicaCreateDto { NmClinica = "X", NrCnpj = "58001200000149", DsEndereco = "R", DsEmail = "x@x.com" });

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>();
    }
}
