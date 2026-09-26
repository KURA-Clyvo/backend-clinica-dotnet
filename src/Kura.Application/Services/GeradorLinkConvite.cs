namespace Kura.Application.Services;

using Kura.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implementação de <see cref="IGeradorLinkConvite"/> — REC-01/backlog
/// <c>KURA_BACKLOG_RECEPCAO.md</c>, A-8.
///
/// <para>Diferente de <c>GeradorUrlFotoPet.cs</c> (que mora em <c>Kura.Api</c> porque precisa do
/// <c>HttpContext</c> atual para derivar a base quando não configurada): este helper NUNCA
/// deriva de request — o link do convite é consumido fora de qualquer contexto HTTP relacionado
/// (QR code, WhatsApp), então a base é SEMPRE explícita (<c>Convite:UrlBaseAppTutor</c>). Sem
/// dependência de tipo ASP.NET nenhum, mora em <c>Kura.Application</c>.</para>
///
/// <para><b>Config ausente/vazia não derruba o processo</b> (mesmo padrão do
/// <c>GeradorUrlFotoPet.java</c> do lado tutor, FT-05 — decisão do maestro para REC-01): a base é
/// lida e validada UMA VEZ no construtor (singleton, ver <c>ServiceCollectionExtensions</c>); se
/// ausente, um único <c>WARN</c> é logado na partida e <see cref="GerarLink"/> passa a devolver
/// sempre <see langword="null"/> — o cadastro de tutor continua funcionando (sem o link), porque
/// numa demo o produto tem que continuar de pé.</para>
/// </summary>
public sealed class GeradorLinkConvite : IGeradorLinkConvite
{
    private readonly string? _baseUrl;

    public GeradorLinkConvite(IConfiguration configuration, ILogger<GeradorLinkConvite> logger)
    {
        var baseConfigurada = configuration["Convite:UrlBaseAppTutor"];
        if (string.IsNullOrWhiteSpace(baseConfigurada))
        {
            _baseUrl = null;
            logger.LogWarning(
                "Convite:UrlBaseAppTutor ausente/vazio — DsLinkConvite sairá null em todo " +
                "cadastro de tutor (POST /api/v1/tutores). Configurar CONVITE_URL_BASE_APP_TUTOR " +
                "(REC-05) para habilitar o link do convite.");
        }
        else
        {
            _baseUrl = baseConfigurada.TrimEnd('/');
        }
    }

    public string? GerarLink(Guid token, long idClinica)
    {
        if (_baseUrl is null)
            return null;

        var tokenCodificado = Uri.EscapeDataString(token.ToString());
        var clinicaCodificada = Uri.EscapeDataString(idClinica.ToString());
        return $"{_baseUrl}/register?token={tokenCodificado}&clinicaId={clinicaCodificada}";
    }
}
