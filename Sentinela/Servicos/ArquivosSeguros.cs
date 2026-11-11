using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Sentinela.Servicos;

public static class ArquivosSeguros
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string nome, uint acesso, uint compartilhamento,
        IntPtr seguranca, uint criacao, uint flags, IntPtr modelo);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle arquivo, StringBuilder caminho, uint tamanho, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle arquivo, uint controle, IntPtr entrada,
        uint tamanhoEntrada, byte[] saida, uint tamanhoSaida, out uint lidos, IntPtr sobreposto);

    // CLOUD e CLOUD_1 até CLOUD_F. Outras tags (inclusive junction/symlink) são recusadas.
    internal static bool TagDeNuvem(uint tag) => (tag & 0xFFFF0FFFu) == 0x9000001Au;

    private static void ConferirPasta(SafeFileHandle handle, bool aceitarNuvem)
    {
        if ((File.GetAttributes(handle) & FileAttributes.ReparsePoint) == 0) return;
        var dados = new byte[16 * 1024];
        if (!DeviceIoControl(handle, 0x000900A8, IntPtr.Zero, 0, dados, (uint)dados.Length, out var lidos, IntPtr.Zero) || lidos < 8)
            throw new IOException("Não consegui verificar o tipo de redirecionamento da pasta.");
        var tag = BitConverter.ToUInt32(dados);
        if (aceitarNuvem && TagDeNuvem(tag)) return;
        throw new IOException(tag is 0xA0000003 or 0xA000000C
            ? "A pasta é uma junção ou link simbólico. Escolha uma pasta sem redirecionamentos."
            : "A pasta tem um redirecionamento que não é de nuvem e não pode ser usado.");
    }

    private static bool EhLink(FileSystemInfo item)
    {
        item.Refresh();
        return item.LinkTarget != null || (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0);
    }

    // Sem SHARE_DELETE: os ancestrais não podem ser trocados enquanto usamos o caminho.
    private sealed class Pastas : IDisposable
    {
        private readonly List<SafeFileHandle> _handles = [];
        public Pastas(string pasta, bool todosAncestrais = false)
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
                    if (!todosAncestrais && atual.FullName.Equals(limite, StringComparison.OrdinalIgnoreCase)) break;
                }
                while (pais.TryPop(out var atual))
                {
                    if (!todosAncestrais && EhLink(atual)) throw new IOException("A pasta é um link ou junção.");
                    if (!atual.Exists) atual.Create();
                    var handle = CreateFile(atual.FullName, 0x80, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
                    _handles.Add(handle);
                    if (handle.IsInvalid)
                        throw new IOException($"Pasta insegura ou indisponível: {atual.FullName}, erro {Marshal.GetLastWin32Error()}.");
                    ConferirPasta(handle, todosAncestrais);
                    if (todosAncestrais && atual.Parent != null)
                    {
                        // Confere também o nome real, para não aceitar aliases curtos de pastas protegidas.
                        var real = new StringBuilder(32768);
                        var tamanho = GetFinalPathNameByHandle(handle, real, (uint)real.Capacity, 0);
                        if (tamanho == 0 || tamanho >= real.Capacity) throw new IOException("Não consegui confirmar o caminho real da pasta.");
                        var nomeReal = real.ToString();
                        if (nomeReal.StartsWith(@"\\?\")) nomeReal = nomeReal[4..];
                        if (MotivoDestinoExportacao(Path.Combine(nomeReal, "relatorio.html")) is { } motivo)
                            throw new IOException(motivo);
                    }
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

    public static bool GravarTexto(string caminho, string conteudo) => GravarTextoProtegido(caminho, conteudo, false);

    internal static string? MotivoDestinoExportacao(string caminho)
    {
        try
        {
            if (caminho.StartsWith(@"\\?\") || caminho.StartsWith(@"\\.\"))
                return "Caminhos de dispositivo (\\\\?\\ ou \\\\.\\) não são permitidos.";
            if (caminho.StartsWith(@"\\") || caminho.StartsWith("//")) return "Caminhos de rede (UNC) não são permitidos. Escolha uma unidade local.";
            if (caminho.Length < 3 || !char.IsAsciiLetter(caminho[0]) || caminho[1] != ':' || (caminho[2] != '\\' && caminho[2] != '/'))
                return "Escolha um caminho completo em uma unidade local.";
            if (caminho.IndexOf(':', 2) >= 0) return "Dois-pontos fora da letra da unidade não são permitidos (fluxo alternativo de arquivo).";
            if (caminho[3..].Split('\\', '/').Any(p => p is not ("." or "..") && (p.EndsWith('.') || p.EndsWith(' '))))
                return "Nomes terminados em ponto ou espaço não são permitidos. Escolha outro destino.";
            var completo = Path.GetFullPath(caminho);
            var pasta = Path.GetDirectoryName(completo);
            if (string.IsNullOrEmpty(pasta) || pasta.Equals(Path.GetPathRoot(completo), StringComparison.OrdinalIgnoreCase))
                return "Não salve na raiz da unidade. Escolha uma pasta para o relatório.";
            foreach (var especial in new[] { Environment.SpecialFolder.Windows, Environment.SpecialFolder.ProgramFiles,
                Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.CommonApplicationData })
            {
                var protegida = Environment.GetFolderPath(especial);
                if (string.IsNullOrEmpty(protegida)) continue;
                protegida = Path.GetFullPath(protegida).TrimEnd('\\', '/');
                if (completo.Equals(protegida, StringComparison.OrdinalIgnoreCase) || completo.StartsWith(protegida + "\\", StringComparison.OrdinalIgnoreCase))
                    return "Não é permitido exportar para uma pasta protegida do Windows ou dos programas: " + protegida;
            }
            if (!ExtensaoHtml(completo)) return File.Exists(completo)
                ? "Não é permitido sobrescrever um arquivo que não seja .html ou .htm."
                : "O relatório deve ter extensão .html ou .htm.";
            return null;
        }
        catch { return "O caminho escolhido é inválido."; }
    }

    private static bool ExtensaoHtml(string caminho) => Path.GetExtension(caminho).Equals(".html", StringComparison.OrdinalIgnoreCase)
        || Path.GetExtension(caminho).Equals(".htm", StringComparison.OrdinalIgnoreCase);

    public static bool GravarExportacao(string caminho, string conteudo) => GravarExportacao(caminho, conteudo, out _);

    public static bool GravarExportacao(string caminho, string conteudo, out string motivo)
    {
        motivo = "";
        try
        {
            if (MotivoDestinoExportacao(caminho) is { } recusa) { motivo = recusa; return false; }
            if (Encoding.UTF8.GetByteCount(conteudo) > 2 * 1024 * 1024) { motivo = "O relatório ultrapassa o limite de 2 MB."; return false; }
            return GravarTextoProtegido(caminho, conteudo, true, out motivo);
        }
        catch (Exception ex) { motivo = "Não consegui salvar o relatório: " + ex.Message; return false; }
    }

    private static bool GravarTextoProtegido(string caminho, string conteudo, bool exportacao) => GravarTextoProtegido(caminho, conteudo, exportacao, out _);

    private static bool GravarTextoProtegido(string caminho, string conteudo, bool exportacao, out string motivo)
    {
        motivo = "Não consegui gravar o arquivo. Confira a permissão da pasta.";
        string? temporario = null;
        try
        {
            caminho = Path.GetFullPath(caminho);
            using var pastas = new Pastas(Path.GetDirectoryName(caminho)!, exportacao);
            if (exportacao && EhLink(new FileInfo(caminho))) { motivo = "O arquivo de destino é um link ou tem redirecionamento. Escolha outro arquivo."; return false; }
            temporario = caminho + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var stream = new FileStream(temporario, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (EhLink(new FileInfo(temporario)) || (File.GetAttributes(stream.SafeFileHandle) & FileAttributes.ReparsePoint) != 0) return false;
                using var escritor = new StreamWriter(stream, new UTF8Encoding(false));
                escritor.Write(conteudo);
            }
            if (EhLink(new FileInfo(temporario))) return false;
            if (EhLink(new FileInfo(caminho)))
            {
                if (exportacao) throw new IOException("O arquivo de destino passou a ser um link ou redirecionamento. Escolha outro arquivo.");
                File.Delete(caminho);
            }
            // Move substitui a entrada do diretório, sem abrir nem escrever no alvo.
            File.Move(temporario, caminho, overwrite: true);
            temporario = null;
            return true;
        }
        catch (Exception ex) { motivo = "Não consegui salvar: " + ex.Message; return false; }
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
