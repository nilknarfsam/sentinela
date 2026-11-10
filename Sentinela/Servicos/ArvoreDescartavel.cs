using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Sentinela.Servicos;

// Abre cada ancestral sem SHARE_DELETE e sem seguir reparse points.
internal sealed class TravaPastas : IDisposable
{
    private readonly List<SafeFileHandle> _handles = [];
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string nome, uint acesso, uint share, IntPtr seguranca, uint criacao, uint flags, IntPtr modelo);
    public TravaPastas(string pasta, bool soAtual = false)
    {
        try
        {
            var pais = new Stack<DirectoryInfo>();
            for (var d = new DirectoryInfo(Path.GetFullPath(pasta)); d != null; d = soAtual ? null : d.Parent) pais.Push(d);
            foreach (var d in pais)
            {
                var h = CreateFile(d.FullName, 0x80, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
                _handles.Add(h);
                if (h.IsInvalid) throw new IOException($"Não foi possível travar {d.FullName}: {Marshal.GetLastWin32Error()}");
                if ((File.GetAttributes(h) & FileAttributes.ReparsePoint) != 0) throw new IOException($"Pasta com reparse point: {d.FullName}");
            }
        }
        catch { Dispose(); throw; }
    }
    public void Dispose() { foreach (var h in _handles.AsEnumerable().Reverse()) h.Dispose(); }
}

internal static class ArvoreDescartavel
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHGetKnownFolderPath(in Guid id, uint flags, IntPtr token, out IntPtr caminho);

    private static string PastaDownloads()
    {
        SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out var caminho);
        try { return Marshal.PtrToStringUni(caminho) ?? throw new IOException(); }
        finally { Marshal.FreeCoTaskMem(caminho); }
    }

    internal static string Normalizar(string pasta) => Path.GetFullPath(pasta).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    internal static bool PastaProibidaParaApagar(string pasta)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pasta) || !Path.IsPathFullyQualified(pasta)) return true;
            // Caminhos de dispositivo não têm a mesma normalização dos caminhos comuns.
            if (pasta.StartsWith(@"\\?\", StringComparison.Ordinal) || pasta.StartsWith(@"\\.\", StringComparison.Ordinal)) return true;
            var p = Normalizar(pasta);
            if (string.Equals(p, Normalizar(Path.GetPathRoot(pasta)!), StringComparison.OrdinalIgnoreCase)) return true;
            Environment.SpecialFolder[] especiais = [Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyMusic,
                Environment.SpecialFolder.MyVideos, Environment.SpecialFolder.Windows, Environment.SpecialFolder.System,
                Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.CommonApplicationData,
                Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.ApplicationData];
            var protegidas = especiais.Select(Environment.GetFolderPath).Where(s => !string.IsNullOrWhiteSpace(s))
                .Append(PastaDownloads())
                .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"))
                .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32"));
            return protegidas.Any(s => string.Equals(p, Normalizar(s), StringComparison.OrdinalIgnoreCase));
        }
        catch { return true; }
    }

    internal static long Percorrer(string pasta, bool apagar, CancellationToken ct = default, Func<FileInfo, bool>? aceitar = null, bool removerLinks = false, bool ancestrais = true, ResultadoLimpeza? resultado = null, Action? aoEnumerar = null)
    {
        if (ct.IsCancellationRequested || (apagar && PastaProibidaParaApagar(pasta))) return 0;
        long total = 0;
        try
        {
            using var trava = new TravaPastas(pasta, soAtual: !ancestrais);
            aoEnumerar?.Invoke();
            foreach (var entrada in new DirectoryInfo(pasta).EnumerateFileSystemInfos())
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var caminho = entrada.FullName;
                    var atributos = entrada.Attributes;
                    var diretorio = (atributos & FileAttributes.Directory) != 0;
                    if (apagar && diretorio && PastaProibidaParaApagar(caminho)) continue;
                    if ((atributos & FileAttributes.ReparsePoint) != 0)
                    {
                        if (apagar && removerLinks) { if (diretorio) Directory.Delete(caminho); else File.Delete(caminho); }
                        continue;
                    }
                    if (diretorio)
                    {
                        total += Percorrer(caminho, apagar, ct, aceitar, removerLinks, ancestrais: false, resultado: resultado);
                        if (apagar && !ct.IsCancellationRequested && !Directory.EnumerateFileSystemEntries(caminho).Any()) Directory.Delete(caminho);
                    }
                    else
                    {
                        var fi = (FileInfo)entrada;
                        if (aceitar != null && !aceitar(fi)) continue;
                        // Metadados da enumeração: não abre o arquivo nem segue seu alvo.
                        long bytes = fi.Length;
                        if (ct.IsCancellationRequested) break;
                        if (apagar)
                        {
                            if ((atributos & FileAttributes.ReadOnly) != 0) fi.Attributes = atributos & ~FileAttributes.ReadOnly;
                            fi.Delete();
                        }
                        total += bytes;
                    }
                }
                catch { if (apagar) resultado?.RegistrarFalha(); }
            }
        }
        catch { if (apagar) resultado?.RegistrarFalha(); }
        return total;
    }
}

public sealed class ResultadoLimpeza
{
    public int NaoApagados { get; private set; }
    internal void RegistrarFalha() => NaoApagados++;
}

