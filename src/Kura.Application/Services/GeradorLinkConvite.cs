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
/// ausente, um único <c>WARN</c> é logado e <see cref="GerarLink"/> passa a devolver sempre
/// <see langword="null"/> — o cadastro de tutor continua funcionando (sem o link), porque numa
/// demo o produto tem que continuar de pé.</para>
///
/// <para><b>"Na partida" depende de QUEM resolve o singleton, não só de ser Singleton</b> — a
/// injeção de dependência do .NET é LAZY por padrão: registrar como <c>AddSingleton</c> garante
/// "uma instância para todo o processo", não "construída no boot". G2 fix wave (achado Minor
/// #5) mediu, com sonda HTTP contra o host real, que sem uma resolução explícita o construtor só
/// rodava na primeira chamada de <see cref="GerarLink"/> em produção (o 1º <c>POST /tutores</c>)
/// — 0 WARN depois de <c>/health</c> responder, 1 WARN só depois do 1º cadastro. Fix: <c>
/// Program.cs</c> resolve <see cref="IGeradorLinkConvite"/> explicitamente logo depois do
/// <c>Build()</c>, ANTES de <c>app.Run()</c> — é essa linha, não o atributo Singleton, que torna
/// "na partida" verdadeiro.</para>
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
            // G2 fix wave, achado Minor #7: o nome de env citado aqui (CONVITE_URL_BASE_APP_TUTOR,
            // convenção de .env deste projeto) só tem efeito se o compose mapear
            // Convite__UrlBaseAppTutor: ${CONVITE_URL_BASE_APP_TUTOR...} (padrão já usado para
            // Luna__ApiKey/LUNA_API_KEY em docker-compose.yml) — mapeamento que a REC-05 ainda
            // vai escrever (hoje `git grep Convite` no compose ⇒ 0). Mantido o nome da env de
            // propósito (é o que quem opera o compose vai procurar), com a nota das duas pontas.
            logger.LogWarning(
                "Convite:UrlBaseAppTutor ausente/vazio — DsLinkConvite sairá null em todo " +
                "cadastro de tutor (POST /api/v1/tutores). Configurar a env CONVITE_URL_BASE_APP_TUTOR " +
                "e o mapeamento Convite__UrlBaseAppTutor no compose (REC-05) para habilitar o link " +
                "do convite.");
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
