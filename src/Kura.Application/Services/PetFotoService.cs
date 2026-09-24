namespace Kura.Application.Services;

using Kura.Application.DTOs.Pet;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Domain.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Upload da foto do pet pela clínica — FT-03/backlog <c>KURA_BACKLOG_FOTO_PET.md</c>.
///
/// <para><b>Ordem da troca (regra do backlog, "Ciclo de vida" 8.9): salva os 2 arquivos
/// NOVOS → atualiza a linha → <see cref="IUnitOfWork.CommitAsync"/> → SÓ DEPOIS exclui os 2
/// antigos.</b> Se o commit nunca acontecer (exceção antes dele), os arquivos antigos nunca
/// são tocados — o pet continua com a foto de antes, íntegra. Falha ao excluir o antigo
/// DEPOIS do commit é logada como aviso (sem PII — só chave/ids numéricos, que não são dado
/// de paciente/tutor) e NUNCA falha o request: a foto nova já está confirmada, um órfão no
/// disco é dívida de storage, não um bug visível ao usuário.</para>
///
/// <para><b>Se o SEGUNDO arquivo (media) falhar ao salvar, o PRIMEIRO (thumb) já salvo é
/// excluído</b> antes de propagar a exceção — nunca deixamos um arquivo novo órfão no disco
/// por causa de uma escrita parcial.</para>
/// </summary>
public sealed class PetFotoService : IPetFotoService
{
    private readonly IPetRepository _petRepository;
    private readonly IArmazenamentoArquivos _armazenamento;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<PetFotoService> _logger;

    public PetFotoService(
        IPetRepository petRepository,
        IArmazenamentoArquivos armazenamento,
        IUnitOfWork uow,
        ILogger<PetFotoService> logger)
    {
        _petRepository = petRepository;
        _armazenamento = armazenamento;
        _uow = uow;
        _logger = logger;
    }

    public async Task<PetFotoResponseDto> UploadFotoAsync(
        long idPet, IFormFile thumb, IFormFile media, CancellationToken ct)
    {
        // GetByIdAsync já é escopado por ApplyTenantFilters (Pet está entre as 8 entidades
        // do filtro de tenant) — pet de outra clínica não é encontrado aqui, 404 genuíno,
        // não um 403 que revelaria a existência do recurso alheio.
        var pet = await _petRepository.GetByIdAsync(idPet)
            ?? throw new EntidadeNaoEncontradaException("Pet", idPet);

        // Defesa em profundidade: o validator (PetFotoUploadValidator, roda antes via
        // FluentValidation auto-validation) já garante que thumb/media são JPEG/PNG/WebP
        // válidos por magic bytes E do MESMO formato (ruling F7-a — ver o validator, que
        // devolve 400 para os dois casos). Detectar de novo aqui é barato (só lê o
        // cabeçalho) e este service não deveria confiar cegamente em nunca ser chamado fora
        // do pipeline HTTP validado (ex.: um teste unitário direto, como os desta própria
        // task) — por isso o "mesmo formato" ainda é conferido aqui, como rede de segurança
        // (422, não 400: este caminho só é alcançável contornando o validator).
        var formatoThumb = ValidadorAssinaturaImagem.Detectar(thumb)
            ?? throw new RegraDeNegocioException("Parte 'thumb' não é uma imagem válida.");
        var formatoMedia = ValidadorAssinaturaImagem.Detectar(media)
            ?? throw new RegraDeNegocioException("Parte 'media' não é uma imagem válida.");

        if (formatoThumb != formatoMedia)
        {
            throw new RegraDeNegocioException(
                "As partes 'thumb' e 'media' precisam ser do mesmo formato de imagem.");
        }

        var extensao = ValidadorAssinaturaImagem.ExtensaoPara(formatoThumb);
        var contentType = ValidadorAssinaturaImagem.ContentTypePara(formatoThumb);

        // Ruling F7-a / regra A2 do backlog: fórmula única em Kura.Domain.Storage.ChaveFotoPet
        // (compartilhada com a FT-04, que lê, e a FT-05/Java, que replica com âncora).
        var chaveBase = ChaveFotoPet.Base(pet.IdClinica, pet.Id, Guid.NewGuid().ToString("N"), extensao);
        var chaveThumbNova = ChaveFotoPet.Variante(chaveBase, ChaveFotoPet.SufixoThumb);
        var chaveMediaNova = ChaveFotoPet.Variante(chaveBase, ChaveFotoPet.SufixoMedia);

        var chaveBaseAntiga = pet.DsFotoChave; // captura ANTES de sobrescrever

        await using (var streamThumb = thumb.OpenReadStream())
        {
            await _armazenamento.SalvarAsync(chaveThumbNova, streamThumb, contentType, ct);
        }

        try
        {
            await using var streamMedia = media.OpenReadStream();
            await _armazenamento.SalvarAsync(chaveMediaNova, streamMedia, contentType, ct);
        }
        catch
        {
            // O 1º arquivo (thumb) já foi salvo com sucesso — não deixar órfão se o 2º falhar.
            await _armazenamento.ExcluirAsync(chaveThumbNova, ct);
            throw;
        }

        pet.DsFotoChave = chaveBase;
        pet.DtFotoAtualizacao = DateTime.UtcNow;
        _petRepository.Update(pet);
        await _uow.CommitAsync();

        // SÓ DEPOIS do commit: exclui as variantes antigas (troca de foto). Pet sem foto
        // anterior (chaveBaseAntiga null) não tem nada para excluir.
        if (chaveBaseAntiga is not null)
            await ExcluirVariantesAntigasAsync(chaveBaseAntiga, ct);

        return new PetFotoResponseDto
        {
            IdPet = pet.Id,
            DsFotoChave = pet.DsFotoChave,
            DtFotoAtualizacao = pet.DtFotoAtualizacao.Value,
        };
    }

    private async Task ExcluirVariantesAntigasAsync(string chaveBaseAntiga, CancellationToken ct)
    {
        var chaveThumbAntiga = ChaveFotoPet.Variante(chaveBaseAntiga, ChaveFotoPet.SufixoThumb);
        var chaveMediaAntiga = ChaveFotoPet.Variante(chaveBaseAntiga, ChaveFotoPet.SufixoMedia);

        // Falha ao excluir arquivo antigo é aviso, nunca falha do request (regra do
        // backlog) — a foto nova já está commitada; um órfão no disco é dívida de storage.
        // Log sem PII: só chave (ids numéricos de clínica/pet + uuid), nunca dado de
        // paciente/tutor.
        try
        {
            await _armazenamento.ExcluirAsync(chaveThumbAntiga, ct);
            await _armazenamento.ExcluirAsync(chaveMediaAntiga, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Falha ao excluir variantes antigas de foto (chave base {ChaveBaseAntiga}) após troca.",
                chaveBaseAntiga);
        }
    }
}
