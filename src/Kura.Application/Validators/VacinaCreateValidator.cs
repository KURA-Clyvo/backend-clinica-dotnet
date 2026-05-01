namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Vacina;

public sealed class VacinaCreateValidator : AbstractValidator<VacinaCreateDto>
{
    public VacinaCreateValidator()
    {
        RuleFor(x => x.IdPet)
            .GreaterThan(0);

        RuleFor(x => x.IdVeterinario)
            .GreaterThan(0);

        RuleFor(x => x.DtEvento)
            .NotEmpty();

        RuleFor(x => x.NmVacina)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.NrLote)
            .NotEmpty()
            .MaximumLength(50);
    }
}
