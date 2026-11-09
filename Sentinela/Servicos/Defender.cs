using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Sentinela.Servicos;

/// <summary>Estado do Windows Defender lido pelo WMI (o mesmo que Get-MpComputerStatus).</summary>
public sealed record StatusDefender(
    bool AntivirusLigado,
    bool TempoReal,
    bool Comportamento,
    bool Downloads,
    bool ProtecaoAdulteracao,
    string Modo,
    string VersaoAssinaturas,
    DateTime? AssinaturasAtualizadas,
    DateTime? UltimaVarreduraRapida,
    DateTime? UltimaVarreduraCompleta)
{
    public bool TudoLigado => AntivirusLigado && TempoReal && Comportamento && Downloads;
    public bool AssinaturasVelhas => AssinaturasAtualizadas is { } d && (DateTime.Now - d).TotalDays > 3;
}

public sealed record Ameaca(
    string Nome,
    string Gravidade,
    string Situacao,
    DateTime? Detectada,
    string Local,
    bool Ativa);

public enum TipoVarredura { Rapida = 1, Completa = 2, Personalizada = 3 }

/// <summary>
/// Conversa com o Windows Defender. Só lê o estado e pede ações que o próprio
/// Defender oferece (varrer, atualizar, remover ameaças). Nunca desliga nada.
/// </summary>
public static class Defender
{
    private const string Ns = @"root\Microsoft\Windows\Defender";

    public static StatusDefender? LerStatus()
    {
        using var busca = new ManagementObjectSearcher(Ns, "SELECT * FROM MSFT_MpComputerStatus");
        foreach (ManagementObject o in busca.Get())
        {
            using (o)
            {
                return new StatusDefender(
                    Bool(o, "AntivirusEnabled"),
                    Bool(o, "RealTimeProtectionEnabled"),
                    Bool(o, "BehaviorMonitorEnabled"),
                    Bool(o, "IoavProtectionEnabled"),
                    Bool(o, "IsTamperProtected"),
                    o["AMRunningMode"]?.ToString() ?? "?",
                    o["AntivirusSignatureVersion"]?.ToString() ?? "?",
                    Data(o, "AntivirusSignatureLastUpdated"),
                    Data(o, "QuickScanEndTime"),
                    Data(o, "FullScanEndTime"));
            }
        }
        return null;
    }

    /// <summary>Histórico de ameaças encontradas. Precisa de administrador.</summary>
    public static List<Ameaca> LerAmeacas()
    {
        var nomes = new Dictionary<string, (string nome, int grav, bool ativa)>();
        using (var b = new ManagementObjectSearcher(Ns, "SELECT * FROM MSFT_MpThreat"))
            foreach (ManagementObject o in b.Get())
                using (o)
                    nomes[o["ThreatID"]?.ToString() ?? ""] = (
                        o["ThreatName"]?.ToString() ?? "?",
                        Convert.ToInt32(o["SeverityID"] ?? 0),
                        Bool(o, "IsActive"));

        var lista = new List<Ameaca>();
        using (var b = new ManagementObjectSearcher(Ns, "SELECT * FROM MSFT_MpThreatDetection"))
            foreach (ManagementObject o in b.Get())
                using (o)
                {
                    var id = o["ThreatID"]?.ToString() ?? "";
                    nomes.TryGetValue(id, out var t);
                    var recursos = o["Resources"] as string[] ?? [];
                    lista.Add(new Ameaca(
                        t.nome ?? "?",
                        Gravidade(t.grav),
                        Situacao(Convert.ToInt32(o["ThreatStatusID"] ?? 0)),
                        Data(o, "InitialDetectionTime"),
                        string.Join("  |  ", recursos.Select(LimparRecurso)),
                        t.ativa));
                }

        return lista.OrderByDescending(a => a.Detectada).ToList();
    }

    /// <summary>Lista o que está na quarentena (texto do MpCmdRun).</summary>
    public static async Task<string> ListarQuarentena()
    {
        var (_, saida) = await RodarMpCmdRun("-Restore -ListAll", CancellationToken.None);
        return string.IsNullOrWhiteSpace(saida) ? "A quarentena está vazia." : saida;
    }

    internal static string ArgumentosRestaurar(string nome, string? caminho)
    {
        static string EntreAspas(string valor, string parametro)
        {
            if (valor.Contains('"') || valor.Any(char.IsControl))
                throw new ArgumentException("Aspas e caracteres de controle não são permitidos.", parametro);
            var barrasFinais = valor.Length - valor.TrimEnd('\\').Length;
            return "\"" + valor + new string('\\', barrasFinais) + "\"";
        }
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Informe o nome da ameaça.", nameof(nome));
        var argumentos = "-Restore -Name " + EntreAspas(nome, nameof(nome));
        if (caminho != null)
        {
            if (string.IsNullOrWhiteSpace(caminho)) throw new ArgumentException("Informe o caminho do arquivo.", nameof(caminho));
            var arquivo = EntreAspas(caminho, nameof(caminho));
            argumentos += " -FilePath " + arquivo;
        }
        return argumentos;
    }

    public static Task<(int codigo, string saida)> Restaurar(string nome, string? caminho)
        => RodarMpCmdRun(ArgumentosRestaurar(nome, caminho), CancellationToken.None);

