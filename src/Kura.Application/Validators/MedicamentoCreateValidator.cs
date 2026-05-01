namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Medicamento;

public sealed class MedicamentoCreateValidator : AbstractValidator<MedicamentoCreateDto>
{
    public MedicamentoCreateValidator()
    {
        RuleFor(x => x.NmMedicamento)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DsPrincipioAtivo)
            .NotEmpty()
            .MaximumLength(200);
    }
}
