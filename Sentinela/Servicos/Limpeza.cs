using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Sentinela.Servicos;

/// <summary>Um grupo de arquivos descartáveis (temporários, lixeira...).</summary>
public sealed class ItemLimpeza : INotifyPropertyChanged
{
    public required string Nome { get; init; }
    public required string Descricao { get; init; }
    public string[] Pastas { get; init; } = [];
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
                    Pastas = [Path.GetTempPath()], HorasMinimas = 24 },
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
            return SHQueryRecycleBin(null, ref info) == 0 ? info.i64Size : 0;
        }
        long total = 0;
        foreach (var pasta in item.Pastas.Where(Directory.Exists))
            foreach (var f in Arquivos(pasta))
                try
                {
                    var fi = new FileInfo(f);
                    if (Velho(fi, item)) total += fi.Length;
                }
                catch { }
        return total;
    }

    /// <summary>Apaga o conteúdo das pastas do item. Devolve quantos bytes saíram.</summary>
    public static long Limpar(ItemLimpeza item)
    {
        if (item.EhLixeira)
        {
            var antes = Medir(item);
            // SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND (o painel já confirmou)
            SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4);
            return antes - Medir(item);
        }

        long liberado = 0;
        foreach (var pasta in item.Pastas.Where(Directory.Exists))
        {
            foreach (var f in Arquivos(pasta))
            {
                try
                {
                    var fi = new FileInfo(f);
                    if (!Velho(fi, item)) continue;
                    long tam = fi.Length;
                    if (fi.IsReadOnly) fi.IsReadOnly = false;
                    fi.Delete();
                    liberado += tam;
                }
                catch { /* em uso ou sem permissão: pula */ }
            }
            // Subpastas que ficaram vazias (de baixo para cima). A pasta raiz fica.
            try
            {
                foreach (var d in Directory.EnumerateDirectories(pasta, "*", new EnumerationOptions
                         { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint })
                         .OrderByDescending(d => d.Length))
                    try { if (!Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); } catch { }
            }
            catch { }
        }
        return liberado;
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

    private static IEnumerable<string> Arquivos(string pasta)
    {
        try
        {
            // Não segue atalhos de pasta (junctions), para nunca sair da pasta.
            return Directory.EnumerateFiles(pasta, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true, IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            }).ToList();
        }
        catch { return []; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? raiz, ref SHQUERYRBINFO info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? raiz, uint flags);
}
