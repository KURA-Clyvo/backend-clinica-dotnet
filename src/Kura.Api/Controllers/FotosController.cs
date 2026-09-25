namespace Kura.Api.Controllers;

using System.Text.RegularExpressions;
using Kura.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Serve os bytes de uma foto de pet (FT-04/backlog <c>KURA_BACKLOG_FOTO_PET.md</c>) —
/// endpoint <see cref="AllowAnonymousAttribute"/> por desenho (regra A6): a URL carrega sua
/// própria autorização via assinatura HMAC (<see cref="IAssinadorUrlFoto"/>, FT-02), não JWT
/// — é o que permite ao <c>&lt;Image&gt;</c> do app (que não manda <c>Authorization</c>) e ao
/// tutor (Java, sem JWT que este <c>.NET</c> aceite) abrirem a mesma URL.
///
/// <para><b>Ordem de validação, cada passo podendo devolver antes do próximo (brief FT-04):</b>
/// <list type="number">
/// <item><description>Chave contra o padrão FECHADO <see cref="PadraoChaveServida"/> — a MESMA
/// base de storage (<c>Storage:BasePath</c>) também guarda os PDFs de receituário (achado
/// F7-c, <c>g2-ft01-ft02.md</c>): sem esta restrição, qualquer chave assinada nessa base —
/// inclusive um receituário, PII de saúde — seria servida por este endpoint anônimo.</description></item>
/// <item><description>Assinatura (<see cref="IAssinadorUrlFoto.Validar"/>): cobre <c>sig</c>
/// adulterada, <c>exp</c> alterado, <c>exp</c> vencido e <c>sig</c> ausente/malformada — a
/// implementação (<c>AssinadorUrlFotoHmac</c>) já decide tudo isso.</description></item>
/// <item><description><see cref="IArmazenamentoArquivos.AbrirAsync"/> — só chega aqui com
/// chave no formato certo e assinatura válida.</description></item>
/// </list>
/// <b>Chave fora do padrão E assinatura inválida devolvem o MESMO status e o MESMO corpo</b>
/// (brief FT-04: "não diga ao cliente qual falhou") — só arquivo ausente (chave válida,
/// assinatura válida, mas <see cref="IArmazenamentoArquivos.AbrirAsync"/> devolve
/// <see langword="null"/>) é <c>404</c>, porque nesse caso o cliente já provou que tinha uma
/// URL genuína desta API.</para>
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/v1/fotos")]
public class FotosController : ControllerBase
{
    /// <summary>
    /// Padrão FECHADO da chave servida por este endpoint — só variantes de foto de pet
    /// (<c>clinica/{idClinica}/pet/{idPet}/{identificador}_(256|1080).(webp|jpg|png)</c>,
    /// fórmula de <see cref="Kura.Domain.Storage.ChaveFotoPet"/>). Age ANTES da assinatura
    /// (achado F7-c): mesmo que alguém um dia assine uma chave de receituário com o mesmo
    /// segredo, este regex barra antes de a assinatura ser sequer conferida.
    /// </summary>
    private static readonly Regex PadraoChaveServida = new(
        @"^clinica/\d+/pet/\d+/[^/]+_(256|1080)\.(webp|jpg|png)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Mapa FECHADO extensão → Content-Type (brief FT-04) — nunca <c>Content-Type</c>
    /// declarado por quem chamou (este endpoint não recebe nenhum) nem inferido por
    /// biblioteca de terceiros. As 3 extensões são exatamente as que
    /// <c>ValidadorAssinaturaImagem</c> (FT-03) produz a partir dos magic bytes no upload —
    /// nenhuma chave gravada por este sistema pode ter outra extensão.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ContentTypePorExtensao =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["webp"] = "image/webp",
            ["jpg"] = "image/jpeg",
            ["png"] = "image/png",
        };

    private readonly IArmazenamentoArquivos _armazenamento;
    private readonly IAssinadorUrlFoto _assinador;
    private readonly TimeProvider _timeProvider;

    public FotosController(
        IArmazenamentoArquivos armazenamento,
        IAssinadorUrlFoto assinador,
        TimeProvider timeProvider)
    {
        _armazenamento = armazenamento;
        _assinador = assinador;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Devolve os bytes da foto identificada por <paramref name="chave"/>, se a assinatura
    /// (<paramref name="exp"/>/<paramref name="sig"/>) for válida.
    /// </summary>
    /// <param name="chave">Caminho completo da variante (rota catch-all — contém barras).</param>
    /// <param name="exp">Unix timestamp (segundos) de expiração da assinatura.</param>
    /// <param name="sig">Assinatura HMAC-SHA256 base64url sem padding (ver <see cref="IAssinadorUrlFoto"/>).</param>
    /// <param name="ct">Token de cancelamento (encerra a leitura do arquivo se a conexão cair).</param>
    /// <response code="200">Bytes da foto, com o Content-Type real e cache imutável.</response>
    /// <response code="403">Chave fora do padrão servido, ou assinatura ausente/inválida/vencida.</response>
    /// <response code="404">Chave e assinatura válidas, mas o arquivo não existe no armazenamento.</response>
    [HttpGet("{*chave}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Obter(
        string? chave, [FromQuery] long? exp, [FromQuery] string? sig, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(chave) || !PadraoChaveServida.IsMatch(chave))
            return AcessoNegado();

        if (exp is null || string.IsNullOrEmpty(sig)
            || !_assinador.Validar(chave, exp.Value, sig, _timeProvider.GetUtcNow()))
        {
            return AcessoNegado();
        }

        var conteudo = await _armazenamento.AbrirAsync(chave, ct);
        if (conteudo is null)
            return NotFound();

        // A extensão vem do 2º grupo do MESMO regex que já validou a chave — nunca falta e
        // nunca é outra coisa que não uma das 3 chaves de ContentTypePorExtensao.
        var extensao = PadraoChaveServida.Match(chave).Groups[2].Value;
        var contentType = ContentTypePorExtensao[extensao];

        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        return File(conteudo, contentType);
    }

    /// <summary>
    /// MESMO status e MESMO corpo para "chave fora do padrão" e "assinatura inválida" — um
    /// corpo diferente por caso permitiria a quem tenta adivinhar uma URL descobrir qual das
    /// duas checagens falhou (brief FT-04).
    /// </summary>
    private IActionResult AcessoNegado() =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Title = "Acesso negado.",
            Status = StatusCodes.Status403Forbidden,
        });
}
