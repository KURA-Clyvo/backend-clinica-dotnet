namespace Kura.IntegrationTests;

using Kura.Domain.Entities;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// S3D-06: fábrica de host para os testes de integração HTTP. Sobe o
/// <c>Program.cs</c> REAL do <c>Kura.Api</c> (toda a fiação de produção: Serilog,
/// autenticação JWT, FluentValidation, health checks, observabilidade, pipeline de
/// middlewares) e substitui apenas a persistência por EF InMemory.
///
/// <para>
/// 🔴 <b>Duas exigências que NÃO são estilo</b>, ambas medidas na revisão G2 da S3D-05
/// (<c>task-S3D-05-review.md</c> §2 e §3) e reconfirmadas por medição nesta task:
/// </para>
///
/// <para>
/// <b>(a) <c>UseEnvironment("Testing")</c> é obrigatório.</b> O
/// <see cref="WebApplicationFactory{TEntryPoint}"/> usa o ambiente <c>Development</c>
/// por PADRÃO (não <c>Testing</c>, não <c>Production</c>). Sem esta linha o guard que a
/// S3D-05 pôs no <c>Program.cs</c> não dispara e o bloco de validação de migrations roda
/// no startup, carregando <c>appsettings.Development.json</c> — que está versionado
/// apontando para <c>oracle.fiap.com.br</c> com uma conta institucional BLOQUEADA. Vale
/// também para o CI, que não define <c>ASPNETCORE_ENVIRONMENT</c>.
/// </para>
///
/// <para>
/// ⚠️ <b>Onde está a barreira de verdade — medido em 4 variantes no G2 e em mais 3 no
/// G2b, estas últimas incluindo a fábrica SEM substituição de <c>DbContext</c>, que é o
/// caso que o G2 não cobriu.</b> NESTA fábrica o bloco de startup <b>não chega a abrir
/// conexão Oracle</b>: ele morre antes com
/// <c>InvalidOperationException: Relational-specific methods…</c>, porque o
/// <c>DbContext</c> já foi substituído por InMemory. Ou seja, <b>quem impede discar para
/// a FIAP é a substituição do <c>DbContext</c>, não esta linha.</b> Não confie no
/// contrário: uma segunda fábrica que mantenha <c>UseEnvironment</c> e dispense a
/// substituição InMemory (p.ex. para testar contra um Oracle local) <b>não</b> está
/// protegida por esta linha — medido no G2b: 57 linhas <c>ORA-</c>
/// (<c>ORA-50201</c>/<c>ORA-12541</c> contra porta morta), nascendo em <c>Semear</c> se
/// <c>UseEnvironment</c> ficar, e em <c>Program.cs:129</c> se sair. A linha continua
/// obrigatória por isso e porque, <b>num processo sem <c>ASPNETCORE_ENVIRONMENT</c>
/// definido</b> (o CI), apagá-la deixa a suíte <b>19/19 vermelha</b>. Atenção ao limite
/// também medido: com <c>ASPNETCORE_ENVIRONMENT=Testing</c> exportado, apagá-la deixa a
/// suíte <b>19/19 verde</b>. Ver
/// <see cref="AmbienteEFiacaoDoHostTests"/>, onde o ambiente é asserido em teste para que
/// apagar a linha quebre a suíte em vez de degradar em silêncio.
/// </para>
///
/// <para>
/// <b>(b) A configuração entra por <see cref="IWebHostBuilder.UseSetting"/>, NUNCA por
/// <c>ConfigureAppConfiguration</c>.</b> No modelo de hosting mínimo
/// (<c>WebApplication.CreateBuilder</c>), os callbacks de <c>ConfigureAppConfiguration</c>
/// registrados via <c>ConfigureWebHost</c> rodam DEPOIS de os top-level statements já
/// terem lido <c>builder.Configuration</c> — medido: o host morre com
/// <c>Connection string 'DefaultConnection' not configured.</c>. As chaves abaixo são
/// exatamente os pontos de fail-fast do startup (<c>ServiceCollectionExtensions</c>,
/// <c>HealthCheckExtensions</c>, <c>Program.cs</c>) mais as duas usadas em runtime pelos
/// filtros de API key.
/// </para>
/// </summary>
public class KuraApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Ambiente declarado ao host. Ver nota (a) na documentação da classe.</summary>
    public const string NomeAmbiente = "Testing";

    public const string EmailClinica = "integracao@kura.test";
    public const string SenhaClinica = "SenhaDeIntegracao#2026";
    public const long IdClinicaSemeada = 1;
    public const long IdVeterinarioSemeado = 1;
    public const string NomeVeterinarioSemeado = "Dra. Integração";

    // G2 Important-1: SEGUNDO tenant, semeado só para dar o que vazar.
    // Com uma clínica só, a asserção "todo item veio da minha clínica" é logicamente
    // incapaz de falhar — não existe linha de outro tenant no banco. Medido na revisão:
    // removendo o query filter de Veterinario, e depois zerando IClinicaContext
    // (vazamento cross-tenant total em produção), a suíte inteira ficava VERDE.
    // Nenhum teste faz login neste tenant: ele existe exclusivamente como isca.
    public const long IdClinicaOutroTenant = 2;
    public const long IdVeterinarioOutroTenant = 2;
    public const string NomeVeterinarioOutroTenant = "Dr. Outro Tenant";

    // ── FD-03: usuários que só existem para provar comportamento novo ────────────────
    /// <summary>
    /// GESTOR sem <c>ID_VETERINARIO</c>, na clínica semeada. Existe para exercitar, sobre
    /// HTTP real, o caso que a heurística de fallback tornava impossível: login de quem não
    /// é veterinário. Nenhum outro teste usa este usuário.
    /// </summary>
    public const string EmailGestorPuro = "gestor-puro@kura.test";

    /// <summary>
    /// MESMO e-mail em duas clínicas — estado LEGAL do banco, porque a UK da V17 é
    /// <c>(ID_CLINICA, DS_EMAIL)</c>. Existe para provar que o login falha explicitamente em
    /// vez de escolher um tenant. Sem as DUAS linhas o cenário é logicamente incapaz de
    /// falhar.
    /// </summary>
    public const string EmailAmbiguo = "atende-nas-duas@kura.test";

    /// <summary>
    /// F1 da fix wave pós-G2. Usuário da clínica SEMEADA cujo <c>ID_VETERINARIO</c> aponta o
    /// veterinário do OUTRO tenant.
    ///
    /// <para>⚠️ Este não é um estado impossível que o teste inventou: a
    /// <c>FK_USUARIO_CLINICA_VET</c> da V17 referencia só <c>VETERINARIO(ID_VETERINARIO)</c>,
    /// <b>sem compor com <c>ID_CLINICA</c></b> — o Oracle aceita a linha. A única defesa é a
    /// guarda em <c>AuthService.ObterVeterinarioVinculadoAsync</c>.</para>
    /// </summary>
    public const string EmailVinculoCruzado = "vinculo-cruzado@kura.test";

    // -- FD-09: tabela de precos semeada nos DOIS tenants -----------------------------
    // A isca cross-tenant e OBRIGATORIA aqui: os DTOs desta task nao tem IdClinica, entao
    // nao existe caminho HTTP que crie um SERVICO_PRECO na clinica alheia. Sem esta linha
    // semeada, a asserta de IDOR ("id de outra clinica devolve 404") seria logicamente
    // incapaz de falhar - o 404 viria de a linha nao existir para ninguem, nao do escopo
    // de tenant. Com ela, remover o predicado de clinica do repositorio devolve 200.
    public const long IdServicoPrecoOutroTenant = 1;
    public const long IdServicoPrecoSemeado = 2;
    public const string NomeServicoPrecoSemeado = "Consulta de rotina (semeada)";
    public const decimal PrecoServicoPrecoSemeado = 180.50m;

    // -- FD-10: evento clinico semeado nos DOIS tenants ------------------------------
    // O evento e o recurso da ROTA do lancamento de cobranca
    // (POST /api/v1/eventos-clinicos/{id}/cobrancas), entao a isca cross-tenant e
    // OBRIGATORIA aqui pelo mesmo motivo da FD-09: sem um evento REAL na clinica alheia,
    // a asserta "evento de outra clinica devolve 404" seria logicamente incapaz de falhar
    // -- o 404 viria de a linha nao existir para ninguem, nao do escopo de tenant. Com
    // ela, remover a comparacao de clinica de EventoClinicoRepository devolve 201.
    public const long IdEventoClinicoSemeado = 1;
    public const long IdEventoClinicoOutroTenant = 2;

    // -- F5 da fix wave 2: SEGUNDO evento da MESMA clinica semeada --------------------
    // Isolamento entre EVENTOS, que e uma pergunta diferente de isolamento entre
    // CLINICAS. A isca do tenant 2 (IdCobrancaOutroTenant) nao alcanca este caso: ela
    // prova que a listagem nao cruza a fronteira de clinica, e nada diz sobre a listagem
    // de um atendimento trazer a conta de OUTRO atendimento da mesma clinica -- que e o
    // que acontece se o predicado de evento do repositorio cair. Medido: com
    // `c.IdEventoClinico == idEventoClinico` neutralizado, a suite ficava 583/583 VERDE.
    public const long IdSegundoEventoClinicoSemeado = 3;

    // -- F3 da revisao G2 da FD-10: COBRANCA semeada no OUTRO tenant ------------------
    // 🔴 Faltava, e a lacuna era mensuravel: a mutacao do predicado de tenant em
    // CobrancaRepository mordia so nas suites unitarias e a suite HTTP ficava VERDE,
    // porque nao existia nenhuma cobranca alheia para vazar. E a mesma licao da FD-09 --
    // *a isca do outro tenant precisa EXISTIR, senao o teste e vacuo*. Nenhum teste faz
    // login na clinica 2; esta linha existe exclusivamente como isca de IDOR.
    public const long IdCobrancaOutroTenant = 1;
    public const decimal ValorCobrancaOutroTenant = 777.77m;

    /// <summary>
    /// 🔴 F6 da fix wave pos-G2 da FD-11: a data da isca e FIXA, nao `UtcNow.AddDays(-1)`.
    ///
    /// <para>O teste de IDOR do resumo financeiro precisa de uma JANELA que contenha a isca —
    /// senao ele passa por VACUO, provando "nao vazou" sobre um periodo em que a isca nem
    /// estava. Com a isca carimbada na CONSTRUCAO da factory e a janela calculada com o
    /// `UtcNow` DO TESTE, uma execucao que cruzasse a meia-noite UTC jogava a isca para fora
    /// da janela e o teste ficava verde sem ter medido nada — exatamente o modo de falha que
    /// ele existe para impedir. Janela de risco pequena, consequencia silenciosa.</para>
    ///
    /// <para>Data fixa remove a corrida inteira: a isca e a janela saem da MESMA referencia
    /// temporal, que agora e uma constante. O ano 2023 nao e usado por nenhum outro cenario
    /// de `FinanceiroResumoHttpTests` (que isola cada teste num ano proprio, porque o resumo
    /// e agregado e o banco InMemory e compartilhado pela IClassFixture).</para>
    /// </summary>
    public static readonly DateTime DtCobrancaOutroTenant =
        new(2023, 4, 12, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>Chave HMAC do JWT. &gt;= 32 bytes, exigência do <c>SymmetricSecurityKey</c>.</summary>
    public const string ChaveJwt = "chave-de-integracao-s3d06-com-mais-de-32-bytes";
    public const string EmissorJwt = "kura-api";
    public const string AudienciaJwt = "kura-client";

    /// <summary>
    /// Connection string INERTE. Aponta para uma porta local morta, nunca para a FIAP.
    /// O <c>DbContext</c> real é substituído por InMemory em
    /// <see cref="ConfigureWebHost"/>, então este valor nunca é usado para abrir conexão —
    /// ele existe só para satisfazer o fail-fast de <c>AddInfrastructure</c>, que lê a
    /// configuração antes de qualquer substituição de serviço ser possível.
    /// </summary>
    public const string ConexaoInerte =
        "User Id=integracao;Password=integracao;Data Source=127.0.0.1:9999/inexistente";

    /// <summary>Base URL inerte da Luna — porta local morta, mesmo raciocínio da conexão.</summary>
    public const string UrlLunaInerte = "http://127.0.0.1:9999/";

    // Banco InMemory exclusivo desta instância de fábrica: duas classes de teste que
    // compartilhem a mesma instância (IClassFixture) veem o mesmo banco; instâncias
    // diferentes ficam isoladas.
    private readonly string _nomeBancoInMemory = $"kura-integration-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // (a) — ver documentação da classe. Não remover.
        builder.UseEnvironment(NomeAmbiente);

        // (b) — ver documentação da classe. UseSetting, não ConfigureAppConfiguration.
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConexaoInerte);
        builder.UseSetting("Jwt:Key", ChaveJwt);
        builder.UseSetting("Jwt:Issuer", EmissorJwt);
        builder.UseSetting("Jwt:Audience", AudienciaJwt);
        builder.UseSetting("Daily:ApiKey", "daily-api-key-de-integracao");
        builder.UseSetting("Luna:BaseUrl", UrlLunaInerte);
        builder.UseSetting("Luna:InboundApiKey", "luna-inbound-de-integracao");
        // Estas duas são lidas em runtime pelos filtros de API key (LunaApiKeyAuthFilter /
        // ApiKeyAuthFilter), não no startup. Declaradas aqui para que os endpoints da Luna
        // e de IoT não morram com InvalidOperationException se algum teste os exercitar.
        builder.UseSetting("Luna:ApiKey", "luna-api-key-de-integracao");
        builder.UseSetting("IoT:ApiKey", "iot-api-key-de-integracao");
        // FT-02 (backlog KURA_BACKLOG_FOTO_PET.md): AddInfrastructure passou a exigir
        // Foto:UrlSecret com >= 32 bytes UTF-8, fail-fast na partida — sem esta linha a
        // fábrica inteira morre com InvalidOperationException antes de qualquer teste rodar.
        builder.UseSetting("Foto:UrlSecret", "chave-de-integracao-ft02-com-mais-de-32-bytes");

        builder.ConfigureServices(services =>
        {
            // Remove TODO registro ligado ao KuraDbContext feito por AddInfrastructure
            // (o provider Oracle inclusive). O predicado por argumento genérico pega
            // também IDbContextOptionsConfiguration<KuraDbContext>, introduzido no EF 9 —
            // filtrar só por DbContextOptions<KuraDbContext> deixaria a configuração do
            // Oracle viva e ela voltaria a ser aplicada por cima do InMemory.
            var registrosDoContexto = services
                .Where(d => d.ServiceType == typeof(KuraDbContext)
                         || d.ServiceType == typeof(DbContextOptions)
                         || (d.ServiceType.IsGenericType
                             && d.ServiceType.GetGenericArguments().Contains(typeof(KuraDbContext))))
                .ToList();

            foreach (var registro in registrosDoContexto)
                services.Remove(registro);

            services.AddDbContext<KuraDbContext>(options =>
            {
                options.UseInMemoryDatabase(_nomeBancoInMemory);
                // Mantido para ficar fiel à produção: o interceptor é quem barra escrita
                // nas tabelas cuja autoridade é o backend Java (CONTA_TUTOR, CONSENTIMENTO).
                options.AddInterceptors(new ReadOnlyTablesInterceptor());

                // 🔴 FIX WAVE PÓS-G2 (F3). Sem esta linha, TODO endpoint que abra transação
                // explícita é INALCANÇÁVEL nesta suíte — e isso não é teoria, é medido:
                // POST /api/v1/auth/register-clinica devolvia 500 com
                //   "An error was generated for warning
                //    'Microsoft.EntityFrameworkCore.Database.Transaction.TransactionIgnoredWarning':
                //    Transactions are not supported by the in-memory store."
                // O provider InMemory promove esse aviso a EXCEÇÃO por padrão, então
                // AuthService.RegisterClinicaAsync morria em BeginTransactionAsync antes de
                // gravar qualquer coisa. Consequência prática: o fluxo que seed-demo.sh
                // exercita de verdade (registro -> login) nunca tinha sido coberto por HTTP
                // neste repo — a lacuna é anterior à FD-03.
                //
                // ⚠️ O QUE ISTO CUSTA, declarado: com o aviso rebaixado, begin/commit/rollback
                // viram NO-OP. Esta suíte passa a provar a ORQUESTRAÇÃO e o resultado HTTP,
                // NUNCA a atomicidade — um rollback que devesse desfazer escritas não desfaz
                // nada aqui. A prova de atomicidade continua onde sempre esteve:
                // AuthServiceTransacaoTests, com fakes que implementam o snapshot à mão (ver a
                // docstring daquela classe), e, no fim, contra Oracle real no gate do ciclo.
                options.ConfigureWarnings(w =>
                    w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            });
        });
    }

    /// <summary>
    /// Semeia clínica + veterinário assim que o host é construído. O seed vive aqui, e não
    /// num script externo, de propósito: a suíte precisa ser autocontida (o CI roda
    /// <c>dotnet test</c> sem Oracle, sem <c>seed-demo.sh</c> e sem compose).
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        Semear(host.Services);
        return host;
    }

    private static void Semear(IServiceProvider services)
    {
        using var escopo = services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();

        // Sem HttpContext neste escopo, IClinicaContext.IdClinicaFiltro é null e os query
        // filters de tenant desligam inteiros — é o que permite semear sem JWT.
        if (db.Clinicas.Any())
            return;

        db.Clinicas.Add(new Clinica
        {
            Id = IdClinicaSemeada,
            NmClinica = "Clínica de Integração",
            NrCnpj = "00000000000191",
            NmRazaoSocial = "Clínica de Integração LTDA",
            DsEndereco = "Rua dos Testes, 100",
            NmCidade = "São Paulo",
            SgUf = "SP",
            NrCep = "01001000",
            NrTelefone = "1130000000",
            DsEmail = EmailClinica,
            DsEmailAcesso = EmailClinica,
            // Hash real: o login de produção valida com BCrypt.Verify. Semear a senha em
            // texto puro faria o cenário de login passar por um caminho que não existe.
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(SenhaClinica),
            StAtiva = true,
            DtCadastro = DateTime.UtcNow,
        });

        db.Veterinarios.Add(new Veterinario
        {
            Id = IdVeterinarioSemeado,
            IdClinica = IdClinicaSemeada,
            NmVeterinario = NomeVeterinarioSemeado,
            NrCrmv = "SP-99999",
            // ⚠️ FD-03: até esta task, este e-mail ser igual ao da clínica NÃO era detalhe —
            // era o primeiro ramo da heurística de fallback de LoginAsync, que escolhia "o
            // veterinário logado" batendo VETERINARIO.DS_EMAIL com CLINICA.DS_EMAIL_ACESSO.
            // A heurística morreu; hoje o vínculo é explícito em
            // USUARIO_CLINICA.ID_VETERINARIO (semeado mais abaixo). O e-mail continua igual
            // só por fidelidade ao que o registro em runtime grava.
            DsEmail = EmailClinica,
            NrTelefone = "11999990000",
            StAtiva = true,
        });

        // Segundo tenant — ver o comentário nas constantes. Sem esta clínica, a asserção
        // de escopo em FluxoDeNegocioHttpTests não tem como falhar.
        db.Clinicas.Add(new Clinica
        {
            Id = IdClinicaOutroTenant,
            NmClinica = "Clínica do Outro Tenant",
            NrCnpj = "00000000000272",
            NmRazaoSocial = "Clínica do Outro Tenant LTDA",
            DsEndereco = "Rua do Vazamento, 200",
            NmCidade = "São Paulo",
            SgUf = "SP",
            NrCep = "01002000",
            NrTelefone = "1130000001",
            DsEmail = "outro-tenant@kura.test",
            DsEmailAcesso = "outro-tenant@kura.test",
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(SenhaClinica),
            StAtiva = true,
            DtCadastro = DateTime.UtcNow,
        });

        db.Veterinarios.Add(new Veterinario
        {
            Id = IdVeterinarioOutroTenant,
            IdClinica = IdClinicaOutroTenant,
            NmVeterinario = NomeVeterinarioOutroTenant,
            NrCrmv = "SP-88888",
            DsEmail = "outro-tenant@kura.test",
            NrTelefone = "11988880000",
            StAtiva = true,
        });

        // 🔴 FD-03: a partir daqui quem autentica é USUARIO_CLINICA, não CLINICA. Sem estas
        // duas linhas o login HTTP de toda a suíte de integração devolve 422 — e é
        // exatamente esse o ponto do escopo de runtime da task: a credencial existir em
        // CLINICA deixou de bastar. Elas são, para a suíte, o equivalente do que
        // AuthService.RegisterClinicaAsync faz em produção e do que a conversão da V17 faz
        // para base já existente.
        //
        // ID_VETERINARIO preenchido de propósito: espelha o vínculo que o registro em runtime
        // cria (veterinário e usuário nascem juntos) e é o que mantém `usuario` não nulo na
        // resposta de login — o cenário que os testes HTTP asserem.
        db.UsuariosClinica.Add(new UsuarioClinica
        {
            Id = 1,
            IdClinica = IdClinicaSemeada,
            IdVeterinario = IdVeterinarioSemeado,
            DsEmail = EmailClinica,
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(SenhaClinica),
            TpPerfil = PerfisUsuarioClinica.Veterinario,
            StAtiva = true,
        });

        // O segundo tenant também ganha usuário — pelo mesmo motivo que ganhou clínica e
        // veterinário: sem linha do outro tenant, nenhuma asserção de escopo tem como falhar.
        db.UsuariosClinica.Add(new UsuarioClinica
        {
            Id = 2,
            IdClinica = IdClinicaOutroTenant,
            IdVeterinario = IdVeterinarioOutroTenant,
            DsEmail = "outro-tenant@kura.test",
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(SenhaClinica),
            TpPerfil = PerfisUsuarioClinica.Veterinario,
            StAtiva = true,
        });

        // GESTOR PURO — sem ID_VETERINARIO. Ver a constante EmailGestorPuro.
        db.UsuariosClinica.Add(new UsuarioClinica
        {
            Id = 3,
            IdClinica = IdClinicaSemeada,
            IdVeterinario = null,
            DsEmail = EmailGestorPuro,
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(SenhaClinica),
            TpPerfil = PerfisUsuarioClinica.Gestor,
            StAtiva = true,
        });

        // VÍNCULO CRUZADO: usuário da clínica 1 apontando o veterinário da clínica 2.
        // Ver a constante EmailVinculoCruzado.
        db.UsuariosClinica.Add(new UsuarioClinica
        {
            Id = 6,
            IdClinica = IdClinicaSemeada,
            IdVeterinario = IdVeterinarioOutroTenant,
            DsEmail = EmailVinculoCruzado,
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(SenhaClinica),
            TpPerfil = PerfisUsuarioClinica.Veterinario,
            StAtiva = true,
        });

        // MESMO e-mail nas duas clínicas. Ver a constante EmailAmbiguo.
        db.UsuariosClinica.Add(new UsuarioClinica
        {
            Id = 4,
            IdClinica = IdClinicaSemeada,
            IdVeterinario = null,
            DsEmail = EmailAmbiguo,
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(SenhaClinica),
            TpPerfil = PerfisUsuarioClinica.Gestor,
            StAtiva = true,
        });
        db.UsuariosClinica.Add(new UsuarioClinica
        {
            Id = 5,
            IdClinica = IdClinicaOutroTenant,
            IdVeterinario = null,
            DsEmail = EmailAmbiguo,
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(SenhaClinica),
            TpPerfil = PerfisUsuarioClinica.Gestor,
            StAtiva = true,
        });

        // FD-09 - ver as constantes IdServicoPreco*. A linha do OUTRO tenant existe
        // exclusivamente como isca de IDOR; nenhum teste faz login naquela clinica.
        db.ServicosPreco.Add(new ServicoPreco
        {
            Id = IdServicoPrecoOutroTenant,
            IdClinica = IdClinicaOutroTenant,
            NmServico = "Consulta do Outro Tenant",
            VlPreco = 999.99m,
            StAtiva = true,
        });

        db.ServicosPreco.Add(new ServicoPreco
        {
            Id = IdServicoPrecoSemeado,
            IdClinica = IdClinicaSemeada,
            NmServico = NomeServicoPrecoSemeado,
            VlPreco = PrecoServicoPrecoSemeado,
            StAtiva = true,
        });

        // FD-10 - ver as constantes IdEventoClinico*. O evento do OUTRO tenant existe
        // exclusivamente como isca de IDOR; nenhum teste faz login naquela clinica.
        db.EventosClinicos.Add(new EventoClinico
        {
            Id = IdEventoClinicoSemeado,
            IdClinica = IdClinicaSemeada,
            IdPet = 1,
            IdVeterinario = IdVeterinarioSemeado,
            IdTipoEvento = 1,
            DtEvento = DateTime.UtcNow.AddDays(-1),
            DsObservacao = "Atendimento semeado para o lancamento de cobranca",
            StAtiva = true,
        });

        db.EventosClinicos.Add(new EventoClinico
        {
            Id = IdEventoClinicoOutroTenant,
            IdClinica = IdClinicaOutroTenant,
            IdPet = 2,
            IdVeterinario = IdVeterinarioOutroTenant,
            IdTipoEvento = 1,
            DtEvento = DateTime.UtcNow.AddDays(-1),
            DsObservacao = "Atendimento do outro tenant (isca)",
            StAtiva = true,
        });

        // F5 - segundo atendimento da MESMA clinica. Ver a constante.
        db.EventosClinicos.Add(new EventoClinico
        {
            Id = IdSegundoEventoClinicoSemeado,
            IdClinica = IdClinicaSemeada,
            IdPet = 3,
            IdVeterinario = IdVeterinarioSemeado,
            IdTipoEvento = 1,
            DtEvento = DateTime.UtcNow.AddDays(-2),
            DsObservacao = "Segundo atendimento da clinica semeada (outro pet)",
            StAtiva = true,
        });

        // F3 - ver a constante IdCobrancaOutroTenant. Isca de IDOR do lado financeiro.
        db.Cobrancas.Add(new Cobranca
        {
            Id = IdCobrancaOutroTenant,
            IdEventoClinico = IdEventoClinicoOutroTenant,
            IdClinica = IdClinicaOutroTenant,
            IdServicoPreco = IdServicoPrecoOutroTenant,
            VlCobrado = ValorCobrancaOutroTenant,
            DsFormaPagamento = "DINHEIRO",
            DtCobranca = DtCobrancaOutroTenant,
            StAtiva = true,
        });

        db.SaveChanges();
    }
}
