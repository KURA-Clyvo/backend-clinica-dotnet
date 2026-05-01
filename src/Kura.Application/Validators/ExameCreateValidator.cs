namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Exame;

public sealed class ExameCreateValidator : AbstractValidator<ExameCreateDto>
{
    public ExameCreateValidator()
    {
        RuleFor(x => x.IdPet)
            .GreaterThan(0);

        RuleFor(x => x.IdVeterinario)
            .GreaterThan(0);

        RuleFor(x => x.NmExame)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DsResultado)
            .NotEmpty()
            .MaximumLength(1000);

        RuleFor(x => x.DtRealizacao)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("'DtRealizacao' não pode ser uma data futura.");
    }
}
