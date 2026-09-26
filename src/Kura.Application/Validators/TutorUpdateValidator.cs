namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Tutor;
using Kura.Domain.Tutores;

public sealed class TutorUpdateValidator : AbstractValidator<TutorUpdateDto>
{
    public TutorUpdateValidator()
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

        // REC-01 (KURA_BACKLOG_RECEPCAO.md, G0 item 4): recomendação do maestro — o PUT NÃO
        // passa a exigir telefone (não quebrar edição parcial de tutor antigo sem telefone),
        // mas quando o campo VEM preenchido, precisa se encaixar na mesma regra de formato do
        // POST (mesmo helper — NormalizadorTelefone), senão o tutor editado continuaria sem
        // casar com a busca da Luna. TASK-60 (coalesce para o sentinela "Não informado" em
        // TutorService.UpdateAsync quando vazio) continua valendo — sem mudança aqui.
        // ApplyConditionTo.CurrentValidator: mesmo achado do TutorCreateValidator — sem o
        // escopo explícito, .When() desativaria também .MaximumLength(20) quando vazio (aqui
        // inofensivo, "" sempre passa MaximumLength; mantido por correção/consistência).
        RuleFor(x => x.NrTelefone)
            .MaximumLength(20)
            .Must(t => NormalizadorTelefone.TentarNormalizar(t, out _))
                .WithMessage(
                    "'NrTelefone' inválido — informe DDD + número (10 ou 11 dígitos), telefone " +
                    "já com DDI do Brasil (55 + DDD + número) ou telefone estrangeiro com '+' " +
                    "explícito.")
                .When(x => !string.IsNullOrWhiteSpace(x.NrTelefone), ApplyConditionTo.CurrentValidator);

        // G2 fix wave (achado Important #1): DsWhatsapp é opcional no PUT (ver TutorUpdateDto),
        // mas quando informado precisa se encaixar na mesma regra de formato.
        RuleFor(x => x.DsWhatsapp)
            .Must(w => NormalizadorTelefone.TentarNormalizar(w, out _))
                .WithMessage("'DsWhatsapp' inválido — mesma regra de 'NrTelefone'.")
                .When(x => !string.IsNullOrWhiteSpace(x.DsWhatsapp), ApplyConditionTo.CurrentValidator);
    }
}