    public static async Task IniciarVarreduraOffline()
    {
        var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command \"Start-MpWDOScan\"")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        using var processo = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o PowerShell.");
        var saida = processo.StandardOutput.ReadToEndAsync();
        var erro = processo.StandardError.ReadToEndAsync();
        await processo.WaitForExitAsync();
        var texto = (await saida + await erro).Trim();
        if (processo.ExitCode != 0)
            throw new InvalidOperationException($"O Defender recusou a varredura offline (código {processo.ExitCode}). {texto}");
    }

    internal static string ArgumentosVarredura(TipoVarredura tipo, string? caminho)
    {
        if (tipo != TipoVarredura.Personalizada) return $"-Scan -ScanType {(int)tipo}";
        if (string.IsNullOrWhiteSpace(caminho) || !Pedidos.CaminhoLocalValido(caminho) || !Path.IsPathFullyQualified(caminho))
            throw new ArgumentException("Informe um caminho absoluto de unidade local.", nameof(caminho));
        // Duplica barras finais para preservar a aspa de fechamento na linha de comando.
        var barrasFinais = caminho.Length - caminho.TrimEnd('\\').Length;
        return "-Scan -ScanType 3 -File \"" + caminho + new string('\\', barrasFinais) + "\"";
    }

    public static Task<(int codigo, string saida)> Varrer(TipoVarredura tipo, CancellationToken ct, string? caminho = null)
    {
        var argumentos = ArgumentosVarredura(tipo, caminho);
        return Task.Run(async () =>
        {
            if (tipo == TipoVarredura.Personalizada && !Pedidos.UnidadePermitida(caminho!))
                throw new ArgumentException("A unidade deve ser local, fixa ou removível.", nameof(caminho));
            return await RodarMpCmdRun(argumentos, ct, cancelar: "-Scan -Cancel");
        }, ct);
    }

    public static Task<(int codigo, string saida)> AtualizarAssinaturas(CancellationToken ct)
        => RodarMpCmdRun("-SignatureUpdate", ct);

    /// <summary>Pede ao Defender para tratar as ameaças ativas (mesmo que Remove-MpThreat).</summary>
    public static async Task<string> RemoverAmeacasAtivas()
    {
        var psi = new ProcessStartInfo("powershell.exe",
            "-NoProfile -NonInteractive -Command \"Remove-MpThreat -ErrorAction Stop; 'OK'\"")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        using var p = Process.Start(psi)!;
        var saida = await p.StandardOutput.ReadToEndAsync();
        var erro = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return saida.Contains("OK") ? "As ameaças ativas foram tratadas pelo Defender." : "Não foi possível: " + erro.Trim();
    }

    public static void AbrirSegurancaDoWindows()
        => Process.Start(new ProcessStartInfo("windowsdefender://threat") { UseShellExecute = true });

    // ------------------------------------------------------------------

    public static string? CaminhoMpCmdRun()
    {
        var pasta = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender", "InstallLocation", null) as string;
        if (!string.IsNullOrEmpty(pasta))
        {
            var exe = Path.Combine(pasta, "MpCmdRun.exe");
            if (File.Exists(exe)) return exe;
        }
        var padrao = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender", "MpCmdRun.exe");
        return File.Exists(padrao) ? padrao : null;
    }

    private static async Task<(int, string)> RodarMpCmdRun(string args, CancellationToken ct, string? cancelar = null)
    {
        var exe = CaminhoMpCmdRun() ?? throw new InvalidOperationException("MpCmdRun.exe não encontrado. O Defender está instalado?");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.GetEncoding((int)GetOEMCP()),
            StandardErrorEncoding = Encoding.GetEncoding((int)GetOEMCP()),
        };
        using var p = Process.Start(psi)!;
        var tOut = p.StandardOutput.ReadToEndAsync();
        var tErr = p.StandardError.ReadToEndAsync();
        try
        {
            await p.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            if (cancelar != null)
            {
                using var c = Process.Start(new ProcessStartInfo(exe, cancelar) { UseShellExecute = false, CreateNoWindow = true });
                c?.WaitForExit(15000);
            }
            try { if (!p.HasExited) p.Kill(); } catch { }
            throw;
        }
        return (p.ExitCode, (await tOut) + (await tErr));
    }

    [DllImport("kernel32.dll")] private static extern uint GetOEMCP();

    private static bool Bool(ManagementBaseObject o, string nome) => o[nome] is bool b && b;

    private static DateTime? Data(ManagementBaseObject o, string nome)
    {
        var v = o[nome]?.ToString();
        if (string.IsNullOrEmpty(v)) return null;
        try
        {
            var d = ManagementDateTimeConverter.ToDateTime(v);
            return d.Year < 2000 ? null : d;
        }
        catch { return null; }
    }

    internal static string LimparRecurso(string r)
    {
        var i = r.IndexOf(':');
        // "file:_C:\pasta\x.exe" -> "C:\pasta\x.exe". Um caminho comum ("C:\x.exe")
        // tem o ':' na posição 1 e não tem prefixo: fica como está.
        return i > 1 && i < 12 && r[..i].All(char.IsLetter) ? r[(i + 1)..].TrimStart('_') : r;
    }

    internal static string Gravidade(int id) => id switch
    {
        1 => "Baixa", 2 => "Moderada", 4 => "Alta", 5 => "Grave", _ => "Desconhecida",
    };

    internal static string Situacao(int id) => id switch
    {
        1 => "Detectada",
        2 => "Limpa",
        3 => "Em quarentena",
        4 => "Removida",
        5 => "Permitida",
        6 => "Bloqueada",
        102 => "Falha na quarentena",
        103 => "Falha ao remover",
        107 => "Falha ao bloquear",
        _ => $"Situação {id}",
    };
}
