namespace Kura.Application.Validators;

using System.Text;
using FluentValidation;
using Kura.Application.DTOs.Luna;

/// <summary>
/// TASK-67: ds_urgencia não tem CHECK constraint no Oracle (DS_NIVEL_URGENCIA é
/// VARCHAR2(20) livre, V9__schema_drift_clinico.sql) — validado aqui mesmo assim,
/// como contrato defensivo com o Pydantic (Literal["BAIXA","MEDIA","ALTA"]), para
/// pegar um client desalinhado com 400 em vez de gravar lixo silenciosamente.
/// </summary>
public sealed class TriageRequestValidator : AbstractValidator<TriageRequestDto>
{
    // Fix wave 1 (IMPORTANTE-2, lu-08-revisao.md frentes 3/4): tamanhos lidos da V21
    // real (backend-tutor-java `main` @ 3fb8ea3,
    // db/migration-oracle/V21__vacinas_v2_notificacao_triagem_luna.sql — grupo 3):
    // "ALTER TABLE TRIAGEM_LUNA ADD DS_REGRAS_VERSAO VARCHAR2(10)" (sem "CHAR", então
    // BYTE — NLS_LENGTH_SEMANTICS default do Oracle) e
    // "ALTER TABLE TRIAGEM_LUNA ADD NR_SCORE NUMBER(5)". Confirmado contra o Oracle do
    // compose por USER_TAB_COLUMNS (G2): DS_REGRAS_VERSAO VARCHAR2 10 CHAR_USED=B.
    private const int MaxRegrasVersaoBytes = 10;
    private const int MaxNrScore = 99999; // NUMBER(5), sem escala — maior inteiro que cabe

    public TriageRequestValidator()
    {
        RuleFor(x => x.IdInteracao)
            .GreaterThan(0)
            .WithMessage("'id_interacao' é obrigatório.");

        RuleFor(x => x.IdTutor)
            .GreaterThan(0)
            .WithMessage("'id_tutor' é obrigatório.");

        RuleFor(x => x.DsUrgencia)
            .Must(u => u is "BAIXA" or "MEDIA" or "ALTA")
            .WithMessage("'ds_urgencia' deve ser BAIXA, MEDIA ou ALTA.");

        RuleFor(x => x.DsRecomendacao)
            .NotEmpty()
            .WithMessage("'ds_recomendacao' não pode ser vazio.");

        // LU-08: opcional e retrocompatível (payload sem o campo continua 201) — só
        // valida tamanho quando presente, nunca presença.
        //
        // Fix wave 1 (IMPORTANTE-2): a regra anterior era MaximumLength(10), que conta
        // CARACTERES — a coluna é VARCHAR2(10 BYTE). 10 caracteres não-ASCII (ex.:
        // acentuados, 2 bytes cada em AL32UTF8) passavam no validator e estouravam o
        // Oracle com ORA-12899 → 500 (reproduzido pela G2 contra Oracle real). Medindo
        // em bytes UTF-8 de verdade em vez de caracteres.
        RuleFor(x => x.DsRegrasVersao)
            .Must(v => Encoding.UTF8.GetByteCount(v!) <= MaxRegrasVersaoBytes)
            .WithMessage($"'regras_versao' deve ter no máximo {MaxRegrasVersaoBytes} bytes UTF-8.")
            .When(x => x.DsRegrasVersao is not null);

        // Fix wave 1 (IMPORTANTE-2): antes deste fix não havia NENHUMA regra de faixa
        // para nr_score — NUMBER(5) estourava com ORA-01438 (valor maior do que o
        // permitido pela precisão) a partir de 100000, reproduzido pela G2 contra
        // Oracle real. Mínimo 0: o motor de regras da Luna (triage_engine.py,
        // kura-luna-ai) soma pontos positivos por categoria detectada (10/3/1) e nunca
        // produz score negativo — não há caso de uso do motor real para um valor
        // abaixo de zero, e permitir negativo abriria uma faixa que nenhum cliente
        // legítimo usa.
        RuleFor(x => x.NrScore)
            .InclusiveBetween(0, MaxNrScore)
            .WithMessage($"'nr_score' deve estar entre 0 e {MaxNrScore}.");
    }
}
