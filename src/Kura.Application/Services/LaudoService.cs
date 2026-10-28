namespace Kura.Application.Services;

using System.Security.Cryptography;
using Kura.Application.DTOs.Common;
using Kura.Application.DTOs.Laudo;
using Kura.Application.Laudos;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Domain.Laudos;
using Microsoft.Extensions.Logging;

/// <summary>
/// S4-NET-03 — laudos. Ordem do POST: pet da clínica (404) → autor → assinatura → sha256 →
/// <b>arquivo no storage</b> → <b>documento no Mongo</b>. Se o documento falhar (Mongo fora,
/// schema recusou), o arquivo recém-gravado é APAGADO (compensação) e a exceção propaga: nunca
/// fica binário órfão por causa de metadado que não entrou.
///
/// <para>LGPD: nada de conteúdo, título, observação ou nome de arquivo em log. A chave de storage
/// só tem ids numéricos e um guid.</para>
/// </summary>
public sealed class LaudoService : ILaudoService
{
    private readonly ILaudoRepository _laudos;
    private readonly IPetRepository _pets;
    private readonly IUsuarioClinicaRepository _usuarios;
    private readonly IArmazenamentoArquivos _armazenamento;
    private readonly IClinicaContext _clinica;
    private readonly ILogger<LaudoService> _logger;

    public LaudoService(
        ILaudoRepository laudos,
        IPetRepository pets,
        IUsuarioClinicaRepository usuarios,
        IArmazenamentoArquivos armazenamento,
        IClinicaContext clinica,
        ILogger<LaudoService> logger)
    {
        _laudos = laudos;
        _pets = pets;
        _usuarios = usuarios;
        _armazenamento = armazenamento;
        _clinica = clinica;
        _logger = logger;
    }

    public async Task<LaudoResponseDto> CriarAsync(
        long idPet, LaudoCriarDto dto, string? emailAutor, CancellationToken ct)
    {
        var idClinica = _clinica.IdClinica;
        await GarantirPetDaClinicaAsync(idPet, idClinica);

        if (string.IsNullOrWhiteSpace(emailAutor))
            throw new UnauthorizedAccessException("Claim 'email' ausente no token JWT.");
        var autor = await _usuarios.BuscarPorEmailNaClinicaAsync(idClinica, emailAutor)
            ?? throw new UnauthorizedAccessException("Usuário do token não encontrado na clínica.");

        // Defesa em profundidade: o controller já validou (LaudoCriarValidator). Aqui a mesma
        // regra é reaplicada porque o service não deve confiar em nunca ser chamado sem ela.
        var arquivo = dto.Arquivo ?? throw new RegraDeNegocioException("Arquivo do laudo é obrigatório.");
        var formato = ValidadorAssinaturaDocumento.Detectar(arquivo)
            ?? throw new RegraDeNegocioException("Arquivo não é um PDF, JPEG, PNG ou WebP válido.");
        if (!LaudoEntrada.TentarTipo(dto.Tipo, out var tipo)
            || !LaudoEntrada.TentarData(dto.DataExame, out var dataExame)
            || !LaudoEntrada.TentarIdEvento(dto.IdEventoClinico, out var idEvento)
            || !LaudoEntrada.TentarDados(dto.Dados, out var dados)
            || string.IsNullOrWhiteSpace(dto.Titulo))
            throw new RegraDeNegocioException("Campos do laudo inválidos.");

        var sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(arquivo, ct));
        var tamanho = arquivo.Length;
        arquivo.Position = 0;

        var contentType = ValidadorAssinaturaDocumento.ContentTypePara(formato);
        var chave = $"clinica/{idClinica}/pet/{idPet}/laudo/{Guid.NewGuid():N}."
            + ValidadorAssinaturaDocumento.ExtensaoPara(formato);

        await _armazenamento.SalvarAsync(chave, arquivo, contentType, ct);

        Laudo criado;
        try
        {
            criado = await _laudos.InserirAsync(idClinica, new Laudo
            {
                IdClinica = idClinica,
                IdPet = idPet,
                IdEventoClinico = idEvento,
                IdUsuarioAutor = autor.Id,
                Tipo = tipo,
                Titulo = dto.Titulo.Trim(),
                DataExame = dataExame,
                Laboratorio = Vazio(dto.Laboratorio),
                Observacao = Vazio(dto.Observacao),
                Dados = dados,
                Arquivo = new ArquivoLaudo
                {
                    Chave = chave,
                    ContentType = contentType,
                    TamanhoBytes = tamanho,
                    Sha256 = sha256,
                },
            }, ct);
        }
        catch
        {
            await TentarApagarOrfaoAsync(chave);
            throw;
        }

