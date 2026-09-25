namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Application.DTOs.Pet;
using Kura.Application.Services;

/// <summary>
/// Valida as 2 partes multipart de <c>POST /api/v1/pets/{id}/foto</c> por MAGIC BYTES
/// (<see cref="ValidadorAssinaturaImagem"/>) — nunca por <c>Content-Type</c>/extensão do
/// cliente. FT-03/backlog <c>KURA_BACKLOG_FOTO_PET.md</c>, "Validação no servidor".
///
/// <para><b>Como isto vira 400.</b> Nenhuma exceção de <c>Kura.Domain.Exceptions</c> mapeia
/// 400 no <c>ExceptionHandlerMiddleware</c> (só 404/422/409/401/500). 🔴 <b>Fix wave G2
/// (g2-ft03.md, achado G2-c/G2-e):</b> este validator NÃO é mais invocado pela auto-validation
/// automática do FluentValidation (que reage a parâmetro `[FromForm]`/ModelState) — o
/// controller (<c>PetsController.UploadFoto</c>) lê o multipart ele mesmo
/// (<c>Request.ReadFormAsync()</c>, necessário para o 413 chegar ao middleware) e chama
/// <c>Validate(dto)</c> manualmente, convertendo os erros em <c>ValidationProblem</c> — mesmo
/// formato de resposta (400 <c>ValidationProblemDetails</c>), mecanismo de invocação
/// diferente. Medido com teste HTTP real em <c>PetFotoHttpTests</c>, não presumido.</para>
///
/// <para>🔴 <b>MEDIDO: as regras têm de ser SÍNCRONAS (<c>Must</c>), não
/// <c>MustAsync</c>.</b> Uma primeira versão usava <c>MustAsync</c> e TODO request devolvia
/// <c>500</c> com <c>AsyncValidatorInvokedSynchronouslyException</c> — o pipeline de
/// auto-validation deste projeto (<c>FluentValidation.AspNetCore</c>) invoca os validadores
/// de forma síncrona, e uma regra assíncrona faz o validador inteiro ser rejeitado em
/// runtime. <see cref="ValidadorAssinaturaImagem.Detectar"/> é síncrono por causa disso — ver
/// o XML doc dele.</para>
///
/// <para><b>Ruling F7-a do maestro (G2 <c>g2-ft01-ft02.md</c>, FT-02/FT-03):</b> <c>thumb</c>
/// e <c>media</c> precisam ter o MESMO formato detectado — se divergirem, também é 400 (não
/// 422), decidido aqui no validator e não no service, exatamente para cair no mesmo mecanismo
/// dos outros dois casos.</para>
/// </summary>
public sealed class PetFotoUploadValidator : AbstractValidator<PetFotoUploadDto>
{
    public PetFotoUploadValidator()
    {
        RuleFor(x => x.Thumb)
            .NotNull().WithMessage("Parte multipart 'thumb' é obrigatória.");

        RuleFor(x => x.Thumb)
            .Must(f => f!.Length > 0).WithMessage("Parte 'thumb' não pode ser um arquivo vazio.")
            .When(x => x.Thumb is not null);

        RuleFor(x => x.Thumb)
            .Must(f => ValidadorAssinaturaImagem.Detectar(f) is not null)
            .WithMessage(
                "Parte 'thumb' não é um JPEG, PNG ou WebP válido (verificado pelos primeiros "
                + "bytes do arquivo, nunca pelo Content-Type informado pelo cliente).")
            .When(x => x.Thumb is not null && x.Thumb.Length > 0);

        RuleFor(x => x.Media)
            .NotNull().WithMessage("Parte multipart 'media' é obrigatória.");

        RuleFor(x => x.Media)
            .Must(f => f!.Length > 0).WithMessage("Parte 'media' não pode ser um arquivo vazio.")
            .When(x => x.Media is not null);

        RuleFor(x => x.Media)
            .Must(f => ValidadorAssinaturaImagem.Detectar(f) is not null)
            .WithMessage(
                "Parte 'media' não é um JPEG, PNG ou WebP válido (verificado pelos primeiros "
                + "bytes do arquivo, nunca pelo Content-Type informado pelo cliente).")
            .When(x => x.Media is not null && x.Media.Length > 0);

        // Ruling F7-a: só faz sentido comparar formato quando as duas partes já passaram,
        // individualmente, pelas regras acima (presentes, não-vazias, magic bytes válidos)
        // — senão estaríamos reportando "formatos diferentes" quando o problema real é
        // "uma das duas nem é imagem", mensagem enganosa.
        RuleFor(x => x)
            .Must(dto =>
            {
                var formatoThumb = ValidadorAssinaturaImagem.Detectar(dto.Thumb);
                var formatoMedia = ValidadorAssinaturaImagem.Detectar(dto.Media);
                if (formatoThumb is null || formatoMedia is null)
                    return true; // já reportado pela regra individual de magic bytes — evita mensagem duplicada
                return formatoThumb == formatoMedia;
            })
            .WithMessage(
                "As partes 'thumb' e 'media' precisam ser do mesmo formato de imagem "
                + "(detectado pelos magic bytes de cada uma).")
            .WithName(nameof(PetFotoUploadDto))
            .When(x =>
                x.Thumb is not null && x.Thumb.Length > 0
                && x.Media is not null && x.Media.Length > 0);
    }
}
