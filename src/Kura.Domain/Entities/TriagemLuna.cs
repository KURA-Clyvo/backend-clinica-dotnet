namespace Kura.Domain.Entities;

public class TriagemLuna : EntidadeBase
{
    public long IdClinica { get; set; }
    public long? IdTutor { get; set; }
    public long? IdPet { get; set; }

    // LU-08 (E18 — higiene): o comentário antigo aqui ("URGENTE | MODERADO | LEVE")
    // nunca bateu com o valor gravado de verdade. Os 3 valores reais são os que
    // TriageRequestValidator aceita (BAIXA|MEDIA|ALTA, espelhando o Literal do
    // Pydantic) — DS_NIVEL_URGENCIA não tem CHECK constraint no Oracle
    // (VARCHAR2(20) livre, V9__schema_drift_clinico.sql), então o validator de
    // aplicação é a única barreira real contra um valor fora desses 3.
    public string DsNivelUrgencia { get; set; } = string.Empty;  // BAIXA | MEDIA | ALTA
    public string DsDescricao { get; set; } = string.Empty;

    // D-L5 (LU-08): StEncaminhadoVet = DsUrgencia == "ALTA", decidido em
    // LunaService.RegistrarTriagemAsync — não é escrito em nenhum outro lugar.
    public bool StEncaminhadoVet { get; set; }
    public DateTime DtTriagem { get; set; }

    /// <summary>
    /// FK nullable para INTERACAO_CANAL (TASK-66/67) — liga a triagem à interação de
    /// canal que a originou. TriageRequestDTO da Luna sempre envia id_interacao, mas a
    /// coluna nasceu nullable porque TRIAGEM_LUNA é pré-existente (V9).
    /// </summary>
    public long? IdInteracao { get; set; }

    // LU-08 / LU-02 (V21, backend-tutor-java, em paralelo): 3 colunas novas, todas
    // NULLABLE no Oracle (NR_SCORE NUMBER(5), DS_SINTOMAS VARCHAR2(1000),
    // DS_REGRAS_VERSAO VARCHAR2(10)). Mapeadas aqui sem gerar DDL — o .NET não cria
    // migration contra Oracle (MIGRATIONS_POLICY.md); a V21 é quem realmente cria as
    // colunas. Até a V21 rodar no ambiente em uso, gravações aqui falham com
    // ORA-00904 (schema drift esperado, não bug desta task) — ver o roteiro de prova
    // Oracle no relatório desta task para o G4 do maestro.

    /// <summary>Score numérico da triagem (0-100 na convenção da Luna). Nullable: nem toda
    /// triagem histórica (pré-V21) tem valor.</summary>
    public int? NrScore { get; set; }

    /// <summary>
    /// Sintomas relatados, gravados como texto delimitado por <c>;</c> (documentado
    /// aqui de propósito — não é JSON, não é CSV). Truncado por BYTES UTF-8 em 1000
    /// via <c>LunaService.TruncarPorBytesUtf8</c> (mesmo raciocínio de
    /// DS_CONTEUDO/DS_DESCRICAO: NLS_LENGTH_SEMANTICS default do Oracle é BYTE, nunca
    /// truncar por caractere). <c>;</c> escolhido por não aparecer em nenhum sintoma
    /// do corpus da Luna (nomes curtos, sem pontuação) — se isso mudar, a leitura em
    /// TriagemLunaRepository.ListarPorClinicaAsync (Split por <c>;</c>) precisa mudar
    /// junto.
    /// </summary>
    public string? DsSintomas { get; set; }

    /// <summary>Versão do motor de regras da Luna que gerou esta triagem (ex.: "1.1").
    /// Opcional e retrocompatível — contrato de LU-07/LU-08.</summary>
    public string? DsRegrasVersao { get; set; }
}
