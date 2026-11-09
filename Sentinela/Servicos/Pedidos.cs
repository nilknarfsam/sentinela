using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace Sentinela.Servicos;

public sealed record Pedido(string Acao, string? Caminho);

public static class Pedidos
{
    internal static string? PastaTeste;
    internal static string Pasta => PastaTeste ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinela", "pedidos");

    public static bool Enviar(Pedido p)
    {
        string? temporario = null;
        try
        {
            var pasta = Pasta;
            Directory.CreateDirectory(pasta);
            var arquivo = Path.Combine(pasta, Guid.NewGuid() + ".json");
            temporario = arquivo + ".tmp";
            File.WriteAllText(temporario, JsonSerializer.Serialize(p));
            File.Move(temporario, arquivo);
            return true;
        }
        catch { return false; }
        finally { try { if (temporario != null) File.Delete(temporario); } catch { } }
    }

    internal static bool CaminhoLocalValido(string caminho)
        => !string.IsNullOrWhiteSpace(caminho) && caminho.Length <= 1000
            && Regex.IsMatch(caminho, @"^[A-Za-z]:[\\/]")
            && !caminho.Any(char.IsControl)
            && caminho.IndexOfAny(['?', '*', '"', '<', '>', '|']) < 0
            && !caminho[2..].Contains(':');

    internal static Pedido? Validar(Pedido p)
        => p.Acao == "mostrar" ? p
            : p.Acao == "verificar" && p.Caminho != null
                && CaminhoLocalValido(p.Caminho) && Path.IsPathFullyQualified(p.Caminho) ? p : null;

    internal static Pedido? Interpretar(string json, DateTime criadoEm, DateTime agora)
    {
        try
        {
            if (agora - criadoEm > TimeSpan.FromMinutes(2)) return null;
            var p = JsonSerializer.Deserialize<Pedido>(json);
            return p == null ? null : Validar(p);
        }
        catch { return null; }
    }

    internal static bool UnidadePermitida(string caminho)
        => new DriveInfo(caminho[..3]).DriveType is DriveType.Fixed or DriveType.Removable;
}

