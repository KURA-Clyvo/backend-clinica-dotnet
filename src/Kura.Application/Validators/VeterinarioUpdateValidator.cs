namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Veterinario;

public sealed class VeterinarioUpdateValidator : AbstractValidator<VeterinarioUpdateDto>
{
    public VeterinarioUpdateValidator()
    {
        RuleFor(x => x.NmVeterinario)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.NrCrmv)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(x => x.DsEmail)
            .NotEmpty()
            .MaximumLength(150);
    }
}
