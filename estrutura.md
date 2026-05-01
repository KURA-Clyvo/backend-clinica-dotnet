KuraApi/
├── src/
│   ├── Kura.Api/                          # Camada de apresentação
│   │   ├── Controllers/
│   │   │   ├── ClinicasController.cs
│   │   │   ├── VeterinariosController.cs
│   │   │   ├── PetsController.cs
│   │   │   ├── TutoresController.cs
│   │   │   ├── EventosClinicosController.cs
│   │   │   ├── VacinasController.cs
│   │   │   ├── PrescricoesController.cs
│   │   │   ├── ExamesController.cs
│   │   │   ├── DashboardController.cs
│   │   │   ├── IotController.cs
│   │   │   └── NotificacoesController.cs
│   │   ├── Middlewares/
│   │   │   └── ExceptionHandlerMiddleware.cs   # captura exceção e grava LOG_ERRO
│   │   ├── Extensions/
│   │   │   └── ServiceCollectionExtensions.cs  # AddApplication(), AddInfrastructure()
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Program.cs
│   │   ├── Dockerfile
│   │   └── Kura.Api.csproj
│   │
│   ├── Kura.Application/                  # Casos de uso
│   │   ├── DTOs/
│   │   │   ├── Pets/
│   │   │   │   ├── PetCreateDto.cs
│   │   │   │   ├── PetUpdateDto.cs
│   │   │   │   └── PetResponseDto.cs
│   │   │   ├── EventosClinicos/
│   │   │   │   ├── EventoClinicoCreateDto.cs
│   │   │   │   ├── VacinaCreateDto.cs        # extends EventoClinicoCreateDto
│   │   │   │   └── ...
│   │   │   └── Common/
│   │   │       ├── PagedResultDto.cs
│   │   │       └── ErrorResponseDto.cs
│   │   ├── Services/
│   │   │   ├── Interfaces/
│   │   │   │   ├── IPetService.cs
│   │   │   │   ├── IEventoClinicoService.cs
│   │   │   │   └── ...
│   │   │   ├── PetService.cs
│   │   │   ├── EventoClinicoService.cs
│   │   │   └── DashboardService.cs
│   │   ├── Validators/
│   │   │   ├── PetCreateValidator.cs
│   │   │   └── ...
│   │   └── Kura.Application.csproj
│   │
│   ├── Kura.Domain/                       # Núcleo
│   │   ├── Entities/
│   │   │   ├── Clinica.cs
│   │   │   ├── Veterinario.cs
│   │   │   ├── Tutor.cs
│   │   │   ├── Pet.cs
│   │   │   ├── Especie.cs
│   │   │   ├── Raca.cs
│   │   │   ├── EventoClinico.cs
│   │   │   ├── TipoEvento.cs
│   │   │   ├── Vacina.cs
│   │   │   ├── Prescricao.cs
│   │   │   ├── Medicamento.cs
│   │   │   ├── Exame.cs
│   │   │   ├── Documento.cs
│   │   │   ├── Notificacao.cs
│   │   │   ├── DispositivoIot.cs
│   │   │   ├── LeituraTemperatura.cs
│   │   │   └── AlertaTemperatura.cs
│   │   ├── Enums/
│   │   │   ├── PortePet.cs                # Pequeno, Medio, Grande
│   │   │   ├── StatusAtivo.cs             # 'S' | 'N'
│   │   │   └── TipoEventoEnum.cs
│   │   ├── Interfaces/
│   │   │   ├── IRepository.cs             # genérico
│   │   │   ├── IPetRepository.cs          # específico
│   │   │   ├── IEventoClinicoRepository.cs
│   │   │   └── IUnitOfWork.cs
│   │   ├── Exceptions/
│   │   │   ├── DomainException.cs
│   │   │   ├── EntidadeNaoEncontradaException.cs
│   │   │   └── RegraDeNegocioException.cs
│   │   └── Kura.Domain.csproj
│   │
│   └── Kura.Infrastructure/               # Adaptadores externos
│       ├── Persistence/
│       │   ├── KuraDbContext.cs
│       │   ├── Configurations/            # Fluent API mapping
│       │   │   ├── ClinicaConfiguration.cs
│       │   │   ├── PetConfiguration.cs
│       │   │   └── ...
│       │   ├── Repositories/
│       │   │   ├── Repository.cs          # base genérico
│       │   │   ├── PetRepository.cs
│       │   │   └── ...
│       │   └── UnitOfWork.cs
│       ├── Logging/
│       │   └── OracleLogErroSink.cs       # sink Serilog que escreve em LOG_ERRO
│       ├── Migrations/                    # EF Core migrations
│       └── Kura.Infrastructure.csproj
│
├── tests/
│   ├── Kura.Application.Tests/
│   └── Kura.Domain.Tests/
│
├── docker-compose.yml                     # API + Oracle XE local p/ dev
├── .dockerignore
├── .gitignore
├── README.md                              # exigido pela FIAP
├── KuraApi.sln
└── docs/
    ├── architecture.md
    └── endpoints.md