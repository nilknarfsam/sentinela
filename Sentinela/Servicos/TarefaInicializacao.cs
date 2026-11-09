using System.Diagnostics;
using System.Xml.Linq;

namespace Sentinela.Servicos;

public static class TarefaInicializacao
{
    public static bool? EstaLigada()
    {
        try
        {
            var (codigo, saida, _) = Executar("/query /tn Sentinela /xml");
            if (codigo != 0) return null;
            var raiz = XDocument.Parse(saida).Root;
            if (raiz == null) return null;
            var ns = raiz.Name.Namespace;
            var habilitada = raiz.Element(ns + "Settings")?.Element(ns + "Enabled")?.Value.Trim();
            return !string.Equals(habilitada, "false", StringComparison.OrdinalIgnoreCase);
        }
        catch { return null; }
    }

    public static (bool ok, string mensagem) Definir(bool ligar)
    {
        bool ok = false;
        string mensagem;
        try
        {
            if (!Sistema.EhAdmin())
                mensagem = "Abra como administrador para mudar.";
            else
            {
                var (codigo, _, erro) = Executar("/change /tn Sentinela " + (ligar ? "/enable" : "/disable"));
                ok = codigo == 0;
                mensagem = ok
                    ? (ligar ? "O Sentinela vai iniciar com o Windows." : "O Sentinela não vai iniciar com o Windows.")
                    : "Não foi possível mudar a inicialização. Verifique se o Sentinela está instalado e abra como administrador."
                      + (string.IsNullOrWhiteSpace(erro) ? "" : " Detalhes do Windows: " + erro.Trim());
            }
        }
        catch (TimeoutException) { mensagem = "O Windows demorou demais para responder. Tente novamente."; }
        catch { mensagem = "Não foi possível mudar a inicialização. Verifique a instalação e tente novamente como administrador."; }
        Diario.Registrar("ajuste", mensagem);
        return (ok, mensagem);
    }

    private static (int codigo, string saida, string erro) Executar(string argumentos)
    {
        using var processo = Process.Start(new ProcessStartInfo(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe"), argumentos)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException();
        var saida = processo.StandardOutput.ReadToEndAsync();
        var erro = processo.StandardError.ReadToEndAsync();
        if (!processo.WaitForExit(10000))
        {
            try { processo.Kill(); } catch { }
            throw new TimeoutException();
        }
        return (processo.ExitCode, saida.GetAwaiter().GetResult(), erro.GetAwaiter().GetResult());
    }
}
