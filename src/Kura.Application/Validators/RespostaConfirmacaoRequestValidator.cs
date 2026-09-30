namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Luna;

/// <summary>
/// REC-15 — valida o shape do payload de POST .../resposta-confirmacao (400, via
/// AddFluentValidationAutoValidation) ANTES de chegar ao service, que trata o valor
/// de negócio (tutor errado / status não elegível) com RegraDeNegocioException (422).
/// Mesmo padrão de TriageRequestValidator: resposta não tem CHECK textual estrito no
/// Oracle (DS_RESPOSTA_CONFIRMACAO é VARCHAR2(20) com CHECK dos 3 valores na V23 —
/// aqui é defesa antecipada, contrato com o Pydantic da Luna).
/// </summary>
public sealed class RespostaConfirmacaoRequestValidator : AbstractValidator<RespostaConfirmacaoRequestDto>
{
    public RespostaConfirmacaoRequestValidator()
    {
        RuleFor(x => x.IdTutor)
            .GreaterThan(0)
            .WithMessage("'id_tutor' é obrigatório.");

        RuleFor(x => x.Resposta)
            .Must(r => r is "SIM" or "CANCELAR" or "REMARCAR")
            .WithMessage("'resposta' deve ser SIM, CANCELAR ou REMARCAR.");
    }
}
