namespace Kura.Domain.Storage;

/// <summary>
/// Fórmula ÚNICA e pública de chave de foto de pet — backlog <c>KURA_BACKLOG_FOTO_PET.md</c>,
/// regra A2, e ruling F7-a do maestro (G2 <c>g2-ft01-ft02.md</c>, FT-02/FT-03).
///
/// <para><b>Base:</b> <c>clinica/{idClinica}/pet/{idPet}/{uuid}.{ext}</c>, com
/// <c>ext</c> ∈ {<c>webp</c>, <c>jpg</c>, <c>png</c>} vindo do formato DETECTADO nos bytes do
/// arquivo (nunca do nome/Content-Type do cliente — ver
/// <c>Kura.Application.Services.ValidadorAssinaturaImagem</c>). Guardada inteira (com
/// extensão) em <c>Pet.DsFotoChave</c> — é a única coluna que representa a foto; sem a
/// extensão embutida na base, quem serve o arquivo depois (FT-04) não saberia qual extensão
/// abrir.</para>
///
/// <para><b>Variantes:</b> <c>{dir}/{uuid}_256.{ext}</c> (thumb) e
/// <c>{dir}/{uuid}_1080.{ext}</c> (media), derivadas de <see cref="Variante"/> inserindo o
/// sufixo de tamanho ANTES da extensão da base.</para>
///
/// <para><b>Único helper para esta fórmula</b> — FT-03 escreve, FT-04 lê para montar as URLs
/// derivadas, FT-05 (Java) replica byte a byte com âncora obrigatória (regra 11 do
/// CLAUDE.md) para este arquivo. Não duplicar a lógica de inserção do sufixo em outro
/// lugar.</para>
/// </summary>
public static class ChaveFotoPet
{
    /// <summary>Sufixo de tamanho da variante de lista/avatar (256px).</summary>
    public const string SufixoThumb = "256";

    /// <summary>Sufixo de tamanho da variante de detalhe (1080px).</summary>
    public const string SufixoMedia = "1080";

    /// <summary>
    /// Monta a chave BASE (com extensão) a partir dos identificadores de tenant/pet, um
    /// identificador único de arquivo (tipicamente <c>Guid.NewGuid():N</c>) e a extensão
    /// (sem ponto) derivada do formato detectado.
    /// </summary>
    public static string Base(long idClinica, long idPet, string identificadorUnico, string extensao) =>
        $"clinica/{idClinica}/pet/{idPet}/{identificadorUnico}.{extensao}";

    /// <summary>
    /// Deriva a chave de uma variante (ex.: <see cref="SufixoThumb"/>/<see cref="SufixoMedia"/>)
    /// a partir da chave base, inserindo <paramref name="sufixoTamanho"/> ANTES da extensão.
    /// Ex.: <c>Variante("clinica/7/pet/12/abc.webp", "256")</c> →
    /// <c>"clinica/7/pet/12/abc_256.webp"</c>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// 🔴 Fix wave G2 (g2-ft03.md, achado G2-f): <paramref name="chaveBase"/> sem ponto (sem
    /// extensão) LANÇA em vez de devolver uma chave sem extensão em silêncio. A versão
    /// anterior tinha um fallback silencioso ("defensivo; a base sempre tem extensão na
    /// prática") que a G2 apontou como risco para quem replicar esta fórmula (FT-05/Java,
    /// regra 11 do CLAUDE.md): uma chave sem extensão que sobrevive sem erro é pior do que
    /// falhar cedo, porque <see cref="Base"/> SEMPRE produz chave com extensão — chegar aqui
    /// sem ponto só pode significar uso indevido do helper (chave que não veio de
    /// <see cref="Base"/>).
    /// </exception>
    public static string Variante(string chaveBase, string sufixoTamanho)
    {
        var indicePonto = chaveBase.LastIndexOf('.');
        if (indicePonto < 0)
        {
            throw new ArgumentException(
                $"Chave base '{chaveBase}' não tem extensão — só chaves produzidas por " +
                $"{nameof(ChaveFotoPet)}.{nameof(Base)}() são aceitas.",
                nameof(chaveBase));
        }

        var semExtensao = chaveBase[..indicePonto];
        var extensao = chaveBase[(indicePonto + 1)..];
        return $"{semExtensao}_{sufixoTamanho}.{extensao}";
    }
}
