using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Sentinela.Servicos;

public sealed record InfoProcesso(int Pid, string Nome, long Inicio, string? Caminho, string? Editora, double CpuPercent, long MemoriaBytes, bool Protegido, string? MotivoProtegido);
public sealed record GrupoProcessos(string Nome, string? Editora, double CpuPercent, long MemoriaBytes, IReadOnlyList<InfoProcesso> Processos) : INotifyPropertyChanged
{
    public bool Protegido => Processos.Any(p => p.Protegido);
    private bool _finalizando;
    public bool Finalizando
    {
        get => _finalizando;
        set
        {
            _finalizando = value;
            PropertyChanged?.Invoke(this, new(nameof(PodeFinalizar)));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool PodeFinalizar => !Protegido && !Finalizando;
    public string Dica => Protegido ? $"Protegido: {Processos.First(p => p.Protegido).MotivoProtegido}" : "Finalizar os processos deste programa";
    public string CpuTexto => $"{CpuPercent:0.0}%";
    public string MemoriaTexto => Limpeza.Formatar(MemoriaBytes);
}

public static class Processos
{
    public static Task<IReadOnlyList<InfoProcesso>> Amostrar(CancellationToken ct = default) => Amostrar(TimeSpan.FromSeconds(2), ct);

    public static Task<IReadOnlyList<InfoProcesso>> Amostrar(TimeSpan intervalo, CancellationToken ct, bool lerEditoras = true) => Task.Run(async () =>
    {
        try
        {
            if (ct.IsCancellationRequested) return (IReadOnlyList<InfoProcesso>)Array.Empty<InfoProcesso>();
            if (intervalo <= TimeSpan.Zero) intervalo = TimeSpan.FromSeconds(2);
            var primeira = Ler(false, ct);
            var inicio = Stopwatch.GetTimestamp();
            await Task.Delay(intervalo, ct).ConfigureAwait(false);
            var segunda = Ler(true, ct);
            var decorrido = Stopwatch.GetElapsedTime(inicio);
            var lista = segunda.Select(p =>
            {
                var cpu = primeira.TryGetValue(p.Key, out var anterior) &&
                    Reconferir(anterior.info.Nome, anterior.info.Inicio, p.Value.info.Nome, p.Value.info.Inicio)
                    ? CalcularCpu(anterior.cpu, p.Value.cpu, decorrido, Environment.ProcessorCount) : 0;
                return p.Value.info with { CpuPercent = cpu };
            }).ToArray();
            // Inclui todos os membros dos grupos candidatos, mesmo os de baixo consumo.
            var grupos = Agrupar(lista);
            var candidatos = grupos.OrderByDescending(g => g.CpuPercent).ThenByDescending(g => g.MemoriaBytes).Take(30)
                .Concat(grupos.OrderByDescending(g => g.MemoriaBytes).ThenByDescending(g => g.CpuPercent).Take(30))
                .SelectMany(g => g.Processos).Select(p => p.Pid).ToHashSet();
            var editoras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            for (var i = 0; i < lista.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                var p = lista[i];
                if (!candidatos.Contains(p.Pid)) continue;
                var caminho = LerCaminho(p.Pid);
                var (protegido, motivo) = Classificar(p.Pid, p.Nome, caminho, Environment.ProcessId, windir);
                lista[i] = p with { Caminho = caminho, Protegido = protegido, MotivoProtegido = motivo };
            }
            // O checkup exibe nome/CPU/memória, sem editora; não precisa verificar assinaturas.
            if (lerEditoras)
            {
                grupos = Agrupar(lista);
                var exibidos = grupos.OrderByDescending(g => g.CpuPercent).ThenByDescending(g => g.MemoriaBytes).Take(10)
                    .Concat(grupos.OrderByDescending(g => g.MemoriaBytes).ThenByDescending(g => g.CpuPercent).Take(10))
                    .SelectMany(g => g.Processos).Select(p => p.Pid).ToHashSet();
                for (var i = 0; i < lista.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var p = lista[i];
                    if (!exibidos.Contains(p.Pid) || p.Caminho == null) continue;
                    if (!editoras.TryGetValue(p.Caminho, out var editora))
                        editoras[p.Caminho] = editora = Assinaturas.LerComando($"\"{p.Caminho}\"").Editora;
                    lista[i] = p with { Editora = editora };
                }
            }
            ct.ThrowIfCancellationRequested();
            return (IReadOnlyList<InfoProcesso>)Array.AsReadOnly(lista);
        }
        catch { return Array.Empty<InfoProcesso>(); }
    });

    private static Dictionary<int, (InfoProcesso info, TimeSpan? cpu)> Ler(bool lerMemoria, CancellationToken ct)
    {
        var lista = new Dictionary<int, (InfoProcesso, TimeSpan?)>();
        var processos = Process.GetProcesses();
        try
        {
            foreach (var processo in processos)
            {
                ct.ThrowIfCancellationRequested();
                int pid;
                try { pid = processo.Id; } catch { continue; }
                string nome = $"PID {pid}";
                long memoria = 0, inicio = 0;
                TimeSpan? cpu = null;
                try { nome = processo.ProcessName; } catch { }
                try { cpu = processo.TotalProcessorTime; } catch { }
                try { inicio = processo.StartTime.ToUniversalTime().Ticks; } catch { }
                if (lerMemoria)
                {
                    try { memoria = processo.WorkingSet64; } catch { }
                    try { memoria = processo.PrivateMemorySize64; } catch { }
                }
                lista[pid] = (new(pid, nome, inicio, null, null, 0, memoria, true, "processo fora dos candidatos da amostra"), cpu);
            }
        }
        finally { foreach (var processo in processos) processo.Dispose(); }
        return lista;
    }

    private static string? LerCaminho(int pid)
    {
        using var handle = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
        if (handle.IsInvalid) return null;
        var caminho = new StringBuilder(32768);
        var tamanho = caminho.Capacity;
        return QueryFullProcessImageName(handle, 0, caminho, ref tamanho) ? caminho.ToString() : null;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint acesso, [MarshalAs(UnmanagedType.Bool)] bool herdar, int pid);

    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle processo, uint flags, StringBuilder caminho, ref int tamanho);

    internal static bool Reconferir(string nomeEsperado, long inicioEsperado, string nomeAtual, long inicioAtual) =>
        inicioEsperado != 0 && inicioAtual != 0 && inicioEsperado == inicioAtual &&
        string.Equals(nomeEsperado, nomeAtual, StringComparison.OrdinalIgnoreCase);

    internal static double CalcularCpu(TimeSpan? anterior, TimeSpan? atual, TimeSpan intervalo, int nucleos) =>
        anterior.HasValue && atual.HasValue ? CalcularCpu(atual.Value - anterior.Value, intervalo, nucleos) : 0;

    internal static double CalcularCpu(TimeSpan delta, TimeSpan intervalo, int nucleos) =>
        intervalo.Ticks <= 0 || nucleos <= 0 ? 0 : Math.Clamp(100.0 * delta.Ticks / intervalo.Ticks / nucleos, 0, 100);

    internal static (bool protegido, string? motivo) Classificar(int pid, string nome, string? caminho, int pidSentinela, string windir)
    {
        if (pid is 0 or 4) return (true, "processo do Windows");
        if (pid == pidSentinela) return (true, "o próprio Sentinela");
        var normalizado = nome.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? nome[..^4] : nome;
        string[] criticos = ["csrss", "wininit", "winlogon", "services", "lsass", "smss", "svchost", "dwm", "explorer", "fontdrvhost", "MsMpEng", "NisSrv", "SecurityHealthService", "Registry", "Memory Compression", "System", "Idle"];
        if (criticos.Contains(normalizado, StringComparer.OrdinalIgnoreCase)) return (true, "processo do Windows");
        if (string.IsNullOrWhiteSpace(caminho)) return (true, "caminho inacessível");
        try
        {
            if (!Path.IsPathFullyQualified(caminho) || string.IsNullOrWhiteSpace(windir)) return (true, "caminho inválido");
            if (Path.GetFullPath(caminho).StartsWith(Path.GetFullPath(windir).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return (true, "processo do Windows");
        }
        catch { return (true, "caminho inválido"); }
        return (false, null);
    }

    public static IReadOnlyList<GrupoProcessos> Agrupar(IEnumerable<InfoProcesso> processos) => processos
        .GroupBy(p => p.Nome.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? p.Nome[..^4] : p.Nome, StringComparer.OrdinalIgnoreCase)
        .Select(g => new GrupoProcessos($"{NomePrograma(g.Key)}{(g.Count() > 1 ? $" ({g.Count()} processos)" : "")}",
            g.Select(p => p.Editora).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e)), g.Sum(p => p.CpuPercent), g.Sum(p => p.MemoriaBytes), Array.AsReadOnly(g.ToArray())))
        .ToArray();

    private static string NomePrograma(string nome) => nome.ToLowerInvariant() switch
    {
        "chrome" => "Google Chrome", "msedge" => "Microsoft Edge", "firefox" => "Mozilla Firefox", _ => nome
    };

    // Chamado em Task.Run pela página, depois da confirmação. Cada PID é reaberto e reconferido.
    public static (bool ok, string mensagem) Finalizar(int pid, string nomeEsperado, long inicioEsperado)
    {
        try
        {
            using var processo = Process.GetProcessById(pid);
            var nome = processo.ProcessName;
            var inicio = processo.StartTime.ToUniversalTime().Ticks;
            if (!Reconferir(nomeEsperado, inicioEsperado, nome, inicio)) return (false, "O processo mudou. Atualize a lista.");
            var caminho = LerCaminho(pid);
            var (protegido, motivo) = Classificar(pid, nome, caminho, Environment.ProcessId, Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            if (protegido) return (false, $"Protegido: {motivo}.");
            processo.Kill(entireProcessTree: false);
            Diario.Registrar("processo", $"Finalizado: {nome}");
            return (true, $"Finalizado: {nome}.");
        }
        catch { return (false, "Não consegui finalizar: o processo saiu ou o acesso foi negado."); }
    }
}
