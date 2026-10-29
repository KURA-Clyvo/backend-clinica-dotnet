# KURA API — Backend Clínica

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![Oracle](https://img.shields.io/badge/Oracle-19c-F80000?logo=oracle&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-compose-2496ED?logo=docker&logoColor=white)
![Azure](https://img.shields.io/badge/Azure-VM%20Linux-0078D4?logo=microsoftazure&logoColor=white)
![xUnit](https://img.shields.io/badge/Testes-xUnit%20%2B%20Moq-green)
[![CI](https://github.com/KURA-Clyvo/backend-clinica-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/KURA-Clyvo/backend-clinica-dotnet/actions/workflows/ci.yml)
![Health](https://img.shields.io/badge/Health%20Checks-self%20%C2%B7%20oracle%20%C2%B7%20luna%20%C2%B7%20mongo-informational)
![MongoDB](https://img.shields.io/badge/MongoDB-laudos%20(kura__docs)-47A248?logo=mongodb&logoColor=white)
![OpenTelemetry](https://img.shields.io/badge/OpenTelemetry-tracing%20%2B%20m%C3%A9tricas%20(Console)-blueviolet)

Hub clínico do ecossistema **Clyvo Vet** — sistema integrado de gestão veterinária desenvolvido como Challenge FIAP 2026. Expõe a API RESTful responsável pelo domínio clínico: prontuários, eventos, agenda, IoT de temperatura e integração com a IA Luna de triagem.

---

## Mapa da rubrica

Cada item avaliável da disciplina, **onde está no código** e **onde está neste README**. Os caminhos
foram conferidos com `ls`/`grep` na revisão que escreveu esta tabela (2026-10-28).

| Item da rubrica | Onde está no código | Onde está no README |
|---|---|---|
| Clean Architecture (camadas) | `src/Kura.Domain` · `src/Kura.Application` · `src/Kura.Infrastructure` · `src/Kura.Api` · `src/Kura.CrossCutting` | [Arquitetura](#arquitetura) |
| SOLID · injeção de dependência | Interfaces em `src/Kura.Domain/Interfaces/` e `src/Kura.Application/Services/Interfaces/`; registro em `src/Kura.Api/Extensions/ServiceCollectionExtensions.cs` (`AddApplication`, `AddInfrastructure`) | [Padrões arquiteturais](#padrões-arquiteturais-relevantes) |
| Tratamento global de exceções | `src/Kura.Api/Middlewares/ExceptionHandlerMiddleware.cs` · `src/Kura.Domain/Exceptions/` | [Padrões arquiteturais](#padrões-arquiteturais-relevantes) |
| Swagger / OpenAPI | `AddSwaggerGen` em `src/Kura.Api/Program.cs` · export em `docs/openapi.json` · teste `tests/Kura.IntegrationTests/OpenApiDocumentoTests.cs` | [Swagger exportado](#swagger-exportado-docsopenapijson) |
| Paginação, ordenação e filtros | `src/Kura.Api/Paginacao/ConsultaPaginada.cs` · `src/Kura.Domain/Paginacao/` · `PetsV2Controller`, `TutoresV2Controller`, `AgendamentosV2Controller`, `LaudosController` | [Paginação, ordenação, filtros e HATEOAS](#paginação-ordenação-filtros-e-hateoas) |
| HATEOAS | `src/Kura.Api/Hal/` (`HalLinkFactory`, `ListasV2Hal`, `LaudoHalDto`) | [Paginação, ordenação, filtros e HATEOAS](#paginação-ordenação-filtros-e-hateoas) |
| JWT / autenticação e autorização | `src/Kura.Api/Program.cs` (`AddJwtBearer`) · `src/Kura.Application/Services/AuthService.cs` · `src/Kura.Api/Extensions/AuthorizationExtensions.cs` | [Autenticação](#autenticação) |
| EF Core com migrações | `src/Kura.Infrastructure/Persistence/KuraDbContext.cs` · `src/Kura.Infrastructure/Migrations/` · `src/Kura.Api/Extensions/MigrationEvidenceExtensions.cs` | [Por que o DDL é Flyway](#por-que-o-ddl-é-flyway-e-o-que-são-as-migrations-ef) |
| MongoDB | `src/Kura.Infrastructure/Mongo/` · `src/Kura.Api/Controllers/LaudosController.cs` | [MongoDB — laudos](#mongodb--laudos) |
| Repository (+ Unit of Work) | `src/Kura.Domain/Interfaces/` · `src/Kura.Infrastructure/Persistence/Repositories/` · `src/Kura.Infrastructure/Mongo/LaudoRepository.cs` | [Padrões arquiteturais](#padrões-arquiteturais-relevantes) |
| Health checks | `src/Kura.Api/Extensions/HealthCheckExtensions.cs` · `src/Kura.Api/HealthChecks/` (`LunaHealthCheck`, `MongoHealthCheck`) | [Health Checks](#health-checks-observabilidade-e-monitoramento) |
| Logging estruturado | Serilog (console + arquivo `logs/kura-api-.log`) em `src/Kura.Api/Program.cs` · `src/Kura.Api/Extensions/RedigirRequestPathEnricher.cs` | [Observabilidade](#health-checks-observabilidade-e-monitoramento) |
| Testes xUnit (AAA), unitários e de integração | `tests/Kura.Domain.Tests` · `tests/Kura.Application.Tests` · `tests/Kura.Infrastructure.Tests` · `tests/Kura.IntegrationTests` | [Testes](#testes) |
| Cobertura de Domínio e Aplicação | `coverlet.collector` nos projetos de teste | [Cobertura](#cobertura-de-domínio-e-aplicação) |
| Endpoints documentados | `docs/openapi.json` + tabelas | [Índice de Endpoints](#índice-de-endpoints) |
| Integrantes | — | [Equipe](#equipe) |

---

## Sumário

1. [Arquitetura](#arquitetura) — inclui [DDL × migrations EF](#por-que-o-ddl-é-flyway-e-o-que-são-as-migrations-ef), [MongoDB](#mongodb--laudos), [paginação/HATEOAS](#paginação-ordenação-filtros-e-hateoas) e [Swagger exportado](#swagger-exportado-docsopenapijson)
2. [Primeiros Passos](#primeiros-passos)
3. [Configuração de Ambiente](#configuração-de-ambiente)
4. [Execução Local](#execução-local)
5. [Execução via Docker](#execução-via-docker)
6. [Autenticação](#autenticação)
7. [Índice de Endpoints](#índice-de-endpoints)
8. [Health Checks, Observabilidade e Monitoramento](#health-checks-observabilidade-e-monitoramento)
9. [Testes](#testes)
10. [Índice de Artefatos — Avaliadores FIAP](#índice-de-artefatos--avaliadores-fiap)
11. [Variáveis de Ambiente](#variáveis-de-ambiente)
12. [Equipe](#equipe)

---

## Arquitetura

Clean Architecture em 4 camadas com separação estrita de responsabilidades:

```
Api  ──►  Application  ──►  Domain
 │                             ▲
 └──────  Infrastructure  ─────┘
```

```mermaid
graph TD
    APP["Apps (mobile-clinica-rn, web da clínica)"] -->|HTTP + JWT| API[Kura.Api]
    LUNA["Luna (IA de triagem, Python)"] -->|HTTP + X-Api-Key| API
    API --> APPL[Kura.Application]
    API --> INFRA[Kura.Infrastructure]
    APPL --> DOM[Kura.Domain]
    INFRA --> DOM
    APPL --> CC[Kura.CrossCutting]
    INFRA --> CC
    INFRA -->|"EF Core (sem DDL)"| ORA[("Oracle — schema compartilhado")]
    INFRA -->|"MongoDB.Driver"| MONGO[("MongoDB kura_docs.laudos")]
    INFRA -->|"Storage:BasePath"| FS[("Storage de arquivos — PDFs e imagens")]
    FLY["Flyway do backend-tutor-java — autoridade de DDL"] -.->|cria e altera as tabelas| ORA
    JAVA["backend-tutor-java (leitor previsto)"] -.->|leitura prevista| MONGO
    API -->|"GET /health"| LUNA
```

Setas cheias = dependência/chamada deste backend. Setas pontilhadas = o que **outro** componente faz
(o Flyway aplica o DDL do Oracle; o Java é o **leitor previsto** da coleção `laudos`, cuja dona é este backend). Oracle,
Mongo e storage são dependências da camada **Infrastructure**; o Domain não conhece nenhuma delas.

| Camada | Responsabilidade |
|---|---|
| **Kura.Domain** | Entidades, interfaces de repositório, exceções de domínio. Zero dependências externas. |
| **Kura.Application** | Serviços de orquestração, DTOs, validadores FluentValidation. |
| **Kura.Infrastructure** | EF Core `KuraDbContext`, repositórios, configurações Fluent API, interceptors. |
| **Kura.Api** | Controllers HTTP, filtros de autenticação, middlewares. Nenhuma lógica de negócio. |
| **Kura.CrossCutting** | Preocupações transversais, que nenhuma camada "possui": hoje só o `ActivitySource` de tracing entre camadas (`KuraActivitySource`). Zero dependências externas, como o Domain — mas separado dele de propósito, porque o núcleo de domínio não deve saber que existe telemetria. |

### Padrões arquiteturais relevantes

- **Repository + Unit of Work** — `IRepository<T>` / `IUnitOfWork` definidos no Domain, implementados na Infrastructure. Um único `CommitAsync()` por operação de serviço.
- **Multi-tenancy implícita** — `IClinicaContext` injeta `ID_CLINICA` do JWT em `HasQueryFilter` global. Endpoints públicos recebem `IdClinicaFiltro = null`, contornando o filtro intencionalmente.
- **Soft delete** — todas as entidades herdam `EntidadeBase` (`StAtiva`, `DtCriacao`, `DtAtualizacao`). Filtro global em `StAtiva`.
- **Bool → CHAR(1)** — conversor global `BoolToSimNaoConverter` mapeia `bool`/`bool?` para `'S'`/`'N'` (schema Oracle).
- **Read-only via interceptor** — `ReadOnlyTablesInterceptor` rejeita qualquer `Add`/`Update`/`Delete` contra `ContaTutor` e `Consentimento` (domínio Java).
- **Concorrência otimista** — `Agendamento` usa coluna `NrVersion`; conflito retorna HTTP 409.


### Por que o DDL é Flyway (e o que são as migrations EF)

O schema Oracle é **compartilhado** com o `backend-tutor-java`, e **quem cria e altera as tabelas é o
Flyway do Java** (`backend-tutor-java`, pastas `db/migration*`). Este backend **não aplica DDL**: o
`Program.cs` nunca chama `Database.Migrate()` nem `EnsureCreated()` (regra em
[`src/Kura.Infrastructure/MIGRATIONS_POLICY.md`](src/Kura.Infrastructure/MIGRATIONS_POLICY.md)).

As migrations EF Core em [`src/Kura.Infrastructure/Migrations/`](src/Kura.Infrastructure/Migrations/)
(17 migrations + `KuraDbContextModelSnapshot.cs`) são o **histórico do modelo EF** — geradas com
`dotnet ef migrations add` e commitadas — e a **evidência de "migrações aplicadas"** da rubrica. O que
o código faz com elas, com precisão
([`MigrationEvidenceExtensions.cs`](src/Kura.Api/Extensions/MigrationEvidenceExtensions.cs)):

- No startup, **fora do ambiente `Testing`**, o `Program.cs` consulta `GetPendingMigrationsAsync()` e,
  se houver migration EF pendente, **só registra um `WARN`** — não aplica nada.
- A verificação tenta até **5 vezes** (espera exponencial 1 s, 2 s, 4 s, 8 s) e, esgotadas as tentativas,
  **segue com a subida**: ela **nunca lança**. Banco fora do ar é reportado por `GET /health`, não por
  um processo que morre.
- O SQL de cada migration pode ser extraído com `dotnet ef migrations script` para `migrations-sql/` e
  entregue a quem mantém o Flyway.

**Por que não trocar a autoridade:** duas ferramentas de DDL no mesmo schema já quebraram este projeto —
o EF declarava `HasDefaultValueSql("SEQ_x.NEXTVAL")` em tabelas cujas sequences o Flyway não criava e
todo `INSERT` do .NET falhava com `ORA-02289` num Oracle criado só pelo Flyway. Foi resolvido
**concentrando o DDL no Flyway** (a migration `V12` do Java criou as sequences). Manter o EF como gerador
de evidência, e o Flyway como único executor, evita que as duas ferramentas voltem a divergir.

> O Oracle **não** é exercitado pela suíte automatizada deste repositório (EF InMemory e fakes).
> `LIKE ... ESCAPE`, `OFFSET/FETCH` e `LongCount` das listas v2 foram provados só no SQL **gerado**; a
> execução contra Oracle real está **NÃO VERIFICADA** neste README.

### MongoDB — laudos

**Por que documento.** Um laudo tem **campos que variam por tipo** (`HEMOGRAMA`, `BIOQUIMICO`,
`IMAGEM`, `RECEITA`, `OUTRO`): hematócrito e leucócitos num, achados de imagem em outro. Em Oracle isso
viraria uma tabela larga cheia de colunas nulas ou um modelo EAV; em documento, o que é comum fica em
campos fixos e o que varia fica em `dados`, sem migration a cada tipo novo.
**Onde documento NÃO vale** (e por isso o resto continua em Oracle): prontuário, agenda, cobrança e
cadastro são relacionais, com integridade referencial, joins e transações entre tabelas — nada disso é
ganho no Mongo.

**Divisão de responsabilidades.** O **binário** (PDF/JPEG/PNG/WebP, até **10 MiB**, aceito pela
**assinatura** dos primeiros bytes, nunca pela extensão) fica no storage de arquivos
(`Storage:BasePath`, chave `clinica/{idClinica}/pet/{idPet}/laudo/{guid}.{ext}`); o Mongo guarda os
**metadados** e o `sha256` do arquivo. A coleção é **do .NET**; o contrato prevê que o `backend-tutor-java` apenas **leia** (essa leitura é de outra frente e **não foi verificada** aqui).

**Contrato da coleção** `kura_docs.laudos` (fonte: `src/Kura.Infrastructure/Mongo/schema/laudos.schema.json`):

| Campo | Tipo BSON | Obrigatório | Regra |
|---|---|---|---|
| `_id` | ObjectId | sim | — |
| `idClinica` · `idPet` · `idUsuarioAutor` | long | sim | escopo e autoria |
| `idEventoClinico` | long ou null | não | vínculo opcional com o prontuário Oracle |
| `tipo` | enum | sim | `HEMOGRAMA` `BIOQUIMICO` `IMAGEM` `RECEITA` `OUTRO` |
| `titulo` | string 1..120 | sim | — |
| `dataExame` | date (UTC) | sim | — |
| `laboratorio` · `observacao` | string ou null | não | até 120 · até 1000 caracteres |
| `dados` | object | sim (pode ser `{}`) | campos variáveis por tipo |
| `arquivo` | object | sim | `chave`, `contentType` (enum de 4), `tamanhoBytes`, `sha256` (`^[0-9a-f]{64}$`) |
| `stAtivo` · `criadoEm` · `atualizadoEm` | bool · date · date | sim | exclusão **lógica** |
| `removidoEm` | date ou null | não | preenchido na exclusão lógica |

O `$jsonSchema` é aplicado com `validationLevel: strict` e `validationAction: error`, com
`additionalProperties: false` na raiz e em `arquivo`. Os **3 índices**
(`src/Kura.Infrastructure/Mongo/schema/laudos.indexes.json`) e a consulta que cada um serve:

| Índice | Chave | Consulta |
|---|---|---|
| `ix_laudo_clinica_pet_ativo_data` | `idClinica, idPet, stAtivo, dataExame↓, _id↓` | lista de laudos do pet na clínica, sem filtro de tipo |
| `ix_laudo_clinica_pet_ativo_tipo_data` | `idClinica, idPet, stAtivo, tipo, dataExame↓, _id↓` | a mesma lista, com `tipo=` |
| `ix_laudo_pet_ativo_data` | `idPet, stAtivo, dataExame↓` | leitura por pet sem clínica (prevista para o lado Java) |

**Escopo obrigatório por clínica.** Todo método de `ILaudoRepository` exige `long idClinica`, e o filtro
de toda leitura/escrita começa por ele e por `stAtivo=true`; não existe `DeleteOne` (exclusão é lógica).
O teste de reflexão `LaudoRepositoryEscopoTests` quebra se alguém acrescentar um método sem `idClinica`
(o equivalente Mongo do `TenantFilterCoverageTests`).

**Runner idempotente.** `MongoSchemaRunner` (`IHostedService`) cria a coleção ou aplica `collMod`, e
cria os índices; rodar de novo não duplica nada. Ele **captura toda exceção** (1 `WARN`, sem URI nem
credencial) e **nunca derruba o processo**. Não roda com `Mongo__InitEnabled=false` nem sem connection
string.

**Mongo fora do ar não derruba a API.** `GET /health` devolve o check `mongo` como `Degraded` (HTTP 200),
nunca `Unhealthy`; as rotas de laudo devolvem `503` com `codigo=LAUDOS_INDISPONIVEL`. Sem
`Mongo__ConnectionString` a API sobe normalmente e laudos ficam indisponíveis.

Configuração: `Mongo__ConnectionString` (vazia = não configurado), `Mongo__Database` (padrão
`kura_docs`), `Mongo__InitEnabled` (padrão `true`), `Mongo__ServerSelectionTimeoutSeconds` (padrão e
teto: 3).

> **Versões — leia antes de citar.** Os testes do repositório e do runner rodam contra um **mongod 8.0.26
> embarcado (Mongo2Go 5.0.0)**. **Medido em 29/10/2026 pelo G4 da F1 no compose LOCAL, com MongoDB
> 7.0.45 real** (fonte: `s4-net-g4.md`, frentes 1–4): o runner aplicou o `$jsonSchema` (`validationLevel
> strict`, `additionalProperties: false`, 12 campos obrigatórios) e os 3 índices com a role real
> `kura_dotnet_runtime`; duas clínicas pelo HTTP (outra clínica recebe `404` em lista, detalhe, arquivo e
> `DELETE`); com o Mongo parado, `/health` devolve `200 Degraded` e os endpoints de laudos devolvem `503`
> (`LAUDOS_INDISPONIVEL`) em ~3 s, enquanto o resto da API segue de pé, e tudo volta **sem reiniciar** a API;
> `/api/v2` (pets, tutores, agendamentos) contra Oracle real com `total` igual ao `sqlplus`. Continua
> **NÃO VERIFICADO**: o deploy na **Azure** (inclusive o `mongo-init` da VM) e a leitura de `laudos` pelo
> `backend-tutor-java` como aplicação (só o usuário `kura_java`, por `mongosh`).
>
> **Runbook (1 linha):** se o log do `kura-api` tiver o aviso `Mongo: schema…`, o runner rodou antes de o
> `mongo-init` criar o usuário `kura_dotnet` (ambiente novo; o `kura-api` não depende do `mongo-init` de
> propósito) — rode `docker compose restart kura-api` (fonte: `s4-net-g4.md`, nota n-1).

### Paginação, ordenação, filtros e HATEOAS

**Parâmetros comuns** (`ConsultaPaginada`): `page` (≥ 1, padrão 1), `pageSize` (1 a 100, padrão 20) e
`sort` (lista branca por recurso; prefixo `-` = decrescente). Valor inválido devolve **400** de validação,
nunca 500. O `sort` nunca chega ao banco como texto livre: é mapeado para um enum fechado.

| Recurso | Rota | `sort` permitido (o padrão é o primeiro) | Filtros |
|---|---|---|---|
| Pets | `GET /api/v2/pets` | `nome`, `-nome`, `dtNascimento`, `-dtNascimento`, `id`, `-id` | `nome` (contém), `tutorId`, `especieId`, `porte` (P/M/G) |
| Tutores | `GET /api/v2/tutores` | `nome`, `-nome`, `id`, `-id` | `busca` (nome ou CPF parcial) |
| Agendamentos | `GET /api/v2/agendamentos` | `dtAgendamento`, `-dtAgendamento`, `id`, `-id` | `dataInicio` e `dataFim` (**obrigatórios**, até 31 dias), `veterinarioId`, `status` |
| Laudos do pet | `GET /api/v1/pets/{idPet}/laudos` | `-dataExame`, `dataExame`, `criadoEm`, `-criadoEm` | `tipo`, `de`, `ate` (ISO-8601) |

**Envelope HAL.** Lista: `items`, `page`, `pageSize`, `total`, `totalPages` e `_links`
(`self`, `first`, `last`, e `prev`/`next` só quando existem; todos preservam filtros e ordenação). Com
`total = 0`, o envelope traz só `self` e `first`. Cada item tem o próprio `_links`. Os hrefs saem do
`LinkGenerator` a partir do **nome da rota** — nenhum é escrito à mão — e vivem só na camada Api.
Exemplo real de uma execução do host de teste (`GET /api/v1/pets/1/laudos?pageSize=2&sort=dataExame`,
token de GESTOR; o array real tem 2 itens, mostrado 1):

```json
{
  "items": [
    {
      "_links": {
        "self": { "href": "/api/v1/laudos/000000000000000000000001" },
        "pet": { "href": "/api/v1/pets/1" },
        "laudosDoPet": { "href": "/api/v1/pets/1/laudos" },
        "arquivo": { "href": "/api/v1/laudos/000000000000000000000001/arquivo" },
        "excluir": { "href": "/api/v1/laudos/000000000000000000000001", "method": "DELETE" }
      },
      "id": "000000000000000000000001", "idPet": 1, "tipo": "HEMOGRAMA", "titulo": "Laudo 1",
      "dataExame": "2026-09-01T00:00:00Z", "dados": { "hematocrito": 42.5 },
      "arquivo": { "contentType": "application/pdf", "tamanhoBytes": 10, "sha256": "fc0222…e003" }
    }
  ],
  "page": 1, "pageSize": 2, "total": 3, "totalPages": 2,
  "_links": {
    "self":  { "href": "/api/v1/pets/1/laudos?sort=dataExame&page=1&pageSize=2" },
    "first": { "href": "/api/v1/pets/1/laudos?sort=dataExame&page=1&pageSize=2" },
    "last":  { "href": "/api/v1/pets/1/laudos?sort=dataExame&page=2&pageSize=2" },
    "next":  { "href": "/api/v1/pets/1/laudos?sort=dataExame&page=2&pageSize=2" }
  }
}
```

(Os campos `idEventoClinico`, `laboratorio`, `observacao`, `criadoEm` e `atualizadoEm` foram omitidos do
exemplo; os ids são os do fake em memória do teste — em produção são ObjectId de 24 hex.)

**Ações por papel.** O mesmo `GET` devolve `_links` diferentes conforme o papel: o link `excluir` do laudo
só aparece para `GESTOR` — a decisão usa a **mesma política** (`SomenteGestor`) que protege o `DELETE`,
então o link e o `403` não divergem. `GESTOR` e `VETERINARIO` enviam laudo. Nas listas v2, as ações de
recepção do agendamento (`checkin`, `inicio-atendimento`, `status`) só aparecem quando a regra do
`AgendaService` as admite para o estado atual.

**Por que as listas paginadas estão em `/api/v2`.** `GET /api/v1/pets` e `GET /api/v1/tutores` devolvem
**array puro**, e o app da clínica publicado lê exatamente esse formato. Trocar o shape da v1 para um
envelope paginado quebraria o app em produção sem nenhum teste deste repositório avisar. Por isso
paginação, ordenação, filtros e HAL de pets, tutores e agendamentos nasceram em **`/api/v2`**, com a v1
intacta (versionamento por rota para evoluir o contrato sem quebrar clientes). Laudos nasceram direto em
`/api/v1`, já paginados, porque não tinham consumidor anterior.

> **Declarado:** a busca por CPF parcial em `busca` volta nos links de navegação (senão a página 2
> deixaria de ser a da mesma busca). Em teste, a instrumentação do OpenTelemetry 1.18 redigiu `url.query`
> e o log de requisição do Serilog não registrou CPF; **não** foi verificado com um coletor real.

### Swagger exportado (`docs/openapi.json`)

A documentação dos endpoints está **exportada** em [`docs/openapi.json`](docs/openapi.json) (OpenAPI do
Swashbuckle, gerado **da aplicação**, nunca à mão) e disponível ao vivo em `/swagger` no ambiente
`Development`. O arquivo **não envelhece**: o teste
[`OpenApiDocumentoTests`](tests/Kura.IntegrationTests/OpenApiDocumentoTests.cs) sobe o `Program.cs`
real, baixa `/swagger/v1/swagger.json` e o compara com o arquivo versionado — rota nova, parâmetro novo ou
`summary` alterado sem regenerar derruba a suíte, com o comando na mensagem. Para regenerar:

```bash
KURA_ATUALIZAR_OPENAPI=1 dotnet test tests/Kura.IntegrationTests --filter "FullyQualifiedName~OpenApiDocumentoTests"
```

(Para o teste poder baixar o JSON, o Swagger também é exposto no ambiente `Testing`.)

Parâmetros de query saem no OpenAPI em **camelCase** (`page`, `pageSize`, `sort`, `nome`…), iguais aos
`_links` (o binding aceita qualquer caixa: `?Page=2` e `?page=2` são equivalentes). Os uploads
`multipart/form-data` (laudos e foto do pet) têm o `requestBody` documentado por
`[DocumentaMultipart]`, só na documentação — o binding do endpoint não muda.

---

## Primeiros Passos

### Pré-requisitos

| Ferramenta | Versão mínima |
|---|---|
| .NET SDK | 10.0 |
| Docker + Docker Compose | 24.x |
| Oracle alcançável | Oracle XE local do `DevOps-Cloud` (`localhost:9092/XEPDB1`) |

> 🔴 **Não aponte a aplicação para `oracle.fiap.com.br`.** É infraestrutura **compartilhada e
> viva**: credencial errada gera `ORA-01017` a cada tentativa e, com o `restart` do container
> repetindo isso em laço, `ORA-01017` repetido vira `ORA-28000` (**conta bloqueada**) — já
> aconteceu neste projeto e travou a conta de toda a equipe. Use o Oracle XE local do
> `DevOps-Cloud`. Os exemplos deste README usam `localhost:9092/XEPDB1` por esse motivo.

### Clonar o repositório

```bash
git clone https://github.com/KURA-Clyvo/backend-clinica-dotnet.git
cd backend-clinica-dotnet
```

---

## Configuração de Ambiente

> **Política de segredos**: jamais commite credenciais. O arquivo `appsettings.Development.json` está no `.gitignore`. Use variáveis de ambiente ou o mecanismo de User Secrets do .NET para desenvolvimento local.

### Opção A — User Secrets (recomendado para desenvolvimento local)

```bash
dotnet user-secrets init --project src/Kura.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "User Id=<YOUR_ORACLE_USER>;Password=<YOUR_ORACLE_PASSWORD>;Data Source=localhost:9092/XEPDB1" \
  --project src/Kura.Api
dotnet user-secrets set "Jwt:Key" "<YOUR_JWT_SECRET_MIN_32_CHARS>" --project src/Kura.Api
dotnet user-secrets set "Jwt:Issuer" "kura-api" --project src/Kura.Api
dotnet user-secrets set "Jwt:Audience" "kura-client" --project src/Kura.Api
dotnet user-secrets set "IoT:ApiKey" "<YOUR_IOT_API_KEY>" --project src/Kura.Api
dotnet user-secrets set "Luna:ApiKey" "<YOUR_LUNA_API_KEY>" --project src/Kura.Api
# Obrigatória para GET /health responder — sem ela o endpoint devolve 500 (ver §Testes/§Health).
dotnet user-secrets set "Luna:BaseUrl" "http://localhost:8000" --project src/Kura.Api
dotnet user-secrets set "Luna:InboundApiKey" "<YOUR_LUNA_INBOUND_API_KEY>" --project src/Kura.Api
dotnet user-secrets set "Daily:ApiKey" "<YOUR_DAILY_CO_API_KEY>" --project src/Kura.Api
```

### Opção B — `appsettings.Development.json` (local, nunca versionado)

Crie `src/Kura.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "User Id=<YOUR_ORACLE_USER>;Password=<YOUR_ORACLE_PASSWORD>;Data Source=localhost:9092/XEPDB1"
  },
  "Jwt": {
    "Key": "<YOUR_JWT_SECRET_MIN_32_CHARS>",
    "Issuer": "kura-api",
    "Audience": "kura-client",
    "ExpiryHours": 8
  },
  "IoT": { "ApiKey": "<YOUR_IOT_API_KEY>" },
  "Luna": {
    "ApiKey": "<YOUR_LUNA_API_KEY>",
    "BaseUrl": "http://localhost:8000",
    "InboundApiKey": "<YOUR_LUNA_INBOUND_API_KEY>"
  },
  "Daily": { "ApiKey": "<YOUR_DAILY_CO_API_KEY>" }
}
```

> `Luna:BaseUrl` é lida pelo health check da Luna e pelo client de transcrição. **Se faltar,
> a aplicação sobe normalmente e é `GET /health` que passa a devolver `500`** com
> `InvalidOperationException: Luna:BaseUrl not configured.` — medido. Ver
> [Health Checks](#health-checks-observabilidade-e-monitoramento).

### Opção C — `.env` para Docker Compose

```bash
cp .env.example .env
# edite .env e preencha os valores reais
```

---

## Execução Local

```bash
dotnet restore
dotnet build
dotnet run --project src/Kura.Api
# Swagger UI: http://localhost:5162/swagger  (apenas ambiente Development)
# Health:     http://localhost:5162/health
# Metrics:    http://localhost:5162/metrics
```

A porta vem do perfil `http` de `src/Kura.Api/Properties/launchSettings.json`
(`applicationUrl: http://localhost:5162`); o perfil `https` sobe também em `7112`. Para ignorar
os perfis e escolher a porta explicitamente, use
`dotnet run --no-launch-profile --project src/Kura.Api` com `ASPNETCORE_URLS`.

> ⚠️ `launchSettings.json` força `ASPNETCORE_ENVIRONMENT=Development`, e em `Development` o
> `Program.cs` executa o bloco de validação de migrations, que **abre conexão real** com o
> Oracle da connection string configurada. Confira para onde ela aponta **antes** de rodar —
> é exatamente esse caminho que já bloqueou a conta institucional.

🔴 **`dotnet run` exige um Oracle alcançável — não é opcional, e a falha não é graciosa.**
Medido nesta máquina, com a connection string apontada para uma porta morta: o processo
**termina no startup** com exceção não tratada e **nunca chega a escutar na porta**.

```
Unhandled exception. Oracle.ManagedDataAccess.Client.OracleException (0x80004005): ORA-50201: ...
 ---> OracleInternal.Network.NetworkException (0x80004005): ORA-50201: ...
 ---> OracleInternal.Network.NetworkException (0x80004005): ORA-12541: TNS: não há listener
   at Program.<Main>$(String[] args) in src/Kura.Api/Program.cs:line 136
```

Repare que o bloco de retry **não re-tentou**: nenhuma linha `"Oracle não disponível (ORA-…)"`
foi emitida. O motivo está detalhado em
[Cold start](#cold-start-no-compose--o-que-é-esperado-e-o-que-não-é-verdade) — o `.Number`
comparado pelo `when` pertence à `OracleException` externa (`ORA-50201`), e o código de rede
real vem numa `NetworkException` dois níveis abaixo.

Se você só quer rodar a **suíte de testes**, não precisa de Oracle nenhum — veja
[Testes](#testes).

---

## Execução via Docker

```bash
cp .env.example .env      # obrigatório: preencha os valores antes de subir
docker-compose up --build
# Swagger UI: http://localhost:8080/swagger
# Health:     http://localhost:8080/health
# Metrics:    http://localhost:8080/metrics
```

> 🔴 **O `cp .env.example .env` não é opcional.** Todas as variáveis do `docker-compose.yml`
> usam a forma `${VAR:?mensagem}`: sem o `.env` preenchido o Compose **aborta** com a mensagem
> da variável que faltou, em vez de subir o container com valor vazio. Isso é deliberado — a
> versão anterior deste arquivo renderizava `Password=;Data Source=oracle.fiap.com.br`, ou
> seja, usuário real e host vivo com senha vazia, que é o laço de `ORA-01017` que bloqueia a
> conta.

Este compose sobe **apenas a API**; ele não inclui banco. Aponte `ORACLE_DATA_SOURCE` para um
Oracle que você controle — o XE local do `DevOps-Cloud` (`localhost:9092/XEPDB1`) é o alvo
esperado. Para subir o ecossistema completo (Oracle XE + .NET + Java + Luna), use o
`docker compose` do repositório `DevOps-Cloud`, não este.

> 🔴 **Limitação conhecida deste `docker-compose.yml`: o `.env.example` traz 3 valores que o
> compose nunca repassa ao container** — `LUNA_BASE_URL`, `LUNA_INBOUND_API_KEY` e
> `STORAGE_BASE_PATH`, que a aplicação leria como `Luna__BaseUrl`, `Luna__InboundApiKey` e
> `Storage__BasePath`.
>
> ⚠️ Repare nos **dois** sistemas de nome: o `.env` usa `LUNA_BASE_URL`; a aplicação .NET lê
> `Luna__BaseUrl`. É o bloco `environment:` do serviço que liga um ao outro — e para estes 3 ele
> simplesmente **não tem a linha**. Por isso preencher o `.env` **não basta**. Medido:
> `docker compose --env-file .env config | grep -c Luna__BaseUrl` → **0**
> (contra **1** para `Luna__ApiKey` e `Daily__ApiKey`, que é o controle positivo de que o
> comando enxergaria a variável se ela estivesse lá).
>
> **Consequência esperada — inferida do código, não medida** (ninguém subiu este container com a
> imagem atual): o container sobe e `GET /health` responde **`500`** com
> `Luna:BaseUrl not configured.`, o mesmo sintoma descrito em
> [Health Checks](#health-checks-observabilidade-e-monitoramento) — aqui por omissão do compose,
> não por erro de quem configurou. A transcrição de áudio quebraria pela mesma razão, embora a
> exceção que aparece seja sempre a de `Luna:BaseUrl`: as duas chaves são lidas no mesmo
> registro, e essa é a primeira. `Storage__BasePath` é opcional e cai no default.
>
> **Saídas:** acrescente as 3 linhas ao bloco `environment:` do serviço, **ou** use o
> `docker compose` do `DevOps-Cloud`, que já as injeta. Este README **não** afirma que o compose
> daqui entrega um `/health` verde — porque ele não entrega.

---

## Autenticação

| Contexto | Mecanismo | Consumidor |
|---|---|---|
| Front da clínica | JWT Bearer — `Authorization: Bearer {token}` | Veterinários e gestores |
| Dispositivos IoT (ESP32) | API Key — `X-Api-Key: {key}` | Sensores de temperatura |
| IA Luna (Python) | API Key — `X-Api-Key: {key}` | Chatbot de triagem |

> A linha "IA Luna" acima documenta o par `Luna:ApiKey`/`LUNA_API_KEY` — a chave que a
> Luna manda no header **`X-Api-Key`** (a migração prevista na TASK-68 **já foi concluída**;
> `LunaApiKeyAuthFilter` lê `X-Api-Key` e nada mais) para
> autenticar suas chamadas *para* este backend (`GET /tutores/telefone/{numero}`,
> `POST /luna/interactions`, `POST /luna/triage` — TASK-67). **Não confundir** com
> `Luna:InboundApiKey`/`LUNA_INBOUND_API_KEY`, usada na direção oposta (este backend →
> Luna, `POST /transcricao`, FEAT-02). São duas chaves/direções diferentes mesmo
> compartilhando o prefixo `Luna:` na config.

### Fluxo de autenticação JWT

```
1. POST /api/v1/auth/register-clinica   →  cria clínica (sem token)
2. POST /api/v1/auth/login              →  retorna { "token": "..." }
3. Swagger → Authorize → Bearer {token}
```

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"dsEmail":"admin@clinica.com","dsSenha":"Senha123!"}' \
  | jq -r '.token')

curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/v1/tutores
```

---

## Índice de Endpoints

> A fonte completa e sempre atual é o OpenAPI exportado em [`docs/openapi.json`](docs/openapi.json) (ver [Swagger exportado](#swagger-exportado-docsopenapijson)); as tabelas abaixo são o índice legível.

### Auth

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| POST | `/api/v1/auth/login` | Autenticação — retorna JWT | Público |
| POST | `/api/v1/auth/register-clinica` | Cadastro de nova clínica | Público |

### Clínicas

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/clinicas/{id}` | Buscar clínica por ID | JWT |
| PUT | `/api/v1/clinicas/{id}` | Atualizar clínica | JWT |
| DELETE | `/api/v1/clinicas/{id}` | Soft delete da clínica | JWT |

### Veterinários

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/veterinarios` | Listar veterinários | JWT |
| GET | `/api/v1/veterinarios/{id}` | Buscar por ID | JWT |
| POST | `/api/v1/veterinarios` | Cadastrar veterinário | JWT |
| PUT | `/api/v1/veterinarios/{id}` | Atualizar veterinário | JWT |
| DELETE | `/api/v1/veterinarios/{id}` | Soft delete | JWT |

### Tutores

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/tutores` | Listar tutores (busca por nome/CPF) | JWT |
| GET | `/api/v1/tutores/{id}` | Buscar por ID | JWT |
| GET | `/api/v1/tutores/{id}/pets` | Pets vinculados ao tutor | JWT |
| GET | `/api/v1/tutores/telefone/{numero}` | Contexto do tutor (clínica + pets) pelo WhatsApp — consumido pela IA Luna (TASK-67) | API Key |
| POST | `/api/v1/tutores` | Criar tutor + invite de onboarding (retorna `TutorComInviteResponseDto` com token UUID válido por 7 dias e canal WHATSAPP \| EMAIL \| SMS) | JWT |
| PUT | `/api/v1/tutores/{id}` | Atualizar tutor | JWT |

### Pets

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/pets` | Listar pets (filtros: `tutorId`, `especieId`, `porte`) | JWT |
| GET | `/api/v1/pets/{id}` | Buscar por ID | JWT |
| POST | `/api/v1/pets` | Cadastrar pet | JWT |
| PUT | `/api/v1/pets/{id}` | Atualizar pet | JWT |
| DELETE | `/api/v1/pets/{id}` | Soft delete | JWT |
| POST | `/api/v1/pets/{id}/tutores` | Vincular tutor adicional (N:N) | JWT |
| GET | `/api/v1/pets/{id}/timeline` | Timeline cronológica de eventos clínicos | JWT |
| GET | `/api/v1/pets/{id}/proximas-vacinas` | Próximas doses agendadas | JWT |

### Eventos Clínicos

> Cada POST cria `EventoClinico` + subtipo atomicamente em um único `CommitAsync()`.

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/eventos-clinicos` | Listar (filtros: `petId`, `tipo`, `dataInicio`, `dataFim`, `veterinarioId`) | JWT |
| GET | `/api/v1/eventos-clinicos/{id}` | Buscar por ID | JWT |
| POST | `/api/v1/eventos-clinicos/vacinas` | Registrar vacina | JWT |
| POST | `/api/v1/eventos-clinicos/prescricoes` | Registrar prescrição | JWT |
| POST | `/api/v1/eventos-clinicos/exames` | Registrar exame | JWT |
| POST | `/api/v1/eventos-clinicos/consultas` | Registrar consulta clínica | JWT |

### Agenda

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/agenda?dataInicio=&dataFim=&veterinarioId=` | Agenda do intervalo (máx. 31 dias; leitura da tabela Java `AGENDAMENTO`) | JWT |
| PATCH | `/api/v1/agendamentos/{id}/status` | Atualizar status com concorrência otimista via `NrVersion` | JWT |

> PATCH aceita `{ "dsStatus": "REALIZADO|CANCELADO", "nrVersion": N }`. Retorna **409** se `nrVersion` estiver desatualizado.

### Luna (IA de Triagem)

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/luna/triagens/relatorio?dataInicio=&dataFim=` | Relatório agregado de triagens geradas pelo chatbot | JWT |
| POST | `/api/v1/luna/interactions` | Registra interação de canal (WhatsApp/e-mail/SMS) recebida/enviada pela Luna (TASK-67) | API Key |
| POST | `/api/v1/luna/triage` | Registra resultado de triagem de IA, ligado à interação de origem (TASK-67) | API Key |

### Dashboard

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/dashboard/hoje` | Resumo do dia atual | JWT |
| GET | `/api/v1/dashboard/alertas` | Alertas ativos | JWT |
| GET | `/api/v1/dashboard/recentes` | Agendamentos recentes | JWT |

### Notificações

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/notificacoes` | Listar notificações da clínica | JWT |
| PATCH | `/api/v1/notificacoes/{id}/marcar-lida` | Marcar como lida | JWT |

### IoT

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| POST | `/api/v1/iot/leituras` | Ingerir leitura de temperatura de sensor ESP32 | API Key |
| GET | `/api/v1/iot/dispositivos` | Listar dispositivos registrados | API Key |
| GET | `/api/v1/iot/dispositivos/{id}/leituras` | Histórico de leituras do dispositivo | API Key |
| GET | `/api/v1/iot/dispositivos/{id}/status` | Status atual do dispositivo | API Key |
| GET | `/api/v1/iot/alertas` | Listar alertas de temperatura | API Key |

### Medicamentos

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/medicamentos?busca=&page=&pageSize=` | Listar medicamentos paginados | JWT |
| GET | `/api/v1/medicamentos/{id}` | Buscar por ID | JWT |
| POST | `/api/v1/medicamentos` | Cadastrar medicamento | JWT |

### Laudos (MongoDB + storage de arquivos)

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| POST | `/api/v1/pets/{idPet}/laudos` | Envia laudo (`multipart/form-data`: `arquivo`, `tipo`, `titulo`, `dataExame`, opcionais `laboratorio`, `observacao`, `idEventoClinico`, `dados`); PDF/JPEG/PNG/WebP até 10 MiB | JWT |
| GET | `/api/v1/pets/{idPet}/laudos` | Lista paginada (`page`, `pageSize`, `sort`, `tipo`, `de`, `ate`), em HAL | JWT |
| GET | `/api/v1/laudos/{id}` | Detalhe (`id` = ObjectId de 24 hex), em HAL | JWT |
| GET | `/api/v1/laudos/{id}/arquivo` | Baixa o binário | JWT |
| DELETE | `/api/v1/laudos/{id}` | Exclusão **lógica** | JWT, somente `GESTOR` |

Respostas de erro: `404` (inexistente, removido, de outra clínica ou id malformado), `422` (o `$jsonSchema`
recusou), `503` com `codigo=LAUDOS_INDISPONIVEL` (Mongo fora do ar). Detalhes em
[MongoDB — laudos](#mongodb--laudos).

### Listas paginadas — `/api/v2`

Pets, tutores e agendamentos em envelope HAL, com paginação, ordenação e filtros. A `/api/v1` dos mesmos
recursos **continua devolvendo array** (ver [por que](#paginação-ordenação-filtros-e-hateoas)).

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v2/pets` | Lista (`page`, `pageSize`, `sort`, `nome`, `tutorId`, `especieId`, `porte`) | JWT |
| GET | `/api/v2/pets/{id}` | Detalhe, em HAL | JWT |
| GET | `/api/v2/tutores` | Lista (`page`, `pageSize`, `sort`, `busca`) | JWT |
| GET | `/api/v2/tutores/{id}` | Detalhe, em HAL | JWT |
| GET | `/api/v2/agendamentos` | Lista (`dataInicio` e `dataFim` obrigatórios, `page`, `pageSize`, `sort`, `veterinarioId`, `status`) | JWT |

### Métricas e Saúde

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/metrics` | Contagens do ambiente inteiro (`escopo: "ambiente"`) + `uptimeSeconds`. **Não** são as métricas de desempenho do OTel | Público |
| GET | `/metrics/clinica` | Pets, eventos e triagens **da clínica do JWT** | JWT |
| GET | `/health` | Health check real — 4 checks (`self`, `oracle`, `luna`, `mongo`) | Público |

> `/metrics` e as métricas do OpenTelemetry são **coisas diferentes com nomes parecidos** —
> ver [Health Checks, Observabilidade e Monitoramento](#health-checks-observabilidade-e-monitoramento).

---

## Testes

**Pré-requisito: nenhum.** A suíte inteira roda **sem Oracle, sem Docker e sem variável de
ambiente nenhuma** (o Mongo2Go extrai um `mongod` do pacote NuGet e o sobe como processo local) — medido com `env -u ASPNETCORE_ENVIRONMENT -u
ConnectionStrings__DefaultConnection`. A suíte de integração sobe o `Program.cs` real com o
`DbContext` substituído por InMemory e uma connection string inerte.

```bash
dotnet test                                                   # tudo
dotnet test KuraApi.slnx --filter "Categoria=Integracao"      # só integração
dotnet test KuraApi.slnx --filter "Categoria!=Integracao"     # só unitários
dotnet test --filter "FullyQualifiedName~NomeDoServico"       # por classe
```

Contagem **medida em 2026-10-28**, com `dotnet test KuraApi.slnx` e `EXIT=0` gravado no log:
`52 + 750 + 280 + 288 = 1370` (Domain · Application · Infrastructure · Integration). O número envelhece a
cada teste novo: a fonte **viva** é o badge de CI no topo, e o retrato datado acima serve só de referência.
Recortes por trait:

```bash
dotnet test KuraApi.slnx --filter "Categoria=Integracao"      # só integração (Kura.IntegrationTests)
dotnet test KuraApi.slnx --filter "Categoria!=Integracao"     # só unitários (os outros 3 projetos)
dotnet test tests/Kura.Application.Tests                      # só um projeto
```

⚠️ O filtro `!=` do VSTest casa **também** o teste que não declara a propriedade — é por isso que os
arquivos unitários não precisaram ser anotados um a um, e é por isso que uma classe de integração que
**esqueça** o `[Trait]` cairia silenciosamente no balde unitário.
`ConvencaoDeTestesCoverageTests` existe para impedir exatamente isso.

**As 3 camadas de teste e o que cada uma prova** (todas em xUnit, padrão **AAA** — Arrange/Act/Assert):

1. **Unitário, com mocks** (`Kura.Domain.Tests`, `Kura.Application.Tests`): regras de domínio e services
   com dependências dubladas (Moq). Prova a lógica, não a persistência.
2. **Infraestrutura** (`Kura.Infrastructure.Tests`): repositórios e políticas de persistência em EF
   InMemory e, para os **laudos, contra um `mongod` 8.0 embarcado (Mongo2Go 5.0.0)** — repositório
   concreto, `$jsonSchema` recusando documento inválido, índices criados e isolamento entre duas clínicas.
   É o **8.0**; o 7.0.45 do compose foi exercitado à parte pelo G4 (ver
   [MongoDB — laudos](#mongodb--laudos)). Esta camada não usa Docker nem Oracle.
3. **Integração HTTP** (`Kura.IntegrationTests`): sobe o `Program.cs` real com `KuraApiFactory`
   (persistência trocada por EF InMemory e **fakes** de Mongo e storage) e prova rota, autorização,
   escopo de clínica, paginação, `_links`, `503` com Mongo fora — mais o `OpenApiDocumentoTests`.

O que **nenhuma** camada automatizada prova: Oracle real, Mongo 7.0 e o deploy na Azure. Oracle real e
Mongo 7.0.45 foram medidos **no compose local** pelo G4 (29/10/2026, `s4-net-g4.md`) — não são testes do
repositório; a **Azure** continua **NÃO VERIFICADA** (ver o aviso em [MongoDB — laudos](#mongodb--laudos)).

| Projeto | Recorte | O que cobre |
|---|---|---|
| `tests/Kura.Domain.Tests` | Unit | Regras de domínio, em processo |
| `tests/Kura.Application.Tests` | Unit | Services com dependências dubladas |
| `tests/Kura.Infrastructure.Tests` | Unit | Políticas de persistência, EF InMemory e Mongo embarcado (Mongo2Go, mongod 8.0), sem host HTTP |
| `tests/Kura.IntegrationTests` | **Integration** | Sobe o `Program.cs` real e faz requisições HTTP ponta a ponta |

Detalhe da convenção, das fixtures e das restrições de quem for acrescentar teste:
[`tests/README.md`](tests/README.md).

### Cobertura de Domínio e Aplicação

Gerada com o `coverlet.collector` que já está nos projetos de teste (sem ferramenta extra):

```bash
dotnet test tests/Kura.Application.Tests --collect:"XPlat Code Coverage" --results-directory <pasta>
dotnet test tests/Kura.Domain.Tests      --collect:"XPlat Code Coverage" --results-directory <pasta>
# lê-se package@line-rate / branch-rate em coverage.cobertura.xml
```

Medido em **2026-10-28** (retrato datado, não é um gate do CI):

| Assembly | Gerado por | Linhas | Ramos |
|---|---|---|---|
| `Kura.Application` | `Kura.Application.Tests` (750 testes) | **80,43%** | 74,91% |
| `Kura.Domain` | `Kura.Application.Tests` (exercita o Domain por baixo) | **88,77%** | 93,75% |
| `Kura.Domain` | `Kura.Domain.Tests` sozinho (52 testes) | 21,65% | 100% |

Os relatórios **não foram mesclados**: o número de Domínio é o do run de `Kura.Application.Tests`; o
`Kura.Domain.Tests` isolado cobre pouco do Domain porque a maior parte das regras é exercitada pelos
services.

---

## Health Checks, Observabilidade e Monitoramento

### `GET /health` — o que é, e o que cada campo significa

Rota **pública** (não exige JWT nem API key), registrada em `Program.cs` por
`app.MapKuraHealthChecks("/health")`. Substituiu o antigo `HealthController`, que devolvia
`200` incondicional sem consultar nada.

O corpo é escrito pelo `ResponseWriter` customizado em
`src/Kura.Api/Extensions/HealthCheckExtensions.cs`:

| Campo | Significado |
|---|---|
| `status` | Estado **agregado** — o pior entre os 4 checks |
| `timestamp` | Instante da apuração, UTC (ISO 8601) |
| `checks[].name` | `self` · `oracle` · `luna` · `mongo` |
| `checks[].status` | `Healthy` · `Degraded` · `Unhealthy` |
| `checks[].description` | Texto do check. **Pode ser `null`** — o check `oracle` vem do pacote oficial e não define descrição |
| `checks[].durationMs` | Duração **daquele** check, em milissegundos |

Corpo real, medido nesta suíte em 2026-08-27 — **antes** do check `mongo` existir (por isso só 3 entradas) — com o
Oracle apontado para uma porta morta e a Luna ausente (recorte formatado para leitura; o serviço emite em linha única):

```json
{
  "status": "Unhealthy",
  "timestamp": "2026-08-27T02:56:24.1171552Z",
  "checks": [
    { "name": "self",   "status": "Healthy",   "description": "API respondendo.", "durationMs": 0.0294 },
    { "name": "oracle", "status": "Unhealthy", "description": null,               "durationMs": 2034.9751 },
    { "name": "luna",   "status": "Degraded",  "description": "Luna indisponível ou não respondeu a tempo.", "durationMs": 2037.0647 }
  ]
}
```

### Os 4 checks

| Check | O que verifica | Falha vira | Por quê |
|---|---|---|---|
| `self` | Que o delegate executou — ou seja, que o pipeline HTTP está de pé e servindo | nunca falha | É o sinal de vida do processo |
| `oracle` | Conectividade com o banco, via `AddDbContextCheck<KuraDbContext>` (pacote oficial da Microsoft) | **`Unhealthy`** | Oracle fora do ar **é** falha real desta API |
| `luna` | `GET {Luna:BaseUrl}/health`, `HttpClient` dedicado com timeout de **3s** | **`Degraded`**, nunca `Unhealthy` | A API da clínica é 100% operacional sem a Luna — só as 2 FEATs que dependem dela ficam indisponíveis |
| `mongo` | Comando `ping` no MongoDB (`MongoHealthCheck`), timeout de **3s**; sem `Mongo__ConnectionString` devolve `Degraded` "não configurado" | **`Degraded`**, nunca `Unhealthy`; o check nunca lança | O Mongo é dependência de **uma feature** (laudos), não da API: fora do ar, só as rotas de laudo respondem `503`. A descrição não carrega URI nem credencial |

> Por isso, num ambiente **sem** `Mongo__ConnectionString` (por exemplo, um `dotnet run` local sem Mongo), o `status` agregado de `GET /health` é `Degraded` (HTTP 200), não `Healthy`. O Oracle continua sendo o único check que vira `503`.

### Status agregado → código HTTP

| `status` | HTTP | Leitura do operador |
|---|---|---|
| `Healthy` | **200** | Tudo no ar |
| `Degraded` | **200** | Serviço **atendendo**; uma dependência de terceiro (Luna) caiu |
| `Unhealthy` | **503** | Banco inacessível — a API não consegue cumprir a função |

🔴 **`Degraded` mapear para `200` é decisão, não descuido.** O healthcheck do container
`kura-api` é `curl -sf .../health`: se `Degraded` virasse `503`, a Luna fora do ar deixaria o
`kura-api` `unhealthy`, e como o serviço `luna-ai` declara
`depends_on: kura-api: condition: service_healthy`, o resultado seria uma **dependência
circular de disponibilidade** — a Luna nunca subiria porque a Luna está fora do ar. Não
"corrija" isso sem ler o XML de `LunaHealthCheck`.

### Os checks rodam em PARALELO — o pior caso é o máximo, não a soma

Medido na mesma requisição do corpo acima: `oracle` **2034,98 ms** + `luna` **2037,06 ms**,
com **2042,99 ms** de wall total da requisição. Se fossem sequenciais o total seria ~4072 ms.

Isso importa por causa da margem contra o healthcheck do container, definido em
`DevOps-Cloud/docker-compose.yml` como `interval: 30s`, **`timeout: 5s`**, `retries: 5`,
`start_period: 60s`. **A margem depende inteiramente de qual caminho o check toma**, e os três
números abaixo vêm de medições distintas — cada um com a origem colada nele, de propósito:

| Caminho | Duração do `/health` | Margem contra `timeout: 5s` | Origem da medição |
|---|---|---|---|
| **Feliz** (Oracle e Luna vivos) | **0,93–88 ms** | ≈ **+4,9 s** | revisão independente do G4 |
| **Falha** (Oracle fora) | **8.027 ms** | **−3,03 s** — estoura | `A1` do G4, provado por `docker inspect .State.Health.Log`: `Health check exceeded timeout (5s)` |
| **Falha** (Oracle congelado, `docker pause`) | **~15,0 s** | estoura | revisão da `S3D-10` |

🔴 **Leia as três linhas juntas ou você tira a conclusão errada.** O check **não** estoura o
`timeout` no uso normal — sobra quase toda a janela. Ele estoura **quando o Oracle já está fora ou
travado**, ou seja, **quando `unhealthy` já é o veredito correto**. O efeito colateral é que o
`kura-api` fica `unhealthy` e **a Luna não sobe** (`depends_on: service_healthy`), o que transforma
um banco indisponível em duas indisponibilidades.

⚠️ **Correção de um número que este README afirmou por 4 sessões.** Até o G4, este parágrafo dizia
que *"o pior caso já **medido** do `/health` é ~4,1s — sobra ~0,9s"*. **Nunca foi medido.** O
`~4,1s` é o `~4072 ms` do parágrafo logo acima — o valor **contrafactual** de *"se fossem
sequenciais"*, que aquele mesmo parágrafo diz que **não acontece**, porque os checks rodam em
paralelo. Uma hipótese virou medição três linhas adiante e depois virou margem de segurança.
**É por isso que a tabela acima carrega a coluna "origem": sem proveniência colada no número,
hipótese e medição ficam tipograficamente idênticas.**

**Qualquer mudança que acrescente latência por requisição precisa ser medida antes e depois** —
e no **caminho feliz**, que é onde a margem de fato existe.

### Cold start no compose — o que é esperado e o que NÃO é verdade

Durante a subida do ecossistema, o Oracle XE demora (o `start_period` dele é de **180s**), e é
**esperado** que o `kura-api` reporte o check `oracle` como indisponível nesse intervalo. Isso
não é bug.

⚠️ **O que este README não afirma, porque foi medido e é falso:** que a convergência venha do
retry com backoff do `Program.cs`. Nas **duas** falhas de conexão medidas neste repositório
(listener ausente; serviço não registrado no listener) o driver lança **`ORA-50201`**, e o
código de rede real (`ORA-12514`/`ORA-12541`) aparece **dois níveis abaixo**, numa
`NetworkException` — que **não é `OracleException` e não expõe `.Number`**. O filtro
`when (retriableErrors.Contains(ex.Number))` não casa, e o bloco **não re-tenta** nesses
caminhos: observou-se **0 linhas** `"Oracle não disponível (ORA-…)"` e `RestartCount 10`. Quem
faz a stack convergir ali é a **restart policy do Docker**.

*Limite honesto desta afirmação:* só os 2 códigos de falha de conexão acima foram exercitados.
Os outros 2 da lista (`1109`, `17002`) **nunca foram**, e nada se afirma sobre eles.

### Frequência de polling sugerida

| Consumidor | Intervalo | Observação |
|---|---|---|
| Healthcheck do container | **30s** (já configurado) | `timeout: 5s` — ver a margem acima |
| Monitor externo / uptime | 30–60s | Abaixo de 30s não agrega: o check `luna` sozinho pode custar ~2s |

### Runbook — o que fazer quando cai

| Sintoma | Causa provável | Ação |
|---|---|---|
| `503`, `oracle` `Unhealthy` | Banco fora do ar, credencial errada ou `Data Source` errado | Confira o container do Oracle e a connection string. 🔴 **Não** fique re-tentando contra `oracle.fiap.com.br`: `ORA-01017` repetido vira `ORA-28000` e bloqueia a conta |
| `200`, `luna` `Degraded` | Luna fora do ar ou lenta (timeout de 3s) | A clínica segue operando. Verifique o serviço `luna-ai`; só transcrição e triagem ficam indisponíveis |
| `500` com `Luna:BaseUrl not configured.` | Config faltando | **Não é indisponibilidade** — é configuração. Defina `Luna:BaseUrl` (`Luna__BaseUrl`). Medido: a app sobe e só o `/health` quebra |
| `404` em `/health` | `MapKuraHealthChecks` não foi chamado | Regressão de fiação em `Program.cs`; coberta por teste de integração |

### OpenTelemetry — onde ver spans e métricas

Configurado em `src/Kura.Api/Extensions/ObservabilityExtensions.cs`, com **exporter Console**
(decisão travada: nunca Application Insights, por custo e pela regra *free-first*). Spans e
métricas saem no **stdout do processo** — em container:

```bash
docker compose logs -f kura-api
```

Recurso declarado uma única vez, válido para as duas pilhas: `service.name = Kura.Api`,
`service.version = 1.0.0`. Sem isso a SDK cairia no fallback `unknown_service:Kura.Api`.

| Instrumentação | Cobre |
|---|---|
| `AddAspNetCoreInstrumentation` | Borda de **entrada**: um span por requisição HTTP, e as métricas de duração/contagem por status |
| `AddHttpClientInstrumentation` | Borda de **saída**: chamadas via `HttpClient` (inclui o próprio health check da Luna) |
| `AddSource(KuraActivitySource.NomeFonte)` | Hierarquia **pai/filho entre as camadas do projeto** (`Kura.CrossCutting.Observability`) |

O `ActivitySource` próprio instrumenta **deliberadamente um fluxo representativo**, não o
projeto inteiro: `AgendaService.GetAgendaAsync` → `AgendaReadRepository.GetByIntervaloAsync`.

**Ausências deliberadas, para não parecerem esquecimento:**

- `OpenTelemetry.Instrumentation.EntityFrameworkCore` — **toda** a série publicada é
  prerelease; não existe tag estável. Além disso produziria span de *comando SQL*, não de
  fronteira arquitetural.
- `OpenTelemetry.Exporter.Prometheus.AspNetCore` — mesma razão (só prerelease). E a rota
  `/metrics` já é ocupada pelo `MetricsController`.

### 🔴 `/metrics` **não** são as métricas do OpenTelemetry

Nomes parecidos, coisas diferentes:

| | `GET /metrics` | Métricas do OpenTelemetry |
|---|---|---|
| O que é | Sobretudo **contagens de negócio** — `ambienteTotalClinicas`, `ambienteTotalPets`, `ambienteTotalEventos`, `ambienteTotalTriagensLuna` — mais `uptimeSeconds` e o nome do `ambiente` | **Desempenho por requisição**: duração e contagem por status, geradas pela instrumentação |
| O que **não** é | Não traz latência, throughput nem percentil algum | Não traz contagem de entidade de domínio |
| Onde sai | Corpo JSON da resposta HTTP | **stdout** do processo, via exporter Console |
| Quem serve | `MetricsController` | SDK do OpenTelemetry |
| Escopo | `/metrics` é do **ambiente inteiro** (`escopo: "ambiente"`, campos `ambiente*`); `/metrics/clinica` conta pets/eventos/triagens da clínica do JWT | Processo |

`uptimeSeconds` é a única coisa em `/metrics` que se parece com sinal de runtime, e vem de
`Environment.TickCount64` — não do OpenTelemetry.

---

## Índice de Artefatos — Avaliadores FIAP

Esta seção mapeia cada artefato técnico avaliável à sua localização exata no repositório.

| Artefato | Localização |
|---|---|
| **Controllers HTTP (API Layer)** | `src/Kura.Api/Controllers/` |
| **Middlewares e Filtros** | `src/Kura.Api/Middlewares/` · `src/Kura.Api/Filters/` |
| **Serviços de Aplicação** | `src/Kura.Application/Services/` |
| **DTOs e Validadores FluentValidation** | `src/Kura.Application/DTOs/` · `src/Kura.Application/Validators/` |
| **Entidades de Domínio** | `src/Kura.Domain/Entities/` |
| **Interfaces de Repositório e Exceções** | `src/Kura.Domain/Interfaces/` · `src/Kura.Domain/Exceptions/` |
| **DbContext e Configuração EF Core** | `src/Kura.Infrastructure/Persistence/KuraDbContext.cs` |
| **Implementações de Repositório** | `src/Kura.Infrastructure/Persistence/Repositories/` |
| **Fluent API — Mapeamento de Entidades** | `src/Kura.Infrastructure/Persistence/Configurations/` |
| **Interceptors (Read-only, Concorrência)** | `src/Kura.Infrastructure/Persistence/Interceptors/` |
| **Conversor Bool → CHAR(1)** | `src/Kura.Infrastructure/Persistence/Converters/BoolToSimNaoConverter.cs` |
| **Histórico de Migrations EF Core** (17 migrations; **evidência apenas** — o DDL é aplicado pelo Flyway) | `src/Kura.Infrastructure/Migrations/` |
| **Health Checks** (registro, writer JSON e checks da Luna e do Mongo) | `src/Kura.Api/Extensions/HealthCheckExtensions.cs` · `src/Kura.Api/HealthChecks/LunaHealthCheck.cs` · `src/Kura.Api/HealthChecks/MongoHealthCheck.cs` |
| **MongoDB — laudos** (contexto, repositório, runner, `$jsonSchema`, índices) | `src/Kura.Infrastructure/Mongo/` · `src/Kura.Infrastructure/Mongo/schema/` |
| **Paginação, ordenação e HAL** | `src/Kura.Api/Paginacao/` · `src/Kura.Api/Hal/` · `src/Kura.Domain/Paginacao/` |
| **Controllers v2 e de laudos** | `src/Kura.Api/Controllers/PetsV2Controller.cs` · `TutoresV2Controller.cs` · `AgendamentosV2Controller.cs` · `LaudosController.cs` |
| **OpenAPI exportado + teste que o trava** | `docs/openapi.json` · `tests/Kura.IntegrationTests/OpenApiDocumentoTests.cs` |
| **Observabilidade — OpenTelemetry** | `src/Kura.Api/Extensions/ObservabilityExtensions.cs` |
| **`ActivitySource` de tracing entre camadas** | `src/Kura.CrossCutting/Observability/` |
| **Testes de Unidade — Application** | `tests/Kura.Application.Tests/` |
| **Testes de Domínio** | `tests/Kura.Domain.Tests/` |
| **Testes de Política — Infrastructure** | `tests/Kura.Infrastructure.Tests/` |
| **Testes de Integração (HTTP, host real)** | `tests/Kura.IntegrationTests/` |
| **Convenção de testes Unit × Integration** | `tests/README.md` |
| **Configuração local (não versionada)** | `src/Kura.Api/appsettings.Development.json` |
| **Provisionamento Docker** | `Dockerfile` · `docker-compose.yml` |
| **Scripts de Deploy Azure (VM Linux)** | `docs/deploy/deploy-kura-vm.sh` |
| **Script de Teardown Azure** | `docs/deploy/teardown-kura.sh` |
| **Guia de Deploy** | `docs/deploy/README.md` |

---

## Variáveis de Ambiente

Consulte `.env.example` na raiz do repositório para o template completo.

Lista derivada das chaves que o código lê (`src/`), não do que já se documentou antes; as 3
linhas `Mongo__*` foram acrescentadas na S4-NET-02.

| Variável | Descrição | Falta dela quebra |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | String de conexão Oracle | Acesso a dados |
| `Jwt__Key` | Chave de assinatura JWT (mínimo 32 caracteres) | **O startup** — é a única lida de forma ansiosa, e a app não sobe sem ela |
| `Jwt__Issuer` | Emissor do token JWT | Validação do token |
| `Jwt__Audience` | Audiência do token JWT | Validação do token |
| `Jwt__ExpiryHours` | Validade do token, em horas. **Opcional** | Nada — o default é **8** (`AuthService`) |
| `IoT__ApiKey` | Autenticação dos dispositivos ESP32 | Os endpoints `/iot/*`, na 1ª chamada |
| `Luna__ApiKey` | Autenticação **de entrada** da Luna (header `X-Api-Key`) | Os 3 endpoints consumidos pela Luna, na 1ª chamada |
| `Luna__BaseUrl` | URL da Luna — usada pelo **health check** e pelo client de transcrição | **`GET /health` devolve `500`** (medido) |
| `Luna__InboundApiKey` | Chave **de saída** (.NET → Luna, transcrição) — direção oposta à `Luna__ApiKey` | A transcrição, na 1ª chamada |
| `Daily__ApiKey` | Chave da Daily.co (teleconsulta) | A teleconsulta, na 1ª chamada |
| `Storage__BasePath` | Pasta dos PDFs de receituário e dos binários de laudo. **Opcional** | Nada — cai no default `<dir da app>/storage/documentos` |
| `Mongo__ConnectionString` | Connection string do MongoDB dos laudos. **Opcional** — vazia = não configurado | Nada: a API sobe, `/health` mostra `mongo: Degraded` e as rotas de laudo devolvem `503` |
| `Mongo__Database` · `Mongo__InitEnabled` · `Mongo__ServerSelectionTimeoutSeconds` | Banco (padrão `kura_docs`), runner de schema/índices (padrão `true`) e timeout de seleção (padrão e teto 3 s). **Opcionais** | Nada |
| `ASPNETCORE_ENVIRONMENT` | `Development` · `Production` · `Testing` | Ver aviso abaixo |

> ⚠️ Exceto `Jwt__Key`, as chaves acima são lidas **preguiçosamente** (dentro de lambdas de
> `AddHttpClient`/filtros): a aplicação **sobe** sem elas e falha só quando o recurso
> correspondente é exercitado pela primeira vez. É por isso que um `/health` com `500` é um
> sintoma de **configuração**, não de indisponibilidade.

> `ASPNETCORE_ENVIRONMENT=Testing` faz o `Program.cs` **pular** o bloco de validação de
> migrations do startup (e expõe o Swagger, para o teste do OpenAPI) — é o que permite subir o host num teste de integração sem abrir
> conexão com o Oracle.

**URL de produção (Azure App Service):** `https://kura-api-fiap.azurewebsites.net`

---

## Equipe

| Membro | RM | Área de Responsabilidade |
|---|---|---|
| **Felipe Ferrete** *(Tech Lead)* | RM562999 | .NET · IoT · IA |
| **Nikolas Brisola** | RM564371 | Java · Backend Tutor |
| **Guilherme Sola** | RM563674 | Mobile Tutor · UX |
| **Gustavo Bosak** | RM566315 | Mobile Clínica · QA |
| **Clayton Alves** | RM562285 | DevOps · BD |

**Divisão técnica:** Felipe Ferrete atuou como arquiteto e desenvolvedor principal do componente .NET clínico, sendo responsável pelo design das quatro camadas Clean Architecture, implementação das configurações Fluent API, mapeamentos de entidades Oracle, restrições de tabelas read-only via interceptor, controle de concorrência otimista no `Agendamento`, integração IoT/ESP32 e hooks de sincronização cross-API com os backends Java e Python.
