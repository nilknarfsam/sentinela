using System.Diagnostics;
using System.Globalization;

namespace Sentinela.Servicos;

/// <summary>
/// Abre endereços da internet no navegador do usuário tentando não passar a
/// elevação do Sentinela para ele. O Sentinela roda como administrador; abrir a
/// URL direto com UseShellExecute faria o navegador rodar como administrador.
/// Passando a URL ao explorer.exe, no padrão do Windows o Explorer da sessão do
/// usuário (sem elevação) é quem abre o navegador.
/// Limites conhecidos: se "Iniciar janelas de pasta em processo separado"
/// estiver ligado, com o UAC desligado ou com o Administrador embutido, o
/// navegador ainda pode abrir elevado (como antes). Não é garantia absoluta.
/// </summary>
public static class NavegadorSeguro
{
    /// <summary>Só endereços https absolutos, sem espaços nas pontas, usuário/senha,
    /// aspas, caracteres de controle ou de formatação invisíveis.</summary>
    internal static bool UrlValida(string? url) => Normalizar(url) != null;

    internal static string? Normalizar(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url != url.Trim() || url.Contains('"')) return null;
        if (url.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format)) return null;
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) || string.IsNullOrEmpty(uri.Host)) return null;
        return uri.AbsoluteUri;
    }

    /// <summary>Monta a chamada do explorer.exe sem abrir nada (testável).</summary>
    internal static ProcessStartInfo? Montar(string? url)
    {
        var normalizada = Normalizar(url);
        if (normalizada == null) return null;
        var inicio = new ProcessStartInfo(CaminhosSistema.Explorer) { UseShellExecute = false };
        inicio.ArgumentList.Add(normalizada);
        return inicio;
    }

    /// <summary>
    /// Abre a URL. Nunca lança exceção; devolve false se a URL for recusada ou se
    /// o Explorer não puder ser iniciado (true não prova que o navegador abriu).
    /// Quem chama continua responsável pela lista branca de endereços permitidos.
    /// </summary>
    public static bool Abrir(string? url)
    {
        var inicio = Montar(url);
        if (inicio == null) return false;
        try
        {
            using var processo = Process.Start(inicio);
            return processo != null;
        }
        catch { return false; }
    }
}
