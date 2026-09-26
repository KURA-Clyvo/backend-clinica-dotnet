namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Tutor;
using Kura.Domain.Tutores;

public sealed class TutorCreateValidator : AbstractValidator<TutorCreateDto>
{
    private const string MensagemTelefoneInvalido =
        "'NrTelefone' inválido — informe DDD + número (10 ou 11 dígitos), telefone já com DDI " +
        "do Brasil (55 + DDD + número) ou telefone estrangeiro com '+' explícito.";

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

        // REC-01 (KURA_BACKLOG_RECEPCAO.md, A-12): telefone passou a ser OBRIGATÓRIO nesta
        // rota — reverte de propósito o coalesce da TASK-60 (sentinela "Não informado"), que
        // deixa de ser produzido por POST /api/v1/tutores (G0 item 6, consumidor 8). Também
        // valida o FORMATO via NormalizadorTelefone — o mesmo helper que decide o valor
        // persistido em TutorService, para nenhum telefone "quase válido" passar o 400 e
        // ainda assim não casar com a busca da Luna.
        // ApplyConditionTo.CurrentValidator é OBRIGATÓRIO aqui: por padrão, .When() no fim de
        // uma cadeia fluente se aplica a TODOS os validators anteriores da MESMA RuleFor (não
        // só o último) — sem o escopo explícito, telefone vazio faria o .When() desativar
        // também o .NotEmpty() (a checagem que precisamos que rode justamente quando vazio),
        // deixando "" passar como válido. Achado ao rodar a suíte, não previsto no design.
        RuleFor(x => x.NrTelefone)
            .NotEmpty().WithMessage("'NrTelefone' é obrigatório.")
            .MaximumLength(20)
            .Must(t => NormalizadorTelefone.TentarNormalizar(t, out _))
                .WithMessage(MensagemTelefoneInvalido)
                .When(x => !string.IsNullOrWhiteSpace(x.NrTelefone), ApplyConditionTo.CurrentValidator);

        // REC-01: DsWhatsapp é opcional ("mesmo número" quando ausente — G0 item 4), mas
        // quando informado precisa se encaixar na mesma regra de formato.
        RuleFor(x => x.DsWhatsapp)
            .Must(w => NormalizadorTelefone.TentarNormalizar(w, out _))
                .WithMessage("'DsWhatsapp' inválido — mesma regra de 'NrTelefone'.")
                .When(x => !string.IsNullOrWhiteSpace(x.DsWhatsapp), ApplyConditionTo.CurrentValidator);

        // REC-01 (A-9): aviso de privacidade passa a depender do que a recepção de fato
        // informou ao tutor — TutorService.CreateAsync gravava StAvisoPrivacidade="S"
        // incondicionalmente antes desta task.
        RuleFor(x => x.StAvisoPrivacidadeInformado)
            .Equal(true)
            .WithMessage(
                "É necessário confirmar que o aviso de privacidade foi informado ao tutor " +
                "antes de cadastrá-lo.");

        RuleFor(x => x.DsCanalConvite)
            .Must(c => c is "WHATSAPP" or "EMAIL" or "SMS")
            .WithMessage("'DsCanalConvite' deve ser WHATSAPP, EMAIL ou SMS.");
    }
}
