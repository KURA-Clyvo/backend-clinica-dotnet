namespace Kura.Application.Validators;

using System.Text;
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

    /// <summary>
    /// G2 (I-1) — <c>DS_OBSERVACOES VARCHAR2(1000)</c> não tem <c>CHAR</c> na DDL (V5,
    /// <c>backend-tutor-java</c>) ⇒ semântica BYTE por default do Oracle (mesmo raciocínio
    /// já usado em <c>TriageRequestValidator.DsRegrasVersao</c>/<c>LunaService.
    /// TruncarPorBytesUtf8</c> — FIX_6 provou truncamento a exatamente 4000 bytes contra
    /// Oracle real na mesma classe de coluna). <c>MaximumLength(1000)</c> conta
    /// CARACTERES: um texto de ≤1000 caracteres acentuados (2 bytes cada em AL32UTF8)
    /// passava o validator antigo e estourava <c>ORA-12899</c> (500) no Oracle real —
    /// InMemory nunca reproduz (classe FIX_4).
    /// </summary>
    public const int MaxObservacoesBytes = 1000;

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

        // G2 (I-1) — medido por HTTP contra o KuraApiFactory real: um DtAgendamento com
        // "Z" (Kind=Utc) ou offset explícito (Kind=Local) era ACEITO com 201 e gravado
        // DESLOCADO -- "…T10:00:00Z" virava 10:00 tratado como hora de SP (o instante
        // pedido era 07:00 SP, +3h de erro), e a resposta ecoava a data COM "Z", quebrando
        // o contrato "hora local de SP, sem Z" da REC-09/A-5. O cliente mais provável
        // (React Native, Date.toISOString()) SEMPRE emite "Z" — grava tudo deslocado sem
        // erro nenhum, e o encaixe (REC-10) compara o valor já deslocado. Fix: recusar
        // qualquer Kind diferente de Unspecified com 400 explícito, em vez de tentar
        // adivinhar/converter -- o cliente tem que mandar a hora local nua, mesma
        // convenção que A-5 já define para toda a tabela AGENDAMENTO.
        RuleFor(x => x.DtAgendamento)
            .Must(dt => dt.Kind == DateTimeKind.Unspecified)
            .WithMessage(
                "'DtAgendamento' deve ser enviado como hora local de São Paulo, sem fuso " +
                "(sem 'Z' e sem offset, ex.: '2026-10-07T09:00:00') — 'Z'/offset indicam " +
                "que o cliente está mandando UTC ou outro fuso, o que grava a hora errada.");

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

        // G2 (I-2) — AGENDAMENTO.DS_OBSERVACOES é VARCHAR2(1000) SEM "CHAR" na DDL (V5),
        // então a coluna é BYTE, não caractere. MaximumLength(1000) media caracteres;
        // trocado por contagem de bytes UTF-8, mesmo padrão de
        // TriageRequestValidator.DsRegrasVersao.
        RuleFor(x => x.DsObservacoes)
            .Must(obs => Encoding.UTF8.GetByteCount(obs!) <= MaxObservacoesBytes)
            .WithMessage($"'DsObservacoes' deve ter no máximo {MaxObservacoesBytes} bytes UTF-8.")
            .When(x => x.DsObservacoes is not null);
    }
}
