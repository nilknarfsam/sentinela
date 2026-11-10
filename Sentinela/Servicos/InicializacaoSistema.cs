using System.Management;

namespace Sentinela.Servicos;

public sealed record ItemInicializacaoSistema(string Nome, string Comando, string Origem)
{
    public string Editora { get; init; } = "?";
    public EstadoAssinatura Assinatura { get; init; }
    public string TextoAssinatura => Assinaturas.Texto(Assinatura);
}

/// <summary>Inventário somente de leitura de tarefas e serviços de terceiros.</summary>
public static class InicializacaoSistema
{
    internal static bool ForaDoWindows(string comando)
    {
        try
        {
            var caminho = Inicializacao.ExtrairExecutavel(comando);
            if (caminho == null) return true;
            var windows = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            var completo = Path.GetFullPath(caminho);
            return !new[] { "System32", "SysWOW64", "WinSxS", "Microsoft.NET" }
                .Any(pasta => completo.StartsWith(Path.Combine(windows, pasta) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase));
        }
        catch { return true; }
    }

    private static ItemInicializacaoSistema ComAssinatura(ItemInicializacaoSistema item)
    {
        var info = Assinaturas.LerComando(item.Comando);
        return item with { Editora = info.Editora, Assinatura = info.Estado };
    }

    public static List<ItemInicializacaoSistema> Tarefas(bool assinaturas = true) => Tarefas(assinaturas, out _);

    internal static List<ItemInicializacaoSistema> Tarefas(bool assinaturas, out bool completo)
    {
        completo = false;
        var falhou = false;
        void Falha() => falhou = true;
        var lista = new List<ItemInicializacaoSistema>();
        object? servico = null, raiz = null;
        try
        {
            var tipo = Type.GetTypeFromProgID("Schedule.Service");
            if (tipo == null) return lista;
            servico = Activator.CreateInstance(tipo);
            if (servico == null) return lista;
            ((dynamic)servico).Connect();
            raiz = ((dynamic)servico).GetFolder("\\");
            LerPasta(raiz, lista, Falha);
            completo = !falhou;
        }
        catch { /* Retorna o inventário disponível. */ }
        finally { Assinaturas.LiberarCom(raiz); Assinaturas.LiberarCom(servico); }
        return lista.Select(i => assinaturas ? ComAssinatura(i) : i).OrderBy(i => i.Nome).ToList();
    }

    private static void LerPasta(dynamic pasta, List<ItemInicializacaoSistema> lista, Action falha)
    {
        object? tarefas = null, pastas = null;
        try
        {
            tarefas = pasta.GetTasks(1);
            for (var i = 1; i <= (int)((dynamic)tarefas).Count; i++)
            {
                object? tarefa = null, definicao = null, gatilhos = null, acoes = null;
                try
                {
                    tarefa = ((dynamic)tarefas)[i];
                    if (!(bool)((dynamic)tarefa).Enabled) continue;
                    definicao = ((dynamic)tarefa).Definition;
                    gatilhos = ((dynamic)definicao).Triggers;
                    var inicia = false;
                    for (var j = 1; j <= (int)((dynamic)gatilhos).Count; j++)
                    {
                        object? gatilho = null;
                        try { gatilho = ((dynamic)gatilhos)[j]; inicia |= (bool)((dynamic)gatilho).Enabled && (int)((dynamic)gatilho).Type is 8 or 9; }
                        finally { Assinaturas.LiberarCom(gatilho); }
                    }
                    if (!inicia) continue;
                    acoes = ((dynamic)definicao).Actions;
                    for (var j = 1; j <= (int)((dynamic)acoes).Count; j++)
                    {
                        object? acao = null;
                        try
                        {
                            acao = ((dynamic)acoes)[j];
                            if ((int)((dynamic)acao).Type != 0) continue;
                            string executavel = ((dynamic)acao).Path;
                            string argumentos = ((dynamic)acao).Arguments;
                            var comando = $"\"{executavel.Trim('"')}\" {argumentos}".Trim();
                            if (ForaDoWindows(comando)) lista.Add(new((string)((dynamic)tarefa).Name, comando, (string)((dynamic)tarefa).Path));
                        }
                        finally { Assinaturas.LiberarCom(acao); }
                    }
                }
                catch { falha(); /* Uma tarefa inacessível não impede as outras. */ }
                finally { Assinaturas.LiberarCom(acoes); Assinaturas.LiberarCom(gatilhos); Assinaturas.LiberarCom(definicao); Assinaturas.LiberarCom(tarefa); }
            }
        }
        catch { falha(); /* Continua pelas subpastas disponíveis. */ }
        try
        {
            pastas = pasta.GetFolders(0);
            for (var i = 1; i <= (int)((dynamic)pastas).Count; i++)
            {
                object? filha = null;
                try
                {
                    filha = ((dynamic)pastas)[i];
                    string caminho = ((dynamic)filha).Path;
                    if (!caminho.Equals(@"\Microsoft", StringComparison.OrdinalIgnoreCase) && !caminho.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase))
                        LerPasta(filha, lista, falha);
                }
                catch { falha(); /* Pasta sem permissão de leitura. */ }
                finally { Assinaturas.LiberarCom(filha); }
            }
        }
        catch { falha(); /* Sem subpastas disponíveis. */ }
        finally { Assinaturas.LiberarCom(pastas); Assinaturas.LiberarCom(tarefas); }
    }

    public static List<ItemInicializacaoSistema> Servicos()
    {
        var lista = new List<ItemInicializacaoSistema>();
        try
        {
            using var busca = new ManagementObjectSearcher("SELECT Name, DisplayName, PathName FROM Win32_Service WHERE StartMode = 'Auto'");
            busca.Options.Timeout = TimeSpan.FromSeconds(10);
            using var encontrados = busca.Get();
            foreach (ManagementObject item in encontrados)
            {
                using (item)
                {
                    var comando = item["PathName"]?.ToString() ?? "";
                    if (ForaDoWindows(comando)) lista.Add(ComAssinatura(new(item["DisplayName"]?.ToString() ?? "Serviço", comando, item["Name"]?.ToString() ?? "")));
                }
            }
        }
        catch { /* WMI indisponível: preserva os resultados já lidos. */ }
        return lista.OrderBy(i => i.Nome).ToList();
    }
}
