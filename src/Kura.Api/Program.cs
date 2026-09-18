using Serilog;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FluentValidation.AspNetCore;
using Microsoft.OpenApi;
using Kura.Api.Extensions;
using Kura.Infrastructure.Persistence;

// QuestPDF (geração de receituário, TASK-15): licença Community — gratuita para
// organizações com receita anual < US$1M (não é mais MIT puro desde 2023.12,
// mas segue free-first e sem custo para este projeto acadêmico).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Serilog — 3-arg overload so the built IServiceProvider is available for the Oracle sink
// S3D-01: sink de arquivo (rolling diário) ao lado do console — a rubrica pede
// "console/arquivo" e um dos dois já bastaria, mas o custo é baixo e remove ambiguidade
// de leitura. Caminho relativo ("logs/") funciona tanto local (fica ao lado do binário
// em execução) quanto em container (relativo ao WORKDIR da imagem), sem exigir volume
// dedicado nem configuração adicional.
// LU-16 G4 (achado A4, BLOQUEANTE): appsettings.json declara
// "Logging:LogLevel:Microsoft.AspNetCore": "Warning" — mas .ReadFrom.Configuration()
// do Serilog.Settings.Configuration só lê a seção "Serilog:...", nunca a seção
// "Logging:..." (convenção do Microsoft.Extensions.Logging puro). Por isso aquele
// override nunca chegava a valer para o Serilog, e
// Microsoft.AspNetCore.Hosting.Diagnostics (categoria que emite "Request starting"/
// "Request finished" em nível Information, sempre com context.Request.Path CRU —
// GET /api/v1/tutores/telefone/{numero} carrega o telefone do tutor no próprio path)
// vazava o telefone em log a cada requisição bem-sucedida. O Override abaixo torna
// o intent já declarado em appsettings.json efetivo de verdade para o Serilog —
// não inventa configuração nova, corrige a que já existia e estava inerte.
builder.Host.UseSerilog((ctx, sp, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    // LU-16 G4 (achado A4, BLOQUEANTE): redige qualquer propriedade RequestPath/Path
    // de PII antes de qualquer sink ver o LogEvent — ver o XML doc do enricher para o
    // porquê de ser global e não só no MessageTemplate do UseSerilogRequestLogging.
    .Enrich.With<Kura.Api.Extensions.RedigirRequestPathEnricher>()
    .WriteTo.Console()
    .WriteTo.File("logs/kura-api-.log", rollingInterval: RollingInterval.Day));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
// S3D-03: health checks reais (API + Oracle + Luna), substituindo o antigo
// HealthController (200 incondicional). Ver Extensions/HealthCheckExtensions.cs.
builder.Services.AddKuraHealthChecks(builder.Configuration);
// S3D-04: tracing entre camadas + métricas de desempenho (OpenTelemetry, exporter
// Console). Ver Extensions/ObservabilityExtensions.cs para a decisão de cobertura
// (por que EntityFrameworkCore/Prometheus ficaram de fora).
builder.Services.AddKuraObservability();
// CORS do app da clínica na web — ver Extensions/CorsExtensions.cs.
builder.Services.AddKuraCors(builder.Configuration);

// JWT
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key not configured.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
        };
    });
// FD-04: primeira politica de autorizacao deste backend. Ver
// Extensions/AuthorizationExtensions.cs para por que RequireClaim (e nao Roles=) e
// por que a politica falha FECHADA para token pre-FD-03, que nao tem a claim `perfil`.
builder.Services.AddKuraAuthorization();

// Swagger with JWT Bearer scheme
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "KURA API — Clyvo Vet", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe o token JWT no formato: Bearer {token}",
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", doc),
            new List<string>()
        }
    });
    // XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Validação de migrations pendentes — apenas AVISO, não aplica nada: o schema é
