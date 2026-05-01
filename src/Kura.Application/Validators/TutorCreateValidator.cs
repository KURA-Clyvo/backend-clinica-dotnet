namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Tutor;

public sealed class TutorCreateValidator : AbstractValidator<TutorCreateDto>
{
    public TutorCreateValidator()
    {
        RuleFor(x => x.NmTutor)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.NrCpf)
            .NotEmpty()
            .Length(11)
            .Matches("^[0-9]{11}$").WithMessage("'NrCpf' deve conter exatamente 11 dígitos numéricos.");

        RuleFor(x => x.DsEmail)
            .NotEmpty()
            .MaximumLength(150);
    }
}
