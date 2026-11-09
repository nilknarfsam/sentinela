using System.Windows;
using System.Windows.Media;

namespace Sentinela.Servicos;

public static class Formatos
{
    public static Brush Cor(string chave) => (Brush)Application.Current.FindResource(chave);

    public static void Tentar(Action a)
    {
        try { a(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Sentinela"); }
    }

    public static string Quando(DateTime? d)
    {
        if (d is not { } v) return "—";
        var dias = (DateTime.Today - v.Date).Days;
        return dias switch
        {
            0 => $"hoje às {v:HH:mm}",
            1 => $"ontem às {v:HH:mm}",
            < 7 => $"há {dias} dias",
            _ => v.ToString("dd/MM/yyyy"),
        };
    }

    public static string Duracao(TimeSpan t)
        => t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}min" : t.TotalMinutes >= 1 ? $"{t.Minutes}min {t.Seconds:00}s" : $"{t.Seconds}s";
}
