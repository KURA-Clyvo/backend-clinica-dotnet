namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Clinica;

public sealed class ClinicaUpdateValidator : AbstractValidator<ClinicaUpdateDto>
{
    public ClinicaUpdateValidator()
    {
        RuleFor(x => x.NmClinica)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.NrTelefone)
            .MaximumLength(20);

        RuleFor(x => x.DsEmail)
            .MaximumLength(150);
    }
}
