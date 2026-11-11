using System.Diagnostics;

namespace Sentinela.Servicos;

/// <summary>
/// Abre endereços da internet no navegador do usuário sem passar a elevação do
/// Sentinela para ele. O Sentinela roda como administrador; abrir a URL direto
/// com UseShellExecute faria o navegador inteiro rodar como administrador.
/// Passando a URL ao explorer.exe, o Explorer da sessão do usuário (sem
/// elevação) é quem abre o navegador.
/// </summary>
public static class NavegadorSeguro
{
    /// <summary>Só endereços https absolutos, sem usuário/senha embutidos.</summary>
    internal static bool UrlValida(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && !url.Any(char.IsControl)
        && !url.Contains('"')
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo)
        && !string.IsNullOrEmpty(uri.Host);

    /// <summary>
    /// Abre a URL. Nunca lança exceção; devolve false se a URL for recusada ou se
    /// o Explorer não puder ser iniciado. Quem chama continua responsável pela
    /// lista branca de endereços permitidos.
    /// </summary>
    public static bool Abrir(string? url)
    {
        if (!UrlValida(url)) return false;
        try
        {
            var inicio = new ProcessStartInfo(CaminhosSistema.Explorer) { UseShellExecute = false };
            inicio.ArgumentList.Add(url!);
            using var processo = Process.Start(inicio);
            return processo != null;
        }
        catch { return false; }
    }
}
