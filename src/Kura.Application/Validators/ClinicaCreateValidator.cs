namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Domain.Clinicas;
using Kura.Application.DTOs.Clinica;

public sealed class ClinicaCreateValidator : AbstractValidator<ClinicaCreateDto>
{
    public ClinicaCreateValidator()
    {
        RuleFor(x => x.NmClinica)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.NrCnpj)
            .NotEmpty()
            .Must(c => NormalizadorCnpj.TentarNormalizar(c, out _))
            .WithMessage("CNPJ inválido. Informe 14 dígitos, com ou sem máscara (00.000.000/0000-00).");

        RuleFor(x => x.DsEndereco)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.NrTelefone)
            .MaximumLength(20);

        RuleFor(x => x.DsEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(150);
    }
}
