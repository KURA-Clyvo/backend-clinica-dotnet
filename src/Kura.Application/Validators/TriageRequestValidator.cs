namespace Kura.Application.Validators;

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

        // LU-08: opcional e retrocompatível (payload sem o campo continua 201) —
        // só valida tamanho quando presente, nunca presença. DS_REGRAS_VERSAO é
        // VARCHAR2(10) na V21 (backend-tutor-java, em paralelo).
        RuleFor(x => x.DsRegrasVersao)
            .MaximumLength(10)
            .WithMessage("'regras_versao' deve ter no máximo 10 caracteres.")
            .When(x => x.DsRegrasVersao is not null);
    }
}
