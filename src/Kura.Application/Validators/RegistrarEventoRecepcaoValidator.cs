namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Agenda;

/// <summary>
/// REC-11 — validação de forma do corpo de <c>POST .../checkin</c> e
/// <c>POST .../inicio-atendimento</c>. Mesmo padrão de <c>AtualizarStatusAgendamentoValidator</c>:
/// só a forma do lock otimista mora aqui; a elegibilidade por status e a idempotência moram em
/// <c>AgendaService</c>.
/// </summary>
public sealed class RegistrarEventoRecepcaoValidator : AbstractValidator<RegistrarEventoRecepcaoDto>
{
    public RegistrarEventoRecepcaoValidator()
    {
        RuleFor(x => x.NrVersion)
            .GreaterThanOrEqualTo(0)
            .WithMessage("'NrVersion' deve ser >= 0.");
    }
}
