namespace Kura.Domain.Interfaces;

/// <summary>
/// Monta a URL assinada de uma VARIANTE de foto de pet a partir da chave BASE gravada em
/// <c>Pet.DsFotoChave</c> — FT-04/backlog <c>KURA_BACKLOG_FOTO_PET.md</c>.
///
/// <para>Combina 3 peças que já existem separadas de propósito (SRP, ver o XML doc de
/// <see cref="IAssinadorUrlFoto"/> e o achado F7-b do G2, <c>g2-ft01-ft02.md</c>):
/// <see cref="Kura.Domain.Storage.ChaveFotoPet.Variante"/> (deriva a chave da variante),
/// <see cref="IAssinadorUrlFoto.Assinar"/> (calcula a <c>sig</c>) e a base HTTP (config ou
/// request atual) — nenhuma das três monta a URL final sozinha.</para>
///
/// <para>Vive em <c>Kura.Domain.Interfaces</c> pelo mesmo motivo de <see cref="IClinicaContext"/>
/// (ver <c>ClinicaContext</c>, em <c>Kura.Api.Services</c>): a implementação precisa do
/// request HTTP atual (<c>IHttpContextAccessor</c>) para a base derivada quando
/// <c>Foto:UrlBase</c> não está configurado — um tipo do framework web que
/// <c>Kura.Application</c> não pode referenciar (achado G2-e, <c>g2-ft03.md</c>) — então a
/// implementação mora em <c>Kura.Api</c>, não em <c>Kura.Infrastructure</c>.</para>
/// </summary>
public interface IGeradorUrlFotoPet
{
    /// <summary>
    /// Gera a URL assinada completa (<c>{base}/api/v1/fotos/{chaveVariante}?exp=...&amp;sig=...</c>)
    /// da variante <paramref name="sufixoTamanho"/> (<see cref="Kura.Domain.Storage.ChaveFotoPet.SufixoThumb"/>
    /// ou <see cref="Kura.Domain.Storage.ChaveFotoPet.SufixoMedia"/>) da chave base informada.
    /// </summary>
    /// <param name="chaveBase">
    /// <c>Pet.DsFotoChave</c>. <see langword="null"/> ou vazia ⇒ devolve <see langword="null"/>
    /// (pet sem foto) — nunca lança, porque "pet sem foto" é o estado mais comum, não uma
    /// exceção.
    /// </param>
    /// <param name="sufixoTamanho">Sufixo de tamanho da variante desejada.</param>
    string? GerarUrl(string? chaveBase, string sufixoTamanho);
}
