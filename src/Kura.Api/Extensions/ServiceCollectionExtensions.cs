namespace Kura.Api.Extensions;

using FluentValidation;
using Kura.Api.Services;
using Kura.Application.Services;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Interceptors;
using Kura.Infrastructure.Persistence.Repositories;
using Kura.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<Kura.Application.AssemblyMarker>();

        // GROUP A — Lookup/reference entities
        services.AddScoped<IEspecieService, EspecieService>();
        services.AddScoped<IRacaService, RacaService>();
        services.AddScoped<ITipoEventoService, TipoEventoService>();
        services.AddScoped<IMedicamentoService, MedicamentoService>();

        // GROUP B — Core clinic entities
        services.AddScoped<IClinicaService, ClinicaService>();
        services.AddScoped<IVeterinarioService, VeterinarioService>();
        services.AddScoped<ITutorService, TutorService>();
        services.AddScoped<IPetService, PetService>();
        // FT-03 (backlog KURA_BACKLOG_FOTO_PET.md): consome IArmazenamentoArquivos (FT-02),
        // registrado em AddInfrastructure.
        services.AddScoped<IPetFotoService, PetFotoService>();

        // GROUP C — Clinical events
        services.AddScoped<IEventoClinicoService, EventoClinicoService>();
        services.AddScoped<IVacinaService, VacinaService>();
        services.AddScoped<IPrescricaoService, PrescricaoService>();
        services.AddScoped<IExameService, ExameService>();
        services.AddScoped<IConsultaService, ConsultaService>();

        // GROUP D — Supporting entities
        services.AddScoped<INotificacaoService, NotificacaoService>();
        services.AddScoped<IDispositivoIotService, DispositivoIotService>();
        services.AddScoped<ILeituraTemperaturaService, LeituraTemperaturaService>();
        services.AddScoped<IAlertaTemperaturaService, AlertaTemperaturaService>();
        services.AddScoped<IAuthService, AuthService>();
        // FD-04: CRUD de USUARIO_CLINICA, protegido pela politica SomenteGestor.
        services.AddScoped<IUsuarioClinicaService, UsuarioClinicaService>();
        // FD-09: CRUD de SERVICO_PRECO, protegido pela mesma politica SomenteGestor.
        services.AddScoped<IServicoPrecoService, ServicoPrecoService>();
        // FD-10: lancamento de COBRANCA no evento clinico. ESCRITA e [Authorize]
        // (o veterinario lanca no fechamento do atendimento); LEITURA e SomenteGestor (D-7).
        services.AddScoped<ICobrancaService, CobrancaService>();

        // FD-11 - KPI financeiros agregados (leitura so, sem UnitOfWork).
        services.AddScoped<IFinanceiroService, FinanceiroService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAgendaService, AgendaService>();
        services.AddScoped<ILunaService, LunaService>();
        services.AddScoped<ITeleconsultaService, TeleconsultaService>();
        services.AddScoped<ISoapDraftService, SoapDraftService>();
        services.AddScoped<IReceituarioPdfService, ReceituarioPdfService>();

        return services;
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not configured.");

        services.AddDbContext<KuraDbContext>((sp, options) =>
        {
            options.UseOracle(
                connectionString,
                oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
            options.AddInterceptors(new ReadOnlyTablesInterceptor());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IClinicaContext, ClinicaContext>();
        services.AddScoped<Kura.Api.Filters.ApiKeyAuthFilter>();
        services.AddScoped<Kura.Api.Filters.LunaApiKeyAuthFilter>();

        // Repositórios especializados
        services.AddScoped<IClinicaRepository, ClinicaRepository>();
        services.AddScoped<IVeterinarioRepository, VeterinarioRepository>();
        services.AddScoped<IUsuarioClinicaRepository, UsuarioClinicaRepository>();
        services.AddScoped<IServicoPrecoRepository, ServicoPrecoRepository>();
        services.AddScoped<ICobrancaRepository, CobrancaRepository>();
        services.AddScoped<ITutorRepository, TutorRepository>();
        services.AddScoped<IPetRepository, PetRepository>();
        services.AddScoped<ITutorPetRepository, TutorPetRepository>();
        services.AddScoped<IEventoClinicoRepository, EventoClinicoRepository>();
        services.AddScoped<ITimelineRepository, TimelineRepository>();
        services.AddScoped<IAgendamentoRepository, AgendamentoRepository>();
        services.AddScoped<IAgendamentoReadRepository, AgendaReadRepository>();
        services.AddScoped<ITriagemLunaRepository, TriagemLunaRepository>();
        services.AddScoped<IInviteTutorRepository, InviteTutorRepository>();
        services.AddScoped<IConsentimentoRepository, ConsentimentoRepository>();

        // FT-02 (backlog KURA_BACKLOG_FOTO_PET.md): armazenamento de arquivo (foto de pet,
        // hoje) e URL assinada. ArmazenamentoLocalDisco reaproveita Storage:BasePath (mesma
        // config de ReceituarioPdfService). O segredo de assinatura é validado AQUI, de
        // forma síncrona e eager — mesmo padrão de Jwt:Key em Program.cs — para que
        // segredo ausente ou curto demais derrube o processo na partida, nunca no primeiro
        // upload/download de foto.
        var fotoUrlSecret = configuration["Foto:UrlSecret"]
            ?? throw new InvalidOperationException("Foto:UrlSecret not configured.");
        if (System.Text.Encoding.UTF8.GetByteCount(fotoUrlSecret) < 32)
            throw new InvalidOperationException(
                "Foto:UrlSecret must be at least 32 bytes (UTF-8).");

        services.AddScoped<IArmazenamentoArquivos, ArmazenamentoLocalDisco>();
        services.AddScoped<IAssinadorUrlFoto>(_ => new AssinadorUrlFotoHmac(fotoUrlSecret));

        // FT-04 (backlog KURA_BACKLOG_FOTO_PET.md): monta a URL assinada completa (base +
        // chave da variante + exp + sig) a partir da chave BASE gravada no Pet — ver
        // GeradorUrlFotoPet para o porquê de a implementação morar em Kura.Api (precisa de
        // IHttpContextAccessor para a base derivada do request atual). TimeProvider.System
        // como singleton: relógio real em produção, substituível por um fake em teste
        // (WithWebHostBuilder) sem Thread.Sleep. SEM fail-fast novo para Foto:UrlBase/
        // Foto:ValidadeUrlHoras de propósito (brief FT-04): ausência é o caminho NORMAL
        // (deriva do request, default 24h), diferente de Foto:UrlSecret acima.
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IGeradorUrlFotoPet, GeradorUrlFotoPet>();

        services.AddHttpClient<IDailyService, DailyService>((sp, http) =>
        {
            var apiKey = configuration["Daily:ApiKey"]
                ?? throw new InvalidOperationException("Daily:ApiKey not configured.");
            http.BaseAddress = new Uri("https://api.daily.co/v1/");
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        });

        services.AddHttpClient<ILunaTranscricaoService, LunaTranscricaoService>((sp, http) =>
        {
            var baseUrl = configuration["Luna:BaseUrl"]
                ?? throw new InvalidOperationException("Luna:BaseUrl not configured.");
            var apiKey = configuration["Luna:InboundApiKey"]
                ?? throw new InvalidOperationException("Luna:InboundApiKey not configured.");
            http.BaseAddress = new Uri(baseUrl);
            http.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        });

        return services;
    }
}
