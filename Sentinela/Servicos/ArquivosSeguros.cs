using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Sentinela.Servicos;

public static class ArquivosSeguros
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string nome, uint acesso, uint compartilhamento,
        IntPtr seguranca, uint criacao, uint flags, IntPtr modelo);

    private static bool EhLink(FileSystemInfo item)
    {
        item.Refresh();
        return item.LinkTarget != null || (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0);
    }

    // Sem SHARE_DELETE: os ancestrais não podem ser trocados enquanto usamos o caminho.
    private sealed class Pastas : IDisposable
    {
        private readonly List<SafeFileHandle> _handles = [];
        public Pastas(string pasta)
        {
            try
            {
                var caminho = new DirectoryInfo(Path.GetFullPath(pasta));
                var limite = new[] { Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.ApplicationData }
                    .Select(p => Path.GetFullPath(Environment.GetFolderPath(p)).TrimEnd(Path.DirectorySeparatorChar))
                    .FirstOrDefault(p => caminho.FullName.Equals(p, StringComparison.OrdinalIgnoreCase)
                        || caminho.FullName.StartsWith(p + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                var pais = new Stack<DirectoryInfo>();
                for (var atual = caminho; atual != null; atual = atual.Parent)
                {
                    pais.Push(atual);
                    if (atual.FullName.Equals(limite, StringComparison.OrdinalIgnoreCase)) break;
                }
                while (pais.TryPop(out var atual))
                {
                    if (EhLink(atual)) throw new IOException("Pasta é um link.");
                    if (!atual.Exists) atual.Create();
                    var handle = CreateFile(atual.FullName, 0x80, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
                    _handles.Add(handle);
                    if (handle.IsInvalid || (File.GetAttributes(handle) & FileAttributes.ReparsePoint) != 0 || EhLink(atual))
                        throw new IOException($"Pasta insegura ou indisponível: {atual.FullName}, erro {Marshal.GetLastWin32Error()}.");
                }
            }
            catch { Dispose(); throw; }
        }
        public void Dispose() { foreach (var handle in _handles.AsEnumerable().Reverse()) handle.Dispose(); }
    }

    public static bool PastaSegura(string pasta)
    {
        try { using var pastas = new Pastas(pasta); return true; }
        catch { return false; }
    }

    public static string? LerTexto(string caminho, int maxBytes)
    {
        try
        {
            if (maxBytes < 0) return null;
            using var pastas = new Pastas(Path.GetDirectoryName(Path.GetFullPath(caminho))!);
            if (EhLink(new FileInfo(caminho))) return null;
            using var handle = CreateFile(caminho, 0x80000000, 3, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
            if (handle.IsInvalid || (File.GetAttributes(handle) & FileAttributes.ReparsePoint) != 0) return null;
            using var stream = new FileStream(handle, FileAccess.Read);
            if (stream.Length > maxBytes) return null;
            using var bytes = new MemoryStream();
            var buffer = new byte[8192];
            while (bytes.Length < maxBytes)
            {
                var lidos = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, maxBytes - bytes.Length));
                if (lidos == 0) break;
                bytes.Write(buffer, 0, lidos);
            }
            if (stream.ReadByte() != -1) return null;
            bytes.Position = 0;
            using var leitor = new StreamReader(bytes, Encoding.UTF8, true);
            return leitor.ReadToEnd();
        }
        catch { return null; }
    }

    public static bool GravarTexto(string caminho, string conteudo)
    {
        string? temporario = null;
        try
        {
            caminho = Path.GetFullPath(caminho);
            using var pastas = new Pastas(Path.GetDirectoryName(caminho)!);
            temporario = caminho + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var stream = new FileStream(temporario, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (EhLink(new FileInfo(temporario)) || (File.GetAttributes(stream.SafeFileHandle) & FileAttributes.ReparsePoint) != 0) return false;
                using var escritor = new StreamWriter(stream, new UTF8Encoding(false));
                escritor.Write(conteudo);
            }
            if (EhLink(new FileInfo(temporario))) return false;
            if (EhLink(new FileInfo(caminho))) File.Delete(caminho);
            // Move substitui a entrada do diretório, sem abrir nem escrever no alvo.
            File.Move(temporario, caminho, overwrite: true);
            temporario = null;
            return true;
        }
        catch { return false; }
        finally { try { if (temporario != null) File.Delete(temporario); } catch { } }
    }

    public static bool Acrescentar(string caminho, string linha, long maxBytes)
    {
        try
        {
            if (maxBytes < 0) return false;
            caminho = Path.GetFullPath(caminho);
            using var pastas = new Pastas(Path.GetDirectoryName(caminho)!);
            if (EhLink(new FileInfo(caminho))) return false;
            // Abre o próprio link e fixa o arquivo antes de usar FileMode.Append.
            using (var handle = CreateFile(caminho, 0x80000000, 3, IntPtr.Zero, 4, 0x00200000, IntPtr.Zero))
            {
                if (handle.IsInvalid || (File.GetAttributes(handle) & FileAttributes.ReparsePoint) != 0) return false;
                using var stream = new FileStream(handle, FileAccess.Read);
                if (stream.Length <= maxBytes)
                {
                    using var append = new FileStream(caminho, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    using var escritor = new StreamWriter(append, new UTF8Encoding(false));
                    escritor.Write(linha);
                    return true;
                }
            }
            var anterior = Path.Combine(Path.GetDirectoryName(caminho)!, "diario-anterior.jsonl");
            if (EhLink(new FileInfo(caminho))) return false;
            if (EhLink(new FileInfo(anterior))) File.Delete(anterior);
            File.Move(caminho, anterior, overwrite: true);
            return Acrescentar(caminho, linha, maxBytes);
        }
        catch { return false; }
    }
}
