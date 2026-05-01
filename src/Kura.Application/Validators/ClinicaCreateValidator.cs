namespace Kura.Application.Validators;

using FluentValidation;
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
            .Matches(@"^\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}$")
            .WithMessage("CNPJ deve estar no formato 00.000.000/0000-00");

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
