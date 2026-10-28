namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Laudo;
using Kura.Application.Laudos;
using Kura.Application.Services;

/// <summary>
/// Valida o multipart de <c>POST /api/v1/pets/{idPet}/laudos</c> (S4-NET-03). Chamado
/// manualmente pelo controller (mesmo desenho da foto do pet: o form é lido à mão por causa do
/// 413 do Kestrel) e convertido em <c>ValidationProblem</c> (400).
///
/// <para>Regras síncronas de propósito (<c>Must</c>, nunca <c>MustAsync</c>): o pipeline do
/// FluentValidation deste projeto é síncrono (ver <see cref="ValidadorAssinaturaImagem"/>).</para>
/// <para>O arquivo é validado por ASSINATURA (<see cref="ValidadorAssinaturaDocumento"/>), nunca
/// por extensão ou Content-Type do cliente.</para>
/// </summary>
public sealed class LaudoCriarValidator : AbstractValidator<LaudoCriarDto>
{
    /// <summary>Teto do arquivo (D-NET-1 do G0): 10 MiB.</summary>
    public const long TamanhoMaximoBytes = 10L * 1024 * 1024;

    public LaudoCriarValidator(TimeProvider relogio)
    {
        RuleFor(x => x.Tipo)
            .Must(t => LaudoEntrada.TentarTipo(t, out _))
            .WithMessage("'tipo' deve ser HEMOGRAMA, BIOQUIMICO, IMAGEM, RECEITA ou OUTRO.");

        RuleFor(x => x.Titulo)
            .Must(t => !string.IsNullOrWhiteSpace(t))
            .WithMessage("'titulo' é obrigatório.")
            .Must(t => t is null || t.Trim().Length <= 120)
            .WithMessage("'titulo' deve ter no máximo 120 caracteres.");

        RuleFor(x => x.Laboratorio)
            .Must(t => t is null || t.Length <= 120)
            .WithMessage("'laboratorio' deve ter no máximo 120 caracteres.");

        RuleFor(x => x.Observacao)
            .Must(t => t is null || t.Length <= 1000)
            .WithMessage("'observacao' deve ter no máximo 1000 caracteres.");

        RuleFor(x => x.DataExame)
            .Must(t => LaudoEntrada.TentarData(t, out _))
            .WithMessage("'dataExame' deve ser uma data ISO-8601 (ex.: 2026-10-01 ou 2026-10-01T14:30:00Z).")
            .Must(t => !LaudoEntrada.TentarData(t, out var d) || d <= relogio.GetUtcNow().UtcDateTime.AddDays(1))
            .WithMessage("'dataExame' não pode estar no futuro (tolerância de 1 dia).");

        RuleFor(x => x.IdEventoClinico)
            .Must(t => LaudoEntrada.TentarIdEvento(t, out _))
            .WithMessage("'idEventoClinico' deve ser um número inteiro positivo.");

        RuleFor(x => x.Dados)
            .Must(t => LaudoEntrada.TentarDados(t, out _))
            .WithMessage(
                "'dados' deve ser um OBJETO JSON válido de até "
                + LaudoEntrada.MaxCaracteresDados + " caracteres.");

        RuleFor(x => x.Arquivo)
            .NotNull().WithMessage("Parte multipart 'arquivo' é obrigatória.");

        RuleFor(x => x.Arquivo)
            .Must(a => a!.Length > 0).WithMessage("'arquivo' não pode ser vazio.")
            .Must(a => a!.Length <= TamanhoMaximoBytes).WithMessage("'arquivo' excede o limite de 10 MiB.")
            .When(x => x.Arquivo is not null);

        RuleFor(x => x.Arquivo)
            .Must(a => ValidadorAssinaturaDocumento.Detectar(a) is not null)
            .WithMessage(
                "'arquivo' não é um PDF, JPEG, PNG ou WebP válido (verificado pelos primeiros bytes, "
                + "nunca pela extensão ou Content-Type informados).")
            .When(x => x.Arquivo is not null && x.Arquivo.Length > 0 && x.Arquivo.Length <= TamanhoMaximoBytes);
    }
}