        return ParaDto(criado);
    }

    public async Task<PagedResultDto<LaudoResponseDto>> ListarPorPetAsync(
        long idPet, LaudoListarDto consulta, CancellationToken ct)
    {
        var idClinica = _clinica.IdClinica;
        await GarantirPetDaClinicaAsync(idPet, idClinica);

        var pagina = await _laudos.ListarPorPetAsync(idClinica, idPet, new ConsultaLaudos(
            consulta.Page, consulta.PageSize, consulta.Tipo, consulta.De, consulta.Ate, consulta.Ordem), ct);

        return new PagedResultDto<LaudoResponseDto>
        {
            Items = pagina.Itens.Select(ParaDto).ToList(),
            Page = pagina.Pagina,
            PageSize = pagina.TamanhoPagina,
            Total = (int)Math.Min(pagina.Total, int.MaxValue),
        };
    }

    public async Task<LaudoResponseDto> ObterAsync(string id, CancellationToken ct) =>
        ParaDto(await ObterOu404Async(id, ct));

    public async Task<LaudoArquivoDownload> ObterArquivoAsync(string id, CancellationToken ct)
    {
        var laudo = await ObterOu404Async(id, ct);
        var stream = await _armazenamento.AbrirAsync(laudo.Arquivo.Chave, ct)
            ?? throw new EntidadeNaoEncontradaException("Arquivo do laudo", id);

        // Content-Type SEMPRE do mapa fechado: um valor fora dele (dado legado/adulterado) nunca
        // é repassado ao navegador como veio.
        var contentType = ValidadorAssinaturaDocumento.ContentTypesPermitidos.Contains(laudo.Arquivo.ContentType)
            ? laudo.Arquivo.ContentType
            : "application/octet-stream";
        return new LaudoArquivoDownload(
            stream, contentType, ValidadorAssinaturaDocumento.ExtensaoPorContentType(contentType));
    }

    public async Task ExcluirAsync(string id, CancellationToken ct)
    {
        if (!await _laudos.ExcluirLogicoAsync(_clinica.IdClinica, id, ct))
            throw new EntidadeNaoEncontradaException("Laudo", id);
    }

    private async Task<Laudo> ObterOu404Async(string id, CancellationToken ct) =>
        await _laudos.ObterAsync(_clinica.IdClinica, id, ct)
            ?? throw new EntidadeNaoEncontradaException("Laudo", id);

    private async Task GarantirPetDaClinicaAsync(long idPet, long idClinica)
    {
        // Consulta Oracle com ID_CLINICA explícito no predicado: pet de outra clínica e pet
        // inexistente dão a MESMA resposta (404), sem oráculo.
        _ = await _pets.GetByIdComVinculosAsync(idPet, idClinica)
            ?? throw new EntidadeNaoEncontradaException("Pet", idPet);
    }

    private async Task TentarApagarOrfaoAsync(string chave)
    {
        try
        {
            await _armazenamento.ExcluirAsync(chave, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao apagar arquivo órfão de laudo (chave {Chave}).", chave);
        }
    }

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static LaudoResponseDto ParaDto(Laudo l) => new()
    {
        Id = l.Id,
        IdPet = l.IdPet,
        IdEventoClinico = l.IdEventoClinico,
        IdUsuarioAutor = l.IdUsuarioAutor,
        Tipo = l.Tipo.ToString(),
        Titulo = l.Titulo,
        DataExame = l.DataExame,
        Laboratorio = l.Laboratorio,
        Observacao = l.Observacao,
        Dados = l.Dados,
        Arquivo = new LaudoArquivoResponseDto
        {
            ContentType = l.Arquivo.ContentType,
            TamanhoBytes = l.Arquivo.TamanhoBytes,
            Sha256 = l.Arquivo.Sha256,
        },
        CriadoEm = l.CriadoEm,
        AtualizadoEm = l.AtualizadoEm,
    };
}
