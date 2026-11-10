using System.Diagnostics;
using Microsoft.Win32;

namespace Sentinela.Servicos;

public sealed record ProgramaInicial(string Nome, string Comando, string Origem, bool Ativo)
{
    public string Estado => Ativo ? "Ativo" : "Desativado";
}

/// <summary>
/// Lista o que abre junto com o Windows (só leitura). Para ligar ou desligar,
/// o painel abre a aba Inicializar do Gerenciador de Tarefas.
/// </summary>
public static class Inicializacao
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Aprovado = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    public static List<ProgramaInicial> Listar()
    {
        var lista = new List<ProgramaInicial>();
        LerRun(lista, Registry.CurrentUser, Run, "Usuário", Aprovado + "Run");
        LerRun(lista, Registry.LocalMachine, Run, "Todos os usuários", Aprovado + "Run");
        LerRun(lista, Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Todos (32 bits)", Aprovado + "Run32");
        LerPasta(lista, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Pasta Inicializar", Registry.CurrentUser);
        LerPasta(lista, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Pasta Inicializar (todos)", Registry.LocalMachine);
        return lista.OrderByDescending(p => p.Ativo).ThenBy(p => p.Nome).ToList();
    }

    public static void AbrirGerenciadorDeTarefas()
        => Process.Start(new ProcessStartInfo(CaminhosSistema.TaskMgr, "/0 /startup") { UseShellExecute = true });

    private static void LerRun(List<ProgramaInicial> lista, RegistryKey raiz, string caminho, string origem, string aprovado)
    {
        using var k = raiz.OpenSubKey(caminho);
        if (k == null) return;
        foreach (var nome in k.GetValueNames())
        {
            if (string.IsNullOrEmpty(nome)) continue;
            lista.Add(new(nome, k.GetValue(nome)?.ToString() ?? "", origem, EstaAtivo(raiz, aprovado, nome)));
        }
    }

    private static void LerPasta(List<ProgramaInicial> lista, string pasta, string origem, RegistryKey raiz)
    {
        if (!Directory.Exists(pasta)) return;
        foreach (var f in Directory.EnumerateFiles(pasta).Where(f => !f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)))
        {
            var nome = Path.GetFileName(f);
            lista.Add(new(Path.GetFileNameWithoutExtension(f), f, origem, EstaAtivo(raiz, Aprovado + "StartupFolder", nome)));
        }
    }

    /// <summary>O Gerenciador de Tarefas guarda o liga/desliga em StartupApproved (1º byte par = ativo).</summary>
    private static bool EstaAtivo(RegistryKey raiz, string caminho, string nome)
    {
        using var k = raiz.OpenSubKey(caminho);
        return AtivoPeloValor(k?.GetValue(nome));
    }

    internal static bool AtivoPeloValor(object? valor)
        => valor is not byte[] { Length: > 0 } b || (b[0] & 1) == 0;
}
