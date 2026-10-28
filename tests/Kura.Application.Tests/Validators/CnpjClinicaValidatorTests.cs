namespace Kura.Application.Tests.Validators;

using FluentAssertions;
using Kura.Application.DTOs.Auth;
using Kura.Application.DTOs.Clinica;
using Kura.Application.Validators;

/// <summary>S4-FIX-02 (E67): CNPJ aceito com ou sem máscara nos dois validators.</summary>
public class CnpjClinicaValidatorTests
{
    private static RegisterClinicaDto Register(string cnpj) => new()
    {
        NmClinica = "Clínica Teste", NrCnpj = cnpj, DsEndereco = "Rua A, 1", NmCidade = "São Paulo",
        SgUf = "SP", NrCep = "01000-000", DsEmail = "c@teste.com", DsEmailAcesso = "a@teste.com",
        DsSenha = "Senha@2026", NmVeterinarioAdmin = "Dr. X", NrCRMV = "SP-1"
    };

    private static ClinicaCreateDto Create(string cnpj) => new()
    {
        NmClinica = "Clínica Teste", NrCnpj = cnpj, DsEndereco = "Rua A, 1", DsEmail = "c@teste.com"
    };

    [Theory]
    [InlineData("58.001.200/0001-49")]
    [InlineData("58001200000149")]
    public void Register_CnpjValido_ComOuSemMascara_Passa(string cnpj) =>
        new RegisterClinicaValidator().Validate(Register(cnpj)).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("5800120000014")]
    [InlineData("58.001.200/0001-4A")]
    public void Register_CnpjInvalido_Falha(string cnpj) =>
        new RegisterClinicaValidator().Validate(Register(cnpj)).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("58.001.200/0001-49")]
    [InlineData("58001200000149")]
    public void ClinicaCreate_CnpjValido_ComOuSemMascara_Passa(string cnpj) =>
        new ClinicaCreateValidator().Validate(Create(cnpj)).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("5800120000014")]
    [InlineData("58.001.200/0001-4A")]
    public void ClinicaCreate_CnpjInvalido_Falha(string cnpj) =>
        new ClinicaCreateValidator().Validate(Create(cnpj)).IsValid.Should().BeFalse();
}
