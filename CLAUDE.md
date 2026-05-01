# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run API
dotnet run --project ./src/Kura.Api

# Tests
dotnet test
dotnet test --filter "FullyQualifiedName~ServiceName"   # single test class

# EF Core migrations
dotnet ef migrations add <MigrationName> --project src/Kura.Infrastructure --startup-project src/Kura.Api
dotnet ef database update --project src/Kura.Infrastructure --startup-project src/Kura.Api

# Docker
docker-compose up
```

## Architecture

4-layer Clean Architecture: `Api → Application → Domain ← Infrastructure`

- **Domain** — entities and repository interfaces only; zero references to Infrastructure, EF Core, or ASP.NET
- **Application** — services (orchestration), DTOs, FluentValidation validators
- **Infrastructure** — EF Core `KuraDbContext`, repository implementations, Fluent API configurations in `Persistence/Configurations/`
- **Api** — thin controllers that only route HTTP; all logic lives in Application services

Reference layout:
```
src/
  Kura.Api/
  Kura.Application/
  Kura.Domain/
  Kura.Infrastructure/
tests/
  Kura.Application.Tests/
  Kura.Domain.Tests/
```

## Key Patterns

- **Repository + Unit of Work** — `IRepository<T>` / `IUnitOfWork` defined in Domain, implemented in Infrastructure
- **Soft delete** — all entities expose `ST_ATIVA`; filtered globally via `HasQueryFilter`, never hard-deleted
- **DTOs** — always separate from domain entities (`CreateDto`, `UpdateDto`, `ResponseDto`)
- **Validation** — FluentValidation only, not DataAnnotations
- **Error handling** — global `ExceptionHandlerMiddleware` writes to `LOG_ERRO` table; no local try/catch except for domain rules
- **Multi-tenancy** — every query implicitly filters by `ID_CLINICA` from the JWT claim via `IClinicaContext` + EF interceptor
- **Async** — every I/O call (`await`) without exception
- **Nullable reference types** — enabled; treat warnings as errors

## Database

Oracle 19c. Critical constraints:
- All table/column identifiers **UPPERCASE** in Fluent API configurations
- Explicit sequences for primary key generation (no `IDENTITY`)
- camelCase in JSON responses, snake_case in Oracle column names
- `.NET writes` the clinic domain tables (Pet, EventoClinico, Vacina, etc.)
- `.NET reads only` three external tables: `CONTA_TUTOR`, `CONSENTIMENTO`, `AGENDAMENTO`
- Connection string lives in `appsettings.Development.json` (never committed)

## Domain Model (core entities)

`Clinica`, `Veterinario`, `Pet`, `Tutor` (N:N via `TUTOR_PET`), `EventoClinico`, `Vacina`, `Prescricao`, `Exame`, `Especie`, `Raca`, `IoTLeitura`, `Notificacao`, `Documento`, `LogErro`, `Alerta`

`EventoClinico` is the base type; `Vacina`, `Prescricao`, and `Exame` are subtypes — creating one requires a transaction that writes both rows atomically.

## Authentication

JWT with claims `clinicaId` and `veterinarioId`. IoT ingestion endpoint (`POST /iot/leituras`) uses API key auth instead of JWT.
