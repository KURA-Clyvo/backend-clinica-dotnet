namespace Kura.Application.DTOs.Tutor;

public sealed class TutorUpdateDto
{
    public string NmTutor { get; init; } = string.Empty;
    public string NrCpf { get; init; } = string.Empty;
    public string DsEmail { get; init; } = string.Empty;
    public string NrTelefone { get; init; } = string.Empty;

    // G2 fix wave (KURA_BACKLOG_RECEPCAO.md, REC-01, achado Important #1 — LGPD): opcional.
    // Ausente e o WhatsApp atual era "o mesmo número" do telefone antigo (normalizado — R1a
    // da fix wave 2) ⇒ TutorService.UpdateAsync o acompanha automaticamente quando o telefone
    // muda; ausente e era diferente ⇒ mantém intocado; presente ⇒ normaliza e grava o valor
    // informado. Ver TutorService.
    //
    // R1d (G2b fix wave 2, achado Minor): "" (vazio) e "   " (whitespace) contam como
    // AUSENTE (mesma regra de string.IsNullOrWhiteSpace do restante do serviço), NÃO como
    // "apagar o WhatsApp existente". HOJE NÃO HÁ FORMA de limpar/remover o DsWhatsapp de um
    // tutor pelo PUT — só de trocá-lo por outro valor válido. Se um dia isso for necessário
    // (ex.: recepção quer desvincular o WhatsApp do telefone), precisa de um sinal explícito
    // diferente de string vazia (ex.: um campo boolean separado, ou um DTO de patch com
    // "campo presente vs ausente" tipado), não a reinterpretação de "".
    public string? DsWhatsapp { get; init; }
}
