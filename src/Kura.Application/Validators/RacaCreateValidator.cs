namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Raca;

public sealed class RacaCreateValidator : AbstractValidator<RacaCreateDto>
{
    public RacaCreateValidator()
    {
        RuleFor(x => x.IdEspecie)
            .GreaterThan(0);

        RuleFor(x => x.NmRaca)
            .NotEmpty()
            .MaximumLength(200);
    }
}
