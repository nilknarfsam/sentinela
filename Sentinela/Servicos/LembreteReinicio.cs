using Microsoft.Win32;

namespace Sentinela.Servicos;

public static class LembreteReinicio
{
    internal static bool DeveLembrar(bool pendente, TimeSpan tempoLigado, DateTime? ultimo, DateTime agora)
        => pendente && tempoLigado >= TimeSpan.FromDays(7) && (ultimo == null || ultimo.Value.Date < agora.Date);

    // Somente registro e contador monotônico; chamado pelo timer fora da thread da tela.
    internal static (bool Pendente, TimeSpan TempoLigado) Ler()
    {
        try
        {
            using var update = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            using var componentes = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            return (update != null || componentes != null, TimeSpan.FromMilliseconds(Environment.TickCount64));
        }
        catch { return (false, TimeSpan.Zero); }
    }
}
