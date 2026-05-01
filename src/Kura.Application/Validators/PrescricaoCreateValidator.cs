namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Prescricao;

public sealed class PrescricaoCreateValidator : AbstractValidator<PrescricaoCreateDto>
{
    public PrescricaoCreateValidator()
    {
        RuleFor(x => x.IdPet)
            .GreaterThan(0);

        RuleFor(x => x.IdVeterinario)
            .GreaterThan(0);

        RuleFor(x => x.IdMedicamento)
            .GreaterThan(0);

        RuleFor(x => x.DsPosologia)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.NrDuracaoDias)
            .InclusiveBetween(1, 365);
    }
}
