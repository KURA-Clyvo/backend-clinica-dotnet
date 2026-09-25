namespace Kura.Api.Services;

using Kura.Domain.Interfaces;
using Kura.Domain.Storage;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Implementação de <see cref="IGeradorUrlFotoPet"/> — FT-04/backlog
/// <c>KURA_BACKLOG_FOTO_PET.md</c>.
///
/// <para><b>Base da URL:</b> <c>Foto:UrlBase</c> quando configurado (produção/compose, atrás
/// de proxy/domínio fixo); senão DERIVADA do request atual em curso
/// (<c>scheme://host{PathBase}</c>) via <see cref="IHttpContextAccessor"/> — mesmo padrão de
/// <c>ClinicaContext</c> (também em <c>Kura.Api.Services</c>), que lê o request atual em vez
/// de exigir configuração nova. <b>Sem fail-fast novo</b> para nenhuma das duas chaves desta
/// task (decisão do maestro, brief FT-04): ausência de <c>Foto:UrlBase</c> é o caminho
/// NORMAL em dev/teste (deriva do request), não um erro de configuração.</para>
///
/// <para><b>Validade:</b> <c>Foto:ValidadeUrlHoras</c> (default 24, sem fail-fast) — mesmo
/// raciocínio.</para>
///
/// <para><b>Relógio injetável</b> (<see cref="TimeProvider"/>, registrado como singleton em
/// <c>ServiceCollectionExtensions.AddInfrastructure</c>): permite a um teste fixar "agora"
/// para gerar uma URL já vencida sem precisar de <c>Thread.Sleep</c>.</para>
/// </summary>
public sealed class GeradorUrlFotoPet : IGeradorUrlFotoPet
{
    private const double ValidadeHorasPadrao = 24;

    private readonly IAssinadorUrlFoto _assinador;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public GeradorUrlFotoPet(
        IAssinadorUrlFoto assinador,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        TimeProvider timeProvider)
    {
        _assinador = assinador;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public string? GerarUrl(string? chaveBase, string sufixoTamanho)
    {
        if (string.IsNullOrEmpty(chaveBase))
            return null;

        var chaveVariante = ChaveFotoPet.Variante(chaveBase, sufixoTamanho);

        var validadeHoras = _configuration.GetValue<double?>("Foto:ValidadeUrlHoras") ?? ValidadeHorasPadrao;
        var expiraEm = _timeProvider.GetUtcNow().AddHours(validadeHoras);

        var sig = _assinador.Assinar(chaveVariante, expiraEm);
        var exp = expiraEm.ToUnixTimeSeconds();

        return $"{ObterBaseUrl()}/api/v1/fotos/{chaveVariante}?exp={exp}&sig={sig}";
    }

    private string ObterBaseUrl()
    {
        var baseConfigurada = _configuration["Foto:UrlBase"];
        if (!string.IsNullOrWhiteSpace(baseConfigurada))
            return baseConfigurada.TrimEnd('/');

        var request = _httpContextAccessor.HttpContext?.Request
            ?? throw new InvalidOperationException(
                "Não foi possível derivar a base da URL de foto: nenhum HttpContext ativo " +
                "e 'Foto:UrlBase' não está configurado. Fora de um request HTTP (ex.: job em " +
                "background), configure 'Foto:UrlBase' explicitamente.");

        return $"{request.Scheme}://{request.Host}{request.PathBase}";
    }
}
