namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Especie;

public sealed class EspecieCreateValidator : AbstractValidator<EspecieCreateDto>
{
    public EspecieCreateValidator()
    {
        RuleFor(x => x.NmEspecie)
            .NotEmpty()
            .MaximumLength(200);
    }
}
