using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Sentinela.Servicos;

public sealed record InfoDisco(string Letra, string Nome, double TotalGB, double LivreGB)
{
    public double UsoPercent => TotalGB <= 0 ? 0 : 100 * (TotalGB - LivreGB) / TotalGB;
    public string Texto => $"{LivreGB:0.#} GB livres de {TotalGB:0} GB";
}

/// <summary>CPU, memória, discos e tempo ligado, sem pacotes extras.</summary>
public static class Sistema
{
    private static readonly object CpuLock = new();
    private static ulong _ociosoAnt, _kernelAnt, _userAnt;

    public static bool EhAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>Uso de CPU em % desde a última chamada.</summary>
    public static double UsoCpu()
    {
        lock (CpuLock)
        {
            if (!GetSystemTimes(out var i, out var k, out var u)) return 0;
            ulong ocioso = i.Valor, kernel = k.Valor, user = u.Valor;
            ulong dOcioso = ocioso - _ociosoAnt, total = (kernel - _kernelAnt) + (user - _userAnt);
            bool primeira = _kernelAnt == 0;
            _ociosoAnt = ocioso; _kernelAnt = kernel; _userAnt = user;
            if (primeira || total == 0) return 0;
            return Math.Clamp(100.0 * (total - dOcioso) / total, 0, 100);
        }
    }

    public static (double usadoGB, double totalGB, double percent) Memoria()
    {
        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref m)) return (0, 0, 0);
        double total = m.ullTotalPhys / 1073741824.0;
        double livre = m.ullAvailPhys / 1073741824.0;
        return (total - livre, total, m.dwMemoryLoad);
    }

    public static List<InfoDisco> Discos() =>
        DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => new InfoDisco(d.Name.TrimEnd('\\'),
                string.IsNullOrWhiteSpace(d.VolumeLabel) ? "Disco local" : d.VolumeLabel,
                d.TotalSize / 1073741824.0, d.AvailableFreeSpace / 1073741824.0))
            .ToList();

    public static string TempoLigado()
    {
        var t = TimeSpan.FromMilliseconds(Environment.TickCount64);
        return t.TotalDays >= 1 ? $"{(int)t.TotalDays}d {t.Hours}h {t.Minutes}min" : $"{t.Hours}h {t.Minutes}min";
    }

    // ------------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME { public uint Baixo, Alto; public ulong Valor => ((ulong)Alto << 32) | Baixo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile,
                     ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME ocioso, out FILETIME kernel, out FILETIME user);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
}