// responsabilidade do Flyway (MIGRATIONS_POLICY.md) e as migrations EF são só evidência.
//
// ⚠️ S3D-10 REESCREVEU ESTE COMENTÁRIO. A versão anterior descrevia, em detalhe, o
// comportamento de uma allowlist de códigos Oracle que NÃO EXISTE MAIS aqui — e várias das
// afirmações dela já eram falsas antes de a allowlist sair (ver abaixo). Deixá-la seria
// reproduzir o padrão "documentação que garante o que o código não faz", que é motivo de
// reprovação neste projeto.
//
// O que era FALSO na redação antiga, e vale registrar porque explica o Critical:
//   - ela afirmava que o retry "não executa" nos caminhos medidos, e tratava isso como
//     curiosidade. Não era curiosidade: era o defeito. Se o catch não casa, a exceção
//     ESCAPA e o processo MORRE. Medido na revisão do G4: HTTP 000 por 139s seguidos e
//     RestartCount=7, com o Oracle inalcançável na partida.
//   - ela creditava a convergência do cold start à restart policy do Docker. Verdade — mas
//     a razão é que a API estava em crash loop, não que o loop fosse inofensivo.
// Hoje a verificação vive em MigrationEvidenceExtensions e NUNCA lança. Ver o XML de lá.
//
// S3D-05: a chamada abaixo abre conexão real com o Oracle no startup, o que inviabiliza
// subir o host num teste de integração (WebApplicationFactory<Program>). Não é questão de
// lentidão: sem este guard de ambiente, a suíte encosta no Oracle de verdade — medido, 57
// linhas ORA- nascendo exatamente na linha do GetPendingMigrationsAsync.
// ⚠️ O guard só dispara se quem sobe o host pedir o ambiente explicitamente. O
// WebApplicationFactory usa "Development" por PADRÃO — a factory da suíte de integração
// PRECISA declarar UseEnvironment("Testing"), senão este bloco roda e carrega
// appsettings.Development.json.
// O que acontece DEPOIS disso depende da fábrica, e foi medido (G2/G2b da S3D-06):
//   - fábrica com o DbContext substituído por InMemory (a KuraApiFactory de hoje): o bloco
//     morre em InvalidOperationException "Relational-specific methods…", com 0 linhas ORA-.
//     Quem barra o Oracle nesse caso é a substituição, NÃO este guard.
//   - fábrica sem essa substituição: a linha do GetPendingMigrationsAsync abaixo abre
//     conexão de verdade — medido, 57 linhas ORA- nascendo exatamente nela. É só nesse
//     caso que este guard é a barreira.
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<KuraDbContext>();

        // S3D-10: a lógica saiu daqui para MigrationEvidenceExtensions e passou a ser NÃO-FATAL.
        // Antes, este bloco capturava apenas uma allowlist escrita à mão de 4 códigos Oracle
        // ([12514, 1109, 12541, 17002]) que, medida contra o ambiente real, acerta ZERO dos 3
        // modos de falha que de fato ocorrem (12154/12545 no stop, 50000 no pause). Consequência
        // medida: com o Oracle inalcançável NA PARTIDA a exceção escapava, o processo morria e,
        // com `restart: unless-stopped`, virava crash loop — HTTP 000 por 139s, RestartCount=7.
        // A API precisa SUBIR e reportar o banco por GET /health; processo morto não reporta nada.
        // Ver o XML da classe para o achado completo e para a aritmética das 5 tentativas.
        await MigrationEvidenceExtensions.RegistrarMigrationsPendentesAsync(
            async () => await context.Database.GetPendingMigrationsAsync(),
            app.Logger);
    }
}

// TASK-84: UseSerilogRequestLogging() precisa vir ANTES de ExceptionHandlerMiddleware
// (ordem, não nível — ver comentário completo em RequestPipelineExtensions).
app.UseRequestLoggingAndExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseKuraCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// S3D-03: substitui o antigo HealthController — não usar [AllowAnonymous] extra
// aqui, MapHealthChecks já não exige autenticação por padrão.
app.MapKuraHealthChecks("/health");

app.Run();

// S3D-05: declaração explícita para destravar WebApplicationFactory<Program> (S3D-06),
// que exige um tipo Program público, e precisa vir DEPOIS de todos os top-level statements.
// ⚠️ MEDIDO neste repo: em net10.0 o Program gerado a partir de top-level statements JÁ SAI
// public, e WebApplicationFactory<Program> compila COM e SEM esta linha. A receita clássica
// ("adicione isto porque o tipo seria internal") descreve TFMs anteriores; hoje é REDUNDANTE.
// O que governa é o TARGET FRAMEWORK, não a versão do SDK: com o MESMO SDK 10.0.203,
// net8.0 → NotPublic, net9.0 → NotPublic, net10.0 → Public. Mantida de propósito como
// fixação explícita da visibilidade, que sobrevive a um downgrade de TFM — mesmo raciocínio
// da guarda de null em KuraDbContext.ApplyTenantFilters.
// A linha é redundante sob REMOÇÃO, mas load-bearing sob mutação de acessibilidade: trocar
// "public" por "internal" aqui compila o Kura.Api normalmente e quebra só o consumidor, com
// CS0122 ("Program" é inacessível) — medido. Ou seja, o modificador escrito neste arquivo é
// quem governa; não é decoração.
// Alternativa descartada: InternalsVisibleTo (mais indireto, e sem precedente neste repo).
public partial class Program;