public sealed class CaixaDePedidos : IDisposable
{
    private readonly object _trava = new();
    private Timer? _timer;
    private SafeFileHandle? _raizHandle;
    private SafeFileHandle? _pedidosHandle;
    private bool _iniciada;
    private volatile bool _descartada;
    private int _processando;
    private long _ultimoMostrar;
    public string? Erro { get; private set; }
    public event Action<Pedido>? Chegou;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string nome, uint acesso, uint compartilhamento,
        IntPtr seguranca, uint criacao, uint flags, IntPtr modelo);

    private static bool EhLink(FileSystemInfo item)
    {
        item.Refresh();
        return (item.Attributes & FileAttributes.ReparsePoint) != 0 || item.LinkTarget != null;
    }

    private static SafeFileHandle AbrirPasta(string pasta)
    {
        if (EhLink(new DirectoryInfo(pasta))) throw new IOException("A pasta de pedidos ou sua pasta pai é um link ou junction.");
        // BACKUP_SEMANTICS | OPEN_REPARSE_POINT, READ | WRITE, sem SHARE_DELETE.
        var handle = CreateFile(pasta, 0x80000000, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
        try
        {
            if ((File.GetAttributes(handle) & FileAttributes.ReparsePoint) != 0 || EhLink(new DirectoryInfo(pasta)))
                throw new IOException("A pasta de pedidos ou sua pasta pai é um link ou junction.");
            return handle;
        }
        catch { handle.Dispose(); throw; }
    }

    public void Iniciar()
    {
        lock (_trava)
        {
            if (_iniciada || _descartada) return;
            try
            {
                var pasta = Path.GetFullPath(Pedidos.Pasta);
                var raiz = Path.GetDirectoryName(pasta)!;
                // Confira antes de criar qualquer filho, para não atravessar uma junction.
                if (Directory.Exists(raiz) && EhLink(new DirectoryInfo(raiz)))
                    throw new IOException("A pasta pai dos pedidos é um link ou junction.");
                Directory.CreateDirectory(raiz);
                _raizHandle = AbrirPasta(raiz);
                if (Directory.Exists(pasta) && EhLink(new DirectoryInfo(pasta)))
                    throw new IOException("A pasta de pedidos é um link ou junction.");
                Directory.CreateDirectory(pasta);
                _pedidosHandle = AbrirPasta(pasta);
                if (EhLink(new DirectoryInfo(raiz)) || EhLink(new DirectoryInfo(pasta)))
                    throw new IOException("A pasta de pedidos ou sua pasta pai é um link ou junction.");
                _iniciada = true;
                Erro = null;
                // Um único consumidor; cada passagem limita o lote e descarta o excesso.
                _timer = new Timer(_ => ProcessarExistentes(pasta), null, 0, 250);
            }
            catch (Exception ex)
            {
                Erro = ex.Message;
                _pedidosHandle?.Dispose();
                _raizHandle?.Dispose();
            }
            finally
            {
                // Dispose pode ter atingido seu timeout enquanto Iniciar acessava o disco.
                if (_descartada) FecharRecursos();
            }
        }
    }

    private void ProcessarExistentes(string pasta)
    {
        lock (_trava)
        {
            if (_descartada || _processando != 0) return;
            _processando = 1;
        }
        try
        {
            var quantidade = 0;
            foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.json", SearchOption.TopDirectoryOnly))
            {
                if (_descartada) return;
                if (++quantidade > 20) { Apagar(arquivo); continue; }
                Processar(arquivo);
            }
        }
        catch { }
        finally
        {
            lock (_trava)
            {
                _processando = 0;
                if (_descartada) FecharRecursos();
            }
        }
    }

    private static void Apagar(string arquivo) { try { File.Delete(arquivo); } catch { } }

    private void Processar(string arquivo)
    {
        try
        {
            var info = new FileInfo(arquivo);
            if (EhLink(info) || info.Length > 4096) { Apagar(arquivo); return; }
            string json;
            // Abra o próprio link, caso alguém substitua o arquivo após a primeira checagem.
            using (var handle = CreateFile(arquivo, 0x80000000, 1, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero))
            {
                if (handle.IsInvalid) return;
                if ((File.GetAttributes(handle) & FileAttributes.ReparsePoint) != 0) return;
                using var stream = new FileStream(handle, FileAccess.Read);
                if (stream.Length > 4096) return;
                var bytes = new byte[4096];
                var tamanho = 0;
                while (tamanho < bytes.Length)
                {
                    var lidos = stream.Read(bytes, tamanho, bytes.Length - tamanho);
                    if (lidos == 0) break;
                    tamanho += lidos;
                }
                json = Encoding.UTF8.GetString(bytes, 0, tamanho).TrimStart('\uFEFF');
            }
            var pedido = Pedidos.Interpretar(json, info.CreationTimeUtc, DateTime.UtcNow);
            if (pedido == null || _descartada) return;
            if (pedido.Acao == "mostrar")
            {
                var agora = Environment.TickCount64;
                if (_ultimoMostrar != 0 && agora - _ultimoMostrar < 2000) return;
                _ultimoMostrar = agora;
            }
            Chegou?.Invoke(pedido);
        }
        catch { /* Arquivo indisponível ou consumidor com falha não derruba o app. */ }
        finally { Apagar(arquivo); }
    }

    private void FecharRecursos()
    {
        _timer?.Dispose();
        _timer = null;
        _pedidosHandle?.Dispose();
        _raizHandle?.Dispose();
    }

    public void Dispose()
    {
        _descartada = true;
        if (!Monitor.TryEnter(_trava, TimeSpan.FromSeconds(2))) return;
        try
        {
            _timer?.Dispose();
            _timer = null;
            // O leitor fecha os handles ao sair; Dispose nunca espera uma leitura.
            if (_processando == 0) FecharRecursos();
        }
        finally { Monitor.Exit(_trava); }
    }
}

