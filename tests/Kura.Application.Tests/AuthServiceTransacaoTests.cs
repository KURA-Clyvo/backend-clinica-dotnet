namespace Kura.Application.Tests;

using System.Linq.Expressions;
using FluentAssertions;
using Kura.Application.DTOs.Auth;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

/// <summary>
/// TASK-30: <c>AuthService.RegisterClinicaAsync</c> fazia duas escritas
/// sequenciais (Clinica, depois Veterinario). Se a segunda falhasse, a Clinica
/// já gravada na primeira ficava órfã (sem veterinário) — e
/// <c>LoginAsync</c> lança <c>RegraDeNegocioException</c> para clínica sem
/// veterinário, tornando a conta permanentemente inutilizável, com o e-mail
/// já "tomado" para um novo cadastro.
///
/// IMPORTANTE — o que este teste prova e o que NÃO prova:
/// Os duplos abaixo (<see cref="FakeClinicaRepository"/>,
/// <see cref="FakeVeterinarioRepository"/>, <see cref="FakeUnitOfWork"/>) são
/// listas em memória com um snapshot manual de "linhas gravadas desde o
/// início da transação", removidas em caso de rollback — isso imita o
/// comportamento de uma transação relacional real, mas não é uma. O provider
/// EF Core InMemory usado nos demais testes deste projeto (e em
/// Kura.Infrastructure.Tests) não suporta transações relacionais de verdade:
/// <c>Database.BeginTransactionAsync</c> lança <c>InvalidOperationException</c>
/// nele, então não dá para exercitar o <c>UnitOfWork</c> real (que chama
/// <c>_context.Database.BeginTransactionAsync()</c>) contra InMemory. Este
/// teste prova a ORQUESTRAÇÃO do <c>AuthService</c> — que ele abre a
/// transação, tenta as duas escritas e faz rollback explícito quando a
/// segunda falha, sem deixar a primeira "vazar" logicamente. A garantia de
/// atomicidade FÍSICA do banco (que um ROLLBACK real desfaz o INSERT no
/// disco) só é exercida contra o Oracle real (produção / DevOps-Cloud).
/// </summary>
public class AuthServiceTransacaoTests
{
    private static IConfiguration BuildConfig()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "supersecretkey12345678901234567890123456789012",
            ["Jwt:Issuer"] = "kura-api",
            ["Jwt:Audience"] = "kura-client",
            ["Jwt:ExpiryHours"] = "8"
        };
        return new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
    }

    private static RegisterClinicaDto BuildDto() => new()
    {
        NmClinica = "Clínica Teste",
        NrCnpj = "12.345.678/0001-99",
        DsEndereco = "Rua A, 1",
        NrTelefone = "(11) 99999-9999",
        DsEmail = "contato@teste.com",
        DsEmailAcesso = "admin@teste.com",
        DsSenha = "Senha@2026",
        NmVeterinarioAdmin = "Dr. Admin",
        NrCRMV = "SP-000111"
    };

    [Fact]
    public async Task RegisterClinicaAsync_FalhaAoGravarVeterinario_NaoDeixaClinicaOrfaERetryFunciona()
    {
        var clinicaStore = new List<Clinica>();
        var veterinarioStore = new List<Veterinario>();
        var usuarioStore = new List<UsuarioClinica>();

        var clinicaRepo = new FakeClinicaRepository(clinicaStore);
        var veterinarioRepo = new FakeVeterinarioRepository(veterinarioStore) { DeveLancarNoProximoAdd = true };
        var usuarioRepo = new FakeUsuarioClinicaRepository(usuarioStore);
        var uow = new FakeUnitOfWork(clinicaStore, veterinarioStore, usuarioStore);
        var sut = new AuthService(clinicaRepo, veterinarioRepo, usuarioRepo, uow, BuildConfig());

        var dto = BuildDto();

        // 1) Falha simulada na criação do Veterinario.
        var act = () => sut.RegisterClinicaAsync(dto);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Critério de aceitação: nenhuma Clinica órfã persistida após a falha.
        clinicaStore.Should().BeEmpty(
            "o rollback da transação deve desfazer o INSERT da Clinica quando o Veterinario falha — " +
            "sem isso, a clínica fica órfã e o e-mail fica permanentemente 'tomado'");
        veterinarioStore.Should().BeEmpty();
        usuarioStore.Should().BeEmpty();

        // 2) Retry com o mesmo e-mail deve funcionar após o rollback.
        var resultado = await sut.RegisterClinicaAsync(dto);

        resultado.Should().NotBeNull();
        resultado.DsEmailAcesso.Should().Be("admin@teste.com");
        clinicaStore.Should().HaveCount(1);
        veterinarioStore.Should().HaveCount(1);
        usuarioStore.Should().HaveCount(1);
    }

    /// <summary>
    /// 🔴 <b>FD-03 — prova de mordida da TERCEIRA escrita.</b> O <c>USUARIO_CLINICA</c> gestor
    /// entra na MESMA transação que a clínica e o veterinário. Se ele falhar, o rollback tem
    /// que desfazer os dois anteriores: a partir da FD-03 uma clínica sem usuário é tão
    /// inutilizável quanto a clínica órfã que a TASK-30 evitou — o login não tem mais como
    /// autenticar contra <c>CLINICA</c> —, e com o e-mail e o CNPJ igualmente "tomados".
    ///
    /// <para><b>Controle positivo:</b> este teste é impossível de escrever contra o código
    /// antigo (não havia terceira escrita), e falha se alguém mover a criação do usuário para
    /// DEPOIS de <c>CommitTransactionAsync</c> — nesse arranjo a clínica sobreviveria à
    /// falha.</para>
    /// </summary>
    [Fact]
    public async Task RegisterClinicaAsync_FalhaAoGravarUsuarioClinica_DesfazClinicaEVeterinario()
    {
        var clinicaStore = new List<Clinica>();
        var veterinarioStore = new List<Veterinario>();
        var usuarioStore = new List<UsuarioClinica>();

        var clinicaRepo = new FakeClinicaRepository(clinicaStore);
        var veterinarioRepo = new FakeVeterinarioRepository(veterinarioStore);
        var usuarioRepo = new FakeUsuarioClinicaRepository(usuarioStore) { DeveLancarNoProximoAdd = true };
        var uow = new FakeUnitOfWork(clinicaStore, veterinarioStore, usuarioStore);
        var sut = new AuthService(clinicaRepo, veterinarioRepo, usuarioRepo, uow, BuildConfig());

        var act = () => sut.RegisterClinicaAsync(BuildDto());
        await act.Should().ThrowAsync<InvalidOperationException>();

        clinicaStore.Should().BeEmpty(
            "clínica sem USUARIO_CLINICA não consegue mais logar — deixá-la gravada é o mesmo " +
            "defeito da clínica órfã da TASK-30, por outro caminho");
        veterinarioStore.Should().BeEmpty();
        usuarioStore.Should().BeEmpty();

        // Retry limpo depois do rollback.
        var resultado = await sut.RegisterClinicaAsync(BuildDto());
        resultado.DsEmailAcesso.Should().Be("admin@teste.com");
        usuarioStore.Should().ContainSingle()
            .Which.TpPerfil.Should().Be(PerfisUsuarioClinica.Gestor);
    }

    // ── Fakes ────────────────────────────────────────────────────────────────
    // Duplos "de verdade" (não Moq) para poder inspecionar o estado persistido
    // após a falha, em vez de só verificar chamadas de método.

    private sealed class FakeClinicaRepository : IClinicaRepository
    {
        private readonly List<Clinica> _store;

        public FakeClinicaRepository(List<Clinica> store) => _store = store;

        public Task<bool> ExisteComCnpjAsync(string cnpj) =>
            Task.FromResult(_store.Any(c => c.NrCnpj == cnpj));

        public Task<bool> ExisteComEmailAcessoAsync(string email) =>
            Task.FromResult(_store.Any(c => c.DsEmailAcesso == email));

        public Task<Clinica?> GetByIdAsync(long id) =>
            Task.FromResult(_store.FirstOrDefault(c => c.Id == id));

        public Task<IEnumerable<Clinica>> GetAllAsync() =>
            Task.FromResult<IEnumerable<Clinica>>(_store.ToList());

        public Task<IEnumerable<Clinica>> FindAsync(Expression<Func<Clinica, bool>> predicate) =>
            Task.FromResult(_store.AsQueryable().Where(predicate).AsEnumerable());

        public Task AddAsync(Clinica entity)
        {
            // Simula o INSERT físico com geração de PK via sequence: o registro
            // passa a "existir" imediatamente (visível dentro da transação em
            // aberto), só deixa de existir se houver ROLLBACK depois.
            entity.Id = _store.Count + 1;
            _store.Add(entity);
            return Task.CompletedTask;
        }

        public void Update(Clinica entity) { }

        public void SoftDelete(Clinica entity) => entity.StAtiva = false;
    }

    private sealed class FakeVeterinarioRepository : IVeterinarioRepository
    {
        private readonly List<Veterinario> _store;

        public FakeVeterinarioRepository(List<Veterinario> store) => _store = store;

        /// <summary>Quando true, a próxima chamada a AddAsync lança e se auto-reseta (simula falha pontual).</summary>
        public bool DeveLancarNoProximoAdd { get; set; }

        public Task<IEnumerable<Veterinario>> GetAllByClinicaIdAsync(long idClinica) =>
            Task.FromResult(_store.Where(v => v.IdClinica == idClinica));

        public Task<Veterinario?> GetByIdAsync(long id) =>
            Task.FromResult(_store.FirstOrDefault(v => v.Id == id));

        public Task<IEnumerable<Veterinario>> GetAllAsync() =>
            Task.FromResult<IEnumerable<Veterinario>>(_store.ToList());

        public Task<IEnumerable<Veterinario>> FindAsync(Expression<Func<Veterinario, bool>> predicate) =>
            Task.FromResult(_store.AsQueryable().Where(predicate).AsEnumerable());

        public Task AddAsync(Veterinario entity)
        {
            if (DeveLancarNoProximoAdd)
            {
                DeveLancarNoProximoAdd = false;
                throw new InvalidOperationException(
                    "Falha simulada ao gravar o veterinário (ex.: violação de constraint, timeout de conexão).");
            }

            entity.Id = _store.Count + 1;
            _store.Add(entity);
            return Task.CompletedTask;
        }

        public void Update(Veterinario entity) { }

        public void SoftDelete(Veterinario entity) => entity.StAtiva = false;

        // FD-04: este fake nao tem query filter nenhum, entao "ignorando filtros" e o mesmo
        // que GetByIdAsync aqui. Implementado so para satisfazer a interface -- nenhum teste
        // desta classe exercita o caminho de vinculo de veterinario.
        public Task<Veterinario?> BuscarPorIdIgnorandoFiltrosAsync(long id) =>
            Task.FromResult(_store.FirstOrDefault(v => v.Id == id));

        // REC-10: idem -- fake sem filtro nenhum, so para satisfazer a interface.
        public Task<Veterinario?> GetByIdAsync(long id, long idClinica) =>
            Task.FromResult(_store.FirstOrDefault(v => v.Id == id && v.IdClinica == idClinica));
    }

    /// <summary>
    /// FD-03: terceiro store da transação de registro. Mesmo desenho dos outros dois.
    /// </summary>
    private sealed class FakeUsuarioClinicaRepository : IUsuarioClinicaRepository
    {
        private readonly List<UsuarioClinica> _store;

        public FakeUsuarioClinicaRepository(List<UsuarioClinica> store) => _store = store;

        /// <summary>Quando true, a próxima chamada a AddAsync lança e se auto-reseta.</summary>
        public bool DeveLancarNoProximoAdd { get; set; }

        public Task<IReadOnlyList<UsuarioClinica>> BuscarAtivosPorEmailAsync(string email) =>
            Task.FromResult<IReadOnlyList<UsuarioClinica>>(
                _store.Where(u => u.DsEmail == email && u.StAtiva).OrderBy(u => u.IdClinica).ToList());

        public Task<UsuarioClinica?> GetByIdAsync(long id) =>
            Task.FromResult(_store.FirstOrDefault(u => u.Id == id));

        // ── FD-04 ────────────────────────────────────────────────────────────────────────
        // Membros novos da interface, implementados sobre o mesmo _store. NENHUM teste desta
        // classe os exercita: o CRUD da FD-04 e provado contra o repositorio REAL sobre
        // KuraDbContext InMemory em UsuarioClinicaServiceTests, justamente para nao trocar a
        // prova do predicado de tenant por uma reimplementacao de fake.
        public Task<IReadOnlyList<UsuarioClinica>> ListarDaClinicaAsync(
            long idClinica, bool incluirInativos = false) =>
            Task.FromResult<IReadOnlyList<UsuarioClinica>>(
                _store.Where(u => u.IdClinica == idClinica && (incluirInativos || u.StAtiva))
                      .OrderBy(u => u.DsEmail).ToList());

        public Task<UsuarioClinica?> BuscarPorIdNaClinicaAsync(long id, long idClinica) =>
            Task.FromResult(_store.FirstOrDefault(u => u.Id == id && u.IdClinica == idClinica));

        public Task<UsuarioClinica?> BuscarPorEmailNaClinicaAsync(
            long idClinica, string email, long? excetoId = null) =>
            Task.FromResult(
                _store.FirstOrDefault(u => u.IdClinica == idClinica
                                        && u.DsEmail == email
                                        && (excetoId == null || u.Id != excetoId)));

        public Task<int> ContarGestoresAtivosAsync(long idClinica, long? excetoId = null) =>
            Task.FromResult(_store.Count(u => u.IdClinica == idClinica
                                           && u.StAtiva
                                           && u.TpPerfil == PerfisUsuarioClinica.Gestor
                                           && (excetoId == null || u.Id != excetoId)));

        /// <summary>
        /// FD-13 — no-op: uma lista em memória não tem lock de linha para adquirir. Esta
        /// classe fake existe para os testes de TRANSAÇÃO do <c>AuthService</c>, que nem
        /// chegam ao invariante do último gestor.
        /// </summary>
        public Task BloquearGestoresAtivosAsync(long idClinica) => Task.CompletedTask;

        public Task<IEnumerable<UsuarioClinica>> GetAllAsync() =>
            Task.FromResult<IEnumerable<UsuarioClinica>>(_store.ToList());

        public Task<IEnumerable<UsuarioClinica>> FindAsync(Expression<Func<UsuarioClinica, bool>> predicate) =>
            Task.FromResult(_store.AsQueryable().Where(predicate).AsEnumerable());

        public Task AddAsync(UsuarioClinica entity)
        {
            if (DeveLancarNoProximoAdd)
            {
                DeveLancarNoProximoAdd = false;
                throw new InvalidOperationException(
                    "Falha simulada ao gravar o usuário da clínica (ex.: violação de UK, timeout).");
            }

            entity.Id = _store.Count + 1;
            _store.Add(entity);
            return Task.CompletedTask;
        }

        public void Update(UsuarioClinica entity) { }

        public void SoftDelete(UsuarioClinica entity) => entity.StAtiva = false;
    }

    /// <summary>
    /// Imita begin/commit/rollback de transação sobre listas em memória
    /// compartilhadas com os fakes de repositório: guarda o tamanho das
    /// listas no início da transação e, em caso de rollback, trunca de volta
    /// a esse tamanho — descartando tudo que foi "inserido" durante a
    /// transação. Não é uma transação de banco real (ver docstring da classe
    /// de teste).
    /// </summary>
    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        private readonly List<Clinica> _clinicaStore;
        private readonly List<Veterinario> _veterinarioStore;
        private readonly List<UsuarioClinica> _usuarioStore;
        private int _clinicaSnapshot;
        private int _veterinarioSnapshot;
        private int _usuarioSnapshot;

        public FakeUnitOfWork(
            List<Clinica> clinicaStore,
            List<Veterinario> veterinarioStore,
            List<UsuarioClinica> usuarioStore)
        {
            _clinicaStore = clinicaStore;
            _veterinarioStore = veterinarioStore;
            _usuarioStore = usuarioStore;
        }

        public Task<int> CommitAsync() => Task.FromResult(1);

        public Task BeginTransactionAsync()
        {
            _clinicaSnapshot = _clinicaStore.Count;
            _veterinarioSnapshot = _veterinarioStore.Count;
            _usuarioSnapshot = _usuarioStore.Count;
            return Task.CompletedTask;
        }

        /// <summary>
        /// FD-13 — devolve <c>true</c> porque este fake IMITA transação de verdade (snapshot +
        /// truncate no rollback). Devolver <c>false</c> aqui faria os chamadores pularem o
        /// commit/rollback e o fake deixaria de exercitar o que ele existe para exercitar.
        /// </summary>
        public async Task<bool> TryBeginTransactionAsync()
        {
            await BeginTransactionAsync();
            return true;
        }

        public Task CommitTransactionAsync() => Task.CompletedTask;

        public Task RollbackTransactionAsync()
        {
            if (_clinicaStore.Count > _clinicaSnapshot)
                _clinicaStore.RemoveRange(_clinicaSnapshot, _clinicaStore.Count - _clinicaSnapshot);

            if (_veterinarioStore.Count > _veterinarioSnapshot)
                _veterinarioStore.RemoveRange(_veterinarioSnapshot, _veterinarioStore.Count - _veterinarioSnapshot);

            if (_usuarioStore.Count > _usuarioSnapshot)
                _usuarioStore.RemoveRange(_usuarioSnapshot, _usuarioStore.Count - _usuarioSnapshot);

            return Task.CompletedTask;
        }

        public void Dispose() { }
    }
}
