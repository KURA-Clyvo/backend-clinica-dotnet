namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Pet;

public sealed class PetCreateValidator : AbstractValidator<PetCreateDto>
{
    public PetCreateValidator()
    {
        RuleFor(x => x.IdEspecie)
            .GreaterThan(0);

        RuleFor(x => x.IdRaca)
            .GreaterThan(0);

        RuleFor(x => x.NmPet)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DtNascimento)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("'DtNascimento' não pode ser uma data futura.");

        RuleFor(x => x.SgSexo)
            .Must(s => s == 'M' || s == 'F')
            .WithMessage("'SgSexo' deve ser 'M' ou 'F'.");

        RuleFor(x => x.SgPorte)
            .Must(p => p == 'P' || p == 'M' || p == 'G')
            .WithMessage("'SgPorte' deve ser 'P', 'M' ou 'G'.");
    }
}
