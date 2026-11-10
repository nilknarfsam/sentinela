namespace Sentinela.Servicos;

internal static class CaminhosSistema
{
    public static string PowerCfg => Path.Combine(Environment.SystemDirectory, "powercfg.exe");
    public static string Mmc => Path.Combine(Environment.SystemDirectory, "mmc.exe");
    public static string AgendadorTarefas => Path.Combine(Environment.SystemDirectory, "taskschd.msc");
    public static string Servicos => Path.Combine(Environment.SystemDirectory, "services.msc");
    public static string GerenciadorDispositivos => Path.Combine(Environment.SystemDirectory, "devmgmt.msc");
    public static string Control => Path.Combine(Environment.SystemDirectory, "control.exe");
    public static string OpcoesEnergia => Path.Combine(Environment.SystemDirectory, "powercfg.cpl");
    public static string PowerShell => Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
    public static string SchTasks => Path.Combine(Environment.SystemDirectory, "schtasks.exe");
    public static string TaskMgr => Path.Combine(Environment.SystemDirectory, "taskmgr.exe");
    public static string Cmd => Path.Combine(Environment.SystemDirectory, "cmd.exe");
    public static string Explorer => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
}
