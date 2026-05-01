using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaIdClinicaNotificacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CLINICA",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_CLINICA.NEXTVAL"),
                    NM_CLINICA = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    NR_CNPJ = table.Column<string>(type: "NVARCHAR2(14)", maxLength: 14, nullable: false),
                    DS_ENDERECO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    NR_TELEFONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    DS_EMAIL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CLINICA", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ESPECIE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_ESPECIE.NEXTVAL"),
                    NM_ESPECIE = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ESPECIE", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "LOG_ERRO",
                columns: table => new
                {
                    ID_LOG = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_LOG_ERRO.NEXTVAL"),
                    DS_ENDPOINT = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    DS_METODO = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    DS_MENSAGEM = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    DS_STACK_TRACE = table.Column<string>(type: "CLOB", nullable: true),
                    NR_STATUS_CODE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DT_OCORRENCIA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOG_ERRO", x => x.ID_LOG);
                });

            migrationBuilder.CreateTable(
                name: "MEDICAMENTO",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_MEDICAMENTO.NEXTVAL"),
                    NM_MEDICAMENTO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    DS_PRINCIPIO_ATIVO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    DS_APRESENTACAO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MEDICAMENTO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TIPO_EVENTO",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_TIPO_EVENTO.NEXTVAL"),
                    CD_TIPO = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    NM_TIPO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TIPO_EVENTO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TUTOR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_TUTOR.NEXTVAL"),
                    NM_TUTOR = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    NR_CPF = table.Column<string>(type: "NVARCHAR2(11)", maxLength: 11, nullable: false),
                    DS_EMAIL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    NR_TELEFONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TUTOR", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DISPOSITIVO_IOT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_DISPOSITIVO_IOT.NEXTVAL"),
                    ID_CLINICA = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    CD_DISPOSITIVO = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    DS_DESCRICAO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    DS_LOCALIZACAO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DISPOSITIVO_IOT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DISPOSITIVO_IOT_CLINICA_ID_CLINICA",
                        column: x => x.ID_CLINICA,
                        principalTable: "CLINICA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VETERINARIO",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_VETERINARIO.NEXTVAL"),
                    ID_CLINICA = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    NM_VETERINARIO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    NR_CRMV = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    DS_EMAIL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    NR_TELEFONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VETERINARIO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_VETERINARIO_CLINICA_ID_CLINICA",
                        column: x => x.ID_CLINICA,
                        principalTable: "CLINICA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RACA",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_RACA.NEXTVAL"),
                    ID_ESPECIE = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    NM_RACA = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RACA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RACA_ESPECIE_ID_ESPECIE",
                        column: x => x.ID_ESPECIE,
                        principalTable: "ESPECIE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LEITURA_TEMPERATURA",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_LEITURA_TEMPERATURA.NEXTVAL"),
                    ID_DISPOSITIVO_IOT = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    VL_TEMPERATURA = table.Column<decimal>(type: "NUMBER(5,2)", nullable: false),
                    VL_UMIDADE = table.Column<decimal>(type: "NUMBER(5,2)", nullable: true),
                    DT_LEITURA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LEITURA_TEMPERATURA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_LEITURA_TEMPERATURA_DISPOSITIVO_IOT_ID_DISPOSITIVO_IOT",
                        column: x => x.ID_DISPOSITIVO_IOT,
                        principalTable: "DISPOSITIVO_IOT",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NOTIFICACAO",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_NOTIFICACAO.NEXTVAL"),
                    ID_CLINICA = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ID_TUTOR = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    ID_VETERINARIO = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    DS_TITULO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    DS_MENSAGEM = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    ST_LIDA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_LEITURA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOTIFICACAO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_NOTIFICACAO_TUTOR_ID_TUTOR",
                        column: x => x.ID_TUTOR,
                        principalTable: "TUTOR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NOTIFICACAO_VETERINARIO_ID_VETERINARIO",
                        column: x => x.ID_VETERINARIO,
                        principalTable: "VETERINARIO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PET",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_PET.NEXTVAL"),
                    ID_ESPECIE = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ID_RACA = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ID_VETERINARIO_RESP = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    NM_PET = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    DT_NASCIMENTO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    SG_SEXO = table.Column<string>(type: "CHAR(1)", nullable: false),
                    SG_PORTE = table.Column<string>(type: "CHAR(1)", nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PET", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PET_ESPECIE_ID_ESPECIE",
                        column: x => x.ID_ESPECIE,
                        principalTable: "ESPECIE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PET_RACA_ID_RACA",
                        column: x => x.ID_RACA,
                        principalTable: "RACA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ALERTA_TEMPERATURA",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_ALERTA_TEMPERATURA.NEXTVAL"),
                    ID_LEITURA_TEMPERATURA = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    DS_TIPO_ALERTA = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    VL_LIMITE = table.Column<decimal>(type: "NUMBER(5,2)", nullable: false),
                    DS_MENSAGEM = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    ST_RESOLVIDO = table.Column<string>(type: "CHAR(1)", nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ALERTA_TEMPERATURA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ALERTA_TEMPERATURA_LEITURA_TEMPERATURA_ID_LEITURA_TEMPERATURA",
                        column: x => x.ID_LEITURA_TEMPERATURA,
                        principalTable: "LEITURA_TEMPERATURA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EVENTO_CLINICO",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_EVENTO_CLINICO.NEXTVAL"),
                    ID_PET = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ID_VETERINARIO = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ID_TIPO_EVENTO = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    DT_EVENTO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DS_OBSERVACAO = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EVENTO_CLINICO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EVENTO_CLINICO_PET_ID_PET",
                        column: x => x.ID_PET,
                        principalTable: "PET",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EVENTO_CLINICO_TIPO_EVENTO_ID_TIPO_EVENTO",
                        column: x => x.ID_TIPO_EVENTO,
                        principalTable: "TIPO_EVENTO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EVENTO_CLINICO_VETERINARIO_ID_VETERINARIO",
                        column: x => x.ID_VETERINARIO,
                        principalTable: "VETERINARIO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TUTOR_PET",
                columns: table => new
                {
                    ID_TUTOR = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ID_PET = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    DS_VINCULO = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ST_PRINCIPAL = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_VINCULO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TUTOR_PET", x => new { x.ID_TUTOR, x.ID_PET });
                    table.ForeignKey(
                        name: "FK_TUTOR_PET_PET_ID_PET",
                        column: x => x.ID_PET,
                        principalTable: "PET",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TUTOR_PET_TUTOR_ID_TUTOR",
                        column: x => x.ID_TUTOR,
                        principalTable: "TUTOR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DOCUMENTO",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_DOCUMENTO.NEXTVAL"),
                    ID_EVENTO_CLINICO = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    NM_ARQUIVO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    DS_TIPO_MIME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    DS_CAMINHO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    NR_TAMANHO_BYTES = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DOCUMENTO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DOCUMENTO_EVENTO_CLINICO_ID_EVENTO_CLINICO",
                        column: x => x.ID_EVENTO_CLINICO,
                        principalTable: "EVENTO_CLINICO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EXAME",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_EXAME.NEXTVAL"),
                    ID_EVENTO_CLINICO = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    NM_EXAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    DS_RESULTADO = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    DT_REALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXAME", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EXAME_EVENTO_CLINICO_ID_EVENTO_CLINICO",
                        column: x => x.ID_EVENTO_CLINICO,
                        principalTable: "EVENTO_CLINICO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PRESCRICAO",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_PRESCRICAO.NEXTVAL"),
                    ID_EVENTO_CLINICO = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ID_MEDICAMENTO = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    DS_POSOLOGIA = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    NR_DURACAO_DIAS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRESCRICAO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PRESCRICAO_EVENTO_CLINICO_ID_EVENTO_CLINICO",
                        column: x => x.ID_EVENTO_CLINICO,
                        principalTable: "EVENTO_CLINICO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PRESCRICAO_MEDICAMENTO_ID_MEDICAMENTO",
                        column: x => x.ID_MEDICAMENTO,
                        principalTable: "MEDICAMENTO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VACINA",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_VACINA.NEXTVAL"),
                    ID_EVENTO_CLINICO = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    NM_VACINA = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    NR_LOTE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    DS_FABRICANTE = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    DT_PROXIMA_DOSE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VACINA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_VACINA_EVENTO_CLINICO_ID_EVENTO_CLINICO",
                        column: x => x.ID_EVENTO_CLINICO,
                        principalTable: "EVENTO_CLINICO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ALERTA_TEMPERATURA_ID_LEITURA_TEMPERATURA",
                table: "ALERTA_TEMPERATURA",
                column: "ID_LEITURA_TEMPERATURA");

            migrationBuilder.CreateIndex(
                name: "IX_CLINICA_NR_CNPJ",
                table: "CLINICA",
                column: "NR_CNPJ",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DISPOSITIVO_IOT_CD_DISPOSITIVO",
                table: "DISPOSITIVO_IOT",
                column: "CD_DISPOSITIVO",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DISPOSITIVO_IOT_ID_CLINICA",
                table: "DISPOSITIVO_IOT",
                column: "ID_CLINICA");

            migrationBuilder.CreateIndex(
                name: "IX_DOCUMENTO_ID_EVENTO_CLINICO",
                table: "DOCUMENTO",
                column: "ID_EVENTO_CLINICO");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTO_CLINICO_ID_PET",
                table: "EVENTO_CLINICO",
                column: "ID_PET");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTO_CLINICO_ID_TIPO_EVENTO",
                table: "EVENTO_CLINICO",
                column: "ID_TIPO_EVENTO");

            migrationBuilder.CreateIndex(
                name: "IX_EVENTO_CLINICO_ID_VETERINARIO",
                table: "EVENTO_CLINICO",
                column: "ID_VETERINARIO");

            migrationBuilder.CreateIndex(
                name: "IX_EXAME_ID_EVENTO_CLINICO",
                table: "EXAME",
                column: "ID_EVENTO_CLINICO");

            migrationBuilder.CreateIndex(
                name: "IX_LEITURA_TEMPERATURA_ID_DISPOSITIVO_IOT",
                table: "LEITURA_TEMPERATURA",
                column: "ID_DISPOSITIVO_IOT");

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICACAO_ID_TUTOR",
                table: "NOTIFICACAO",
                column: "ID_TUTOR");

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICACAO_ID_VETERINARIO",
                table: "NOTIFICACAO",
                column: "ID_VETERINARIO");

            migrationBuilder.CreateIndex(
                name: "IX_PET_ID_ESPECIE",
                table: "PET",
                column: "ID_ESPECIE");

            migrationBuilder.CreateIndex(
                name: "IX_PET_ID_RACA",
                table: "PET",
                column: "ID_RACA");

            migrationBuilder.CreateIndex(
                name: "IX_PRESCRICAO_ID_EVENTO_CLINICO",
                table: "PRESCRICAO",
                column: "ID_EVENTO_CLINICO");

            migrationBuilder.CreateIndex(
                name: "IX_PRESCRICAO_ID_MEDICAMENTO",
                table: "PRESCRICAO",
                column: "ID_MEDICAMENTO");

            migrationBuilder.CreateIndex(
                name: "IX_RACA_ID_ESPECIE",
                table: "RACA",
                column: "ID_ESPECIE");

            migrationBuilder.CreateIndex(
                name: "IX_TIPO_EVENTO_CD_TIPO",
                table: "TIPO_EVENTO",
                column: "CD_TIPO",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TUTOR_NR_CPF",
                table: "TUTOR",
                column: "NR_CPF",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TUTOR_PET_ID_PET",
                table: "TUTOR_PET",
                column: "ID_PET");

            migrationBuilder.CreateIndex(
                name: "IX_VACINA_ID_EVENTO_CLINICO",
                table: "VACINA",
                column: "ID_EVENTO_CLINICO");

            migrationBuilder.CreateIndex(
                name: "IX_VETERINARIO_ID_CLINICA",
                table: "VETERINARIO",
                column: "ID_CLINICA");

            migrationBuilder.CreateIndex(
                name: "IX_VETERINARIO_NR_CRMV",
                table: "VETERINARIO",
                column: "NR_CRMV",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ALERTA_TEMPERATURA");

            migrationBuilder.DropTable(
                name: "DOCUMENTO");

            migrationBuilder.DropTable(
                name: "EXAME");

            migrationBuilder.DropTable(
                name: "LOG_ERRO");

            migrationBuilder.DropTable(
                name: "NOTIFICACAO");

            migrationBuilder.DropTable(
                name: "PRESCRICAO");

            migrationBuilder.DropTable(
                name: "TUTOR_PET");

            migrationBuilder.DropTable(
                name: "VACINA");

            migrationBuilder.DropTable(
                name: "LEITURA_TEMPERATURA");

            migrationBuilder.DropTable(
                name: "MEDICAMENTO");

            migrationBuilder.DropTable(
                name: "TUTOR");

            migrationBuilder.DropTable(
                name: "EVENTO_CLINICO");

            migrationBuilder.DropTable(
                name: "DISPOSITIVO_IOT");

            migrationBuilder.DropTable(
                name: "PET");

            migrationBuilder.DropTable(
                name: "TIPO_EVENTO");

            migrationBuilder.DropTable(
                name: "VETERINARIO");

            migrationBuilder.DropTable(
                name: "RACA");

            migrationBuilder.DropTable(
                name: "CLINICA");

            migrationBuilder.DropTable(
                name: "ESPECIE");
        }
    }
}
