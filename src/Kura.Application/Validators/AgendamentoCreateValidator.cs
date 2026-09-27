namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Agenda;

/// <summary>
/// REC-10 — validação de forma do <c>POST /api/v1/agendamentos</c>. Regras relacionais
/// (existência/clínica/vínculo de tutor, pet, veterinário e triagem de origem; tolerância de
/// encaixe) moram em <c>AgendaService.CriarAsync</c>, não aqui — elas dependem de estado
/// (banco, relógio da clínica), e este validator não tem acesso a nenhum dos dois, mesmo
/// padrão de <c>AtualizarStatusAgendamentoValidator</c> (a máquina de transição mora no
/// service, não no validator).
/// </summary>
public sealed class AgendamentoCreateValidator : AbstractValidator<AgendamentoCreateDto>
{
    /// <summary>
    /// G0 item 9 — no Java, esta lista só existe em <c>@Schema(allowableValues=...)</c>
    /// (documentação do Swagger, NÃO validação: <c>AgendamentoRequest.java</c> só tem
    /// <c>@NotBlank</c> em <c>tipo</c>). O <c>.NET</c> é mais estrito de propósito: valida a
    /// lista de verdade. Base medida no G0 (compose, 2026-09-26): <c>CONSULTA 5</c>,
    /// <c>TELEORIENTACAO 5</c> — os 2 valores em uso hoje estão nesta lista.
    /// </summary>
    public static readonly IReadOnlySet<string> TiposPermitidos =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "CONSULTA",
            "RETORNO",
            "VACINA",
            "EXAME",
            "PROCEDIMENTO",
            "TELEORIENTACAO",
        };

    public AgendamentoCreateValidator()
    {
        RuleFor(x => x.IdTutor)
            .GreaterThan(0);

        RuleFor(x => x.IdPet)
            .GreaterThan(0);

        RuleFor(x => x.IdVeterinario)
            .GreaterThan(0);

        RuleFor(x => x.DtAgendamento)
            .NotEmpty();

        RuleFor(x => x.DsTipo)
            .Must(TiposPermitidos.Contains)
            .WithMessage(
                "'DsTipo' deve ser um de: "
                + string.Join(", ", TiposPermitidos.OrderBy(s => s, StringComparer.Ordinal))
                + ".");

        // AgendamentoRequest.java: @Min(5) @Max(480), default 30 quando ausente. Nullable
        // aqui pelo mesmo motivo: "ausente" != "zero", e o default é aplicado no service,
        // não neste validator (mesmo padrão de DsVinculo em PetCreateValidator/PetService).
        RuleFor(x => x.Duracao!.Value)
            .InclusiveBetween(5, 480)
            .When(x => x.Duracao.HasValue)
            .WithMessage("'Duracao' deve estar entre 5 e 480 minutos.");

        // AGENDAMENTO.DS_OBSERVACOES é VARCHAR2(1000) (V5, backend-tutor-java) — mesmo
        // raciocínio de tamanho de ConsultaCreateValidator.DsObservacao.
        RuleFor(x => x.DsObservacoes)
            .MaximumLength(1000);
    }
}
