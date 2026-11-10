using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Sentinela.Servicos;

/// <summary>Um grupo de arquivos descartáveis (temporários, lixeira...).</summary>
public sealed class ItemLimpeza : INotifyPropertyChanged
{
    public required string Nome { get; init; }
    public required string Descricao { get; set; }
    public string[] Pastas { get; init; } = [];
    public string? Processo { get; init; }
    public bool Disponivel { get; set; } = true;
    public bool EhLixeira { get; init; }
    public bool PrecisaAdmin { get; init; }
    /// <summary>Só apaga arquivos sem alteração há pelo menos estas horas (protege instaladores em andamento).</summary>
    public int HorasMinimas { get; init; }

    private bool _marcado = true;
    public bool Marcado { get => _marcado; set { _marcado = value; Avisar(); } }

    private long _bytes = -1;
    public long Bytes { get => _bytes; set { _bytes = value; Avisar(); Avisar(nameof(Tamanho)); } }
    public string Tamanho => _bytes < 0 ? "—" : Limpeza.Formatar(_bytes);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Avisar([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new(p));
}

/// <summary>
/// Limpa só arquivos que o Windows e os programas recriam sozinhos.
/// Arquivos em uso são pulados. Nada fora destas pastas é tocado.
/// </summary>
public static class Limpeza
{
    public static List<ItemLimpeza> Itens()
    {
        var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return
        [
            new() { Nome = "Temporários do usuário", Descricao = "Arquivos que os programas deixam para trás (%TEMP%).",
                    Pastas = [Path.Combine(local, "Temp")], HorasMinimas = 24 },
            new() { Nome = "Temporários do Windows", Descricao = "Pasta C:\\Windows\\Temp.",
                    Pastas = [Path.Combine(win, "Temp")], PrecisaAdmin = true, HorasMinimas = 24 },
            new() { Nome = "Downloads do Windows Update", Descricao = "Atualizações já instaladas. O Windows baixa de novo se precisar.",
                    Pastas = [Path.Combine(win, "SoftwareDistribution", "Download")], PrecisaAdmin = true },
            new() { Nome = "Relatórios de erro", Descricao = "Relatórios de travamentos já enviados ou antigos.",
                    Pastas = [Path.Combine(local, "Microsoft", "Windows", "WER"),
                              Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER")] },
            new() { Nome = "Lixeira", Descricao = "Esvazia a Lixeira. Isso não tem volta.", EhLixeira = true, Marcado = false },
        ];
    }

    public static long Medir(ItemLimpeza item)
    {
        if (item.EhLixeira)
        {
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
            try { return SHQueryRecycleBin(null, ref info) == 0 ? info.i64Size : 0; } catch { return 0; }
        }
        return item.Pastas.Sum(p => ArvoreDescartavel.Percorrer(p, false, aceitar: f => Velho(f, item)));
    }

    public static long Limpar(ItemLimpeza item, CancellationToken ct = default, ResultadoLimpeza? resultado = null)
    {
        try
        {
            if (ct.IsCancellationRequested || item.Pastas.Any(ArvoreDescartavel.PastaProibidaParaApagar)) return 0;
            if (item.Processo != null && !CachesNavegadores.Fechado(item.Processo)) return 0;
            if (item.EhLixeira)
            {
                var antes = Medir(item);
                SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4);
                return Math.Max(0, antes - Medir(item));
            }
            return item.Pastas.Sum(p => ArvoreDescartavel.Percorrer(p, true, ct, aceitar: f => Velho(f, item), resultado: resultado));
        }
        catch { return 0; }
    }
    public static string Formatar(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.##} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.#} MB",
        >= 1L << 10 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B",
    };

    internal static bool Velho(FileInfo fi, ItemLimpeza item)
        => item.HorasMinimas <= 0 || (DateTime.Now - fi.LastWriteTime).TotalHours >= item.HorasMinimas;

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? raiz, ref SHQUERYRBINFO info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? raiz, uint flags);
}
