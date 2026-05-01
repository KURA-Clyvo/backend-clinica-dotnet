namespace Kura.Api.Extensions;

using FluentValidation;
using Kura.Api.Services;
using Kura.Application.Services;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
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

        // GROUP C — Clinical events
        services.AddScoped<IEventoClinicoService, EventoClinicoService>();
        services.AddScoped<IVacinaService, VacinaService>();
        services.AddScoped<IPrescricaoService, PrescricaoService>();
        services.AddScoped<IExameService, ExameService>();

        // GROUP D — Supporting entities
        services.AddScoped<INotificacaoService, NotificacaoService>();
        services.AddScoped<IDispositivoIotService, DispositivoIotService>();
        services.AddScoped<ILeituraTemperaturaService, LeituraTemperaturaService>();
        services.AddScoped<IAlertaTemperaturaService, AlertaTemperaturaService>();

        return services;
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not configured.");

        services.AddDbContext<KuraDbContext>(options =>
            options.UseOracle(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IClinicaContext, ClinicaContext>();

        // Repositórios especializados
        services.AddScoped<IClinicaRepository, ClinicaRepository>();
        services.AddScoped<IVeterinarioRepository, VeterinarioRepository>();
        services.AddScoped<ITutorRepository, TutorRepository>();
        services.AddScoped<IPetRepository, PetRepository>();
        services.AddScoped<ITutorPetRepository, TutorPetRepository>();
        services.AddScoped<IEventoClinicoRepository, EventoClinicoRepository>();
        services.AddScoped<ITimelineRepository, TimelineRepository>();

        return services;
    }
}
