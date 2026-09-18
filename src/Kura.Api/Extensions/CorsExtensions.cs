namespace Kura.Api.Extensions;

/// <summary>
/// CORS para o <c>mobile-clinica-rn</c> rodando no navegador (Expo web): sem isto todo
/// preflight <c>OPTIONS</c> respondia 405 e o app da clínica só funcionava em modo mock.
///
/// <para>
/// Origens vêm de <c>Cors:AllowedOrigins</c> (env <c>Cors__AllowedOrigins</c>) ou, se vazio,
/// de <c>CORS_ALLOWED_ORIGINS</c> — o mesmo nome que a API Java e a Luna usam, para o
/// docker-compose do DevOps-Cloud configurar os três com uma variável só. Lista separada por
/// vírgula. Vazio = nenhuma política registrada, comportamento anterior preservado.
/// </para>
/// </summary>
public static class CorsExtensions
{
    public const string NomePolitica = "KuraWeb";

    public static IServiceCollection AddKuraCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origens = LerOrigens(configuration);
        if (origens.Length == 0) return services;

        services.AddCors(options => options.AddPolicy(NomePolitica, policy => policy
            .WithOrigins(origens)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Content-Disposition")));
        return services;
    }

    public static WebApplication UseKuraCors(this WebApplication app)
    {
        if (LerOrigens(app.Configuration).Length > 0) app.UseCors(NomePolitica);
        return app;
    }

    internal static string[] LerOrigens(IConfiguration configuration)
    {
        var bruto = configuration["Cors:AllowedOrigins"];
        if (string.IsNullOrWhiteSpace(bruto)) bruto = configuration["CORS_ALLOWED_ORIGINS"];
        return (bruto ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
