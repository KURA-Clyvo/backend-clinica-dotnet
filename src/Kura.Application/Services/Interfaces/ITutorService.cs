namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Luna;
using Kura.Application.DTOs.Pet;
using Kura.Application.DTOs.Tutor;

public interface ITutorService
{
    Task<IEnumerable<TutorResponseDto>> SearchAsync(string? busca);
    Task<TutorResponseDto> GetByIdAsync(long id);
    Task<IEnumerable<PetResponseDto>> GetPetsAsync(long id);
    Task<TutorComInviteResponseDto> CreateAsync(TutorCreateDto dto, long clinicaId);
    Task<TutorResponseDto> UpdateAsync(long id, TutorUpdateDto dto);
    Task SoftDeleteAsync(long id);

    /// <summary>
    /// REC-02 (KURA_BACKLOG_RECEPCAO.md): reemite o convite de onboarding de um tutor —
    /// gera um novo token (7 dias), cancela (soft delete) todo invite ativo e ainda não
    /// utilizado do mesmo tutor, e devolve o novo link. A-7: escopo por clínica à mão via
    /// <c>ITutorRepository.GetByIdAsync(id, idClinica)</c> — tutor de outra clínica ou
    /// inativo devolve o MESMO 404 de tutor inexistente (sem oráculo de existência).
    /// Tutor que já concluiu o onboarding (existe <c>CONTA_TUTOR</c>) ⇒ 409.
    /// </summary>
    Task<InviteTutorReemitidoResponseDto> ReemitirConviteAsync(long id, long clinicaId);

    /// <summary>
    /// TASK-67: GET /api/v1/tutores/telefone/{numero} — consumido pela IA Luna para
    /// resolver clínica + pets a partir do WhatsApp, antes de registrar interações.
    /// Deliberadamente sem escopo de clínica (ver ITutorRepository.GetByTelefoneAsync).
    /// Retorna null se não houver tutor ativo com esse telefone (controller mapeia
    /// para 404) — e também se houver MAIS DE UM tutor ativo com esse telefone,
    /// qualquer clínica, inclusive dois tutores da MESMA clínica (TASK-79): tratado
    /// como não encontrado em vez de devolver um tutor arbitrário (potencialmente de
    /// clínica errada). Consequência: aquele telefone recebe o fallback genérico da
    /// Luna e a interação é gravada com clínica/tutor nulos.
    /// </summary>
    Task<TutorContextoLunaDto?> BuscarContextoPorTelefoneAsync(string numero);
}
