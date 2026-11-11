using System.Runtime.InteropServices;

namespace Sentinela.Servicos;

internal static class RotinaSemanal
{
    internal static DateTime MomentoMarcado(DateTime agora, DayOfWeek dia, int hora)
    {
        var momento = agora.Date.AddDays(-((7 + (int)agora.DayOfWeek - (int)dia) % 7)).AddHours(hora);
        return momento > agora ? momento.AddDays(-7) : momento;
    }

    internal static bool DeveVarrer(DateTime agora, DayOfWeek dia, int hora, DateTime? ultimaCompleta,
        DateTime? ultimaTentativa, TimeSpan ocioso, bool varrendo, bool ligada)
    {
        if (!ligada || varrendo || !Enum.IsDefined(dia) || hora is < 0 or > 23) return false;
        var momento = MomentoMarcado(agora, dia, hora);
        return !(ultimaCompleta >= momento) && !(ultimaTentativa >= momento)
            && (agora - momento <= TimeSpan.FromHours(1) || ocioso >= TimeSpan.FromMinutes(10));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UltimaEntrada { public uint Tamanho; public uint Instante; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref UltimaEntrada entrada);

    internal static TimeSpan LerTempoOcioso()
    {
        try
        {
            var entrada = new UltimaEntrada { Tamanho = (uint)Marshal.SizeOf<UltimaEntrada>() };
            return GetLastInputInfo(ref entrada)
                ? TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - entrada.Instante)) : TimeSpan.Zero;
        }
        catch { return TimeSpan.Zero; }
    }
}
