using System.Diagnostics;
using System.Management;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Sentinela.Servicos.Checkup;

public sealed class VerificacaoSaudeDiscos : Verificacao
{
    public override string Nome => "Saúde dos discos";
    public override int Peso => 3;
    protected override async Task<ResultadoVerificacao> Ler(CancellationToken ct) => Avaliar(await SaudeDisco.Ler(ct).ConfigureAwait(false), Sistema.EhAdmin());
    internal static ResultadoVerificacao Avaliar(IEnumerable<DiscoFisico> discos, bool admin)
    {
        var lista = discos.Select(d => (disco: d, avaliacao: SaudeDisco.Avaliar(d))).OrderByDescending(d => d.avaliacao.nivel).ToArray();
        if (lista.Length == 0) return Checkup.Indisponivel("Saúde dos discos");
        var pior = lista[0];
        var desconhecidos = lista.All(d => d.disco.Saude == "Desconhecida");
        return new("Saúde dos discos", "\uEDA2", desconhecidos ? NivelVerificacao.Atencao : pior.avaliacao.nivel,
            desconhecidos ? "Não consegui ler a saúde dos discos" :
            pior.avaliacao.nivel == NivelVerificacao.Ok ? lista.Length == 1 ? "Disco saudável" : $"Todos os {lista.Length} discos saudáveis" : $"{pior.disco.Nome}: {pior.avaliacao.motivo}",
            string.Join("\n", lista.Select(d => SaudeDisco.Descrever(d.disco)))
                + (admin ? "" : "\nAbra como administrador para ver desgaste e temperatura."), new("Ver discos", "rolar", "discos"));
    }
}

public sealed class VerificacaoPeso : Verificacao
{
    public override string Nome => "O que está pesando";
    public override int Peso => 2;
    protected override async Task<ResultadoVerificacao> Ler(CancellationToken ct)
    {
        var processos = await Processos.Amostrar(TimeSpan.FromSeconds(1), ct, lerEditoras: false).ConfigureAwait(false);
        if (ct.IsCancellationRequested || processos.Count == 0) return Checkup.Indisponivel(Nome);
        var grupos = Processos.Agrupar(processos.Where(p => !p.Protegido));
        var maior = grupos.OrderByDescending(g => g.CpuPercent).ThenByDescending(g => g.MemoriaBytes).FirstOrDefault();
        var memoria = Sistema.Memoria().percent;
        var info = maior == null ? null : new InfoProcesso(0, maior.Nome, 0, null, maior.Editora, maior.CpuPercent, maior.MemoriaBytes, false, null);
        return new(Nome, "\uE9D9", NivelPeso(processos.Sum(p => p.CpuPercent), memoria, info),
            maior == null ? "Nenhum programa acessível para comparar" : $"{maior.Nome} está usando {maior.CpuPercent:0.#}% da CPU e {maior.MemoriaTexto}",
            $"Memória total em {memoria:0}% de uso. A amostra mostra o uso neste instante.", new("Ver o que está pesando", "rolar", "agora"));
    }

    internal static NivelVerificacao NivelPeso(double cpuTotalPercent, double memoriaPercent, InfoProcesso? maisPesado)
    {
        // O total inclui o Windows: os limiares de CPU se aplicam somente ao programa não protegido.
        var cpu = maisPesado is { Protegido: false } ? maisPesado.CpuPercent : 0;
        return cpu >= 40 || memoriaPercent >= 90 ? NivelVerificacao.Problema :
            cpu >= 20 || memoriaPercent >= 80 ? NivelVerificacao.Atencao : NivelVerificacao.Ok;
    }
}

public sealed class VerificacaoReinicio : Verificacao
{
    public override string Nome => "Reinício pendente";
    public override int Peso => 3;
    protected override Task<ResultadoVerificacao> Ler(CancellationToken ct)
    {
        using var update = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
        using var componentes = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
        return Task.FromResult(Avaliar(update != null || componentes != null, TimeSpan.FromMilliseconds(Environment.TickCount64)));
    }
    internal static ResultadoVerificacao Avaliar(bool pendente, TimeSpan ligado)
    {
        var tempo = ligado.TotalDays >= 1 ? $"{(int)ligado.TotalDays}d {ligado.Hours}h {ligado.Minutes}min" : $"{ligado.Hours}h {ligado.Minutes}min";
        return new("Reinício pendente", "\uE777", pendente ? NivelVerificacao.Problema : ligado.TotalDays >= 7 ? NivelVerificacao.Atencao : NivelVerificacao.Ok,
            pendente ? "Há atualização esperando reinício" : ligado.TotalDays >= 7 ? $"Ligado há {(int)ligado.TotalDays} dias. Reiniciar de vez em quando deixa o PC mais leve." : $"Ligado há {tempo}",
            pendente ? "Salve seus arquivos antes de reiniciar pelo Windows." : "Reiniciar ajuda a concluir atualizações e renovar a sessão."
                + (ligado.TotalDays >= 7 ? " (com a Inicialização rápida do Windows, desligar não zera esse tempo; só Reiniciar)" : ""),
            pendente ? new("Abrir o Windows Update", "abrir", "ms-settings:windowsupdate") : null);
    }
}

public sealed class VerificacaoEspaco : Verificacao
{
    public override string Nome => "Espaço livre";
    public override int Peso => 3;
    protected override Task<ResultadoVerificacao> Ler(CancellationToken ct) => Task.FromResult(Avaliar(Sistema.Discos()));
    internal static ResultadoVerificacao Avaliar(IEnumerable<InfoDisco> discos)
    {
        var lista = discos.Where(d => d.TotalGB > 0).Select(d => (disco: d, percentual: 100 * d.LivreGB / d.TotalGB))
            .Select(d => (d.disco, d.percentual, nivel: d.percentual < 10 || d.disco.LivreGB < 10 ? NivelVerificacao.Problema : d.percentual < 20 ? NivelVerificacao.Atencao : NivelVerificacao.Ok))
            .OrderByDescending(d => d.nivel).ThenBy(d => d.percentual).ToArray();
        if (lista.Length == 0) return Checkup.Indisponivel("Espaço livre");
        static string Texto(InfoDisco d, double p) => $"{d.Letra} {d.LivreGB:0.#} GB livres ({p:0.#}%)";
        var pior = lista[0];
        return new("Espaço livre", "\uEDA2", pior.nivel, Texto(pior.disco, pior.percentual),
            string.Join("\n", lista.Select(d => Texto(d.disco, d.percentual))), new("Abrir a Otimização", "pagina", "limpeza"));
    }
}

public sealed record DispositivoComErro(string Nome, int Codigo);
public sealed class VerificacaoDrivers : Verificacao
{
    public override string Nome => "Drivers";
    public override int Peso => 2;
    protected override Task<ResultadoVerificacao> Ler(CancellationToken ct)
    {
        using var busca = new ManagementObjectSearcher("SELECT Name, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0");
        busca.Options.Timeout = TimeSpan.FromSeconds(5);
        using var encontrados = busca.Get();
        var lista = new List<DispositivoComErro>();
        foreach (ManagementObject item in encontrados)
        {
            using (item)
            {
                ct.ThrowIfCancellationRequested();
                lista.Add(new(item["Name"]?.ToString() ?? "Dispositivo sem nome", Convert.ToInt32(item["ConfigManagerErrorCode"])));
            }
        }
        return Task.FromResult(Avaliar(lista));
    }
    internal static ResultadoVerificacao Avaliar(IEnumerable<DispositivoComErro> dispositivos)
    {
        var lista = dispositivos.Where(d => d.Codigo is not (0 or 45)).ToArray();
        var nivel = lista.Length == 0 ? NivelVerificacao.Ok : lista.All(d => d.Codigo == 22) ? NivelVerificacao.Atencao : NivelVerificacao.Problema;
        return new("Drivers", "\uE964", nivel, lista.Length == 0 ? "Todos os dispositivos funcionando" :
            nivel == NivelVerificacao.Atencao ? $"{lista.Length} dispositivo(s) desativado(s)" : $"{lista.Length} dispositivo(s) com problema",
            lista.Length == 0 ? "O Sentinela não instala drivers sozinho: use o Windows Update e o app da NVIDIA." :
                string.Join("\n", lista.Select(d => $"{d.Nome} (código {d.Codigo})")),
            new("Abrir o Gerenciador de Dispositivos", "abrir", "devmgmt.msc"));
    }
}

public sealed class VerificacaoEnergia : Verificacao
{
    public override string Nome => "Plano de energia";
    public override int Peso => 1;
    protected override async Task<ResultadoVerificacao> Ler(CancellationToken ct)
    {
        using var processo = new Process { StartInfo = new ProcessStartInfo(CaminhosSistema.PowerCfg, "/getactivescheme")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            processo.Start();
            var saida = processo.StandardOutput.ReadToEndAsync(limite.Token);
            var erro = processo.StandardError.ReadToEndAsync(limite.Token);
            await processo.WaitForExitAsync(limite.Token).ConfigureAwait(false);
            await erro.ConfigureAwait(false);
            return processo.ExitCode == 0 ? Avaliar(await saida.ConfigureAwait(false)) : Checkup.Indisponivel(Nome);
        }
        finally
        {
            try { if (!processo.HasExited) processo.Kill(); } catch { /* Processo já encerrado ou indisponível. */ }
        }
    }
    internal static ResultadoVerificacao Avaliar(string saida)
    {
        var achado = Regex.Match(saida, @"\b[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}\b");
        if (!achado.Success) return Checkup.Indisponivel("Plano de energia");
        var nome = achado.Value.ToLowerInvariant() switch
        {
            "381b4222-f694-41f0-9685-ff5bb260df2e" => "Equilibrado",
            "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" => "Alto desempenho",
            "a1841308-3542-4c4b-8bd3-cdc28aa24d6d" => "Economia de energia",
            "e9a42b02-d5df-448d-aa00-03f14749eb61" => "Desempenho máximo",
            _ => "Plano personalizado"
        };
        var economia = nome == "Economia de energia";
        return new("Plano de energia", "\uE945", economia ? NivelVerificacao.Atencao : NivelVerificacao.Ok,
            economia ? "Plano Economia de energia deixa o PC mais lento" : nome, "Plano ativo nas opções de energia do Windows.",
            economia ? new("Abrir as Opções de Energia", "abrir", "powercfg.cpl") : null);
    }
}

public sealed class VerificacaoInicializacao : Verificacao
{
    public override string Nome => "Inicialização";
    public override int Peso => 2;
    protected override Task<ResultadoVerificacao> Ler(CancellationToken ct)
    {
        var ativos = Inicializacao.Listar().Where(p => p.Ativo).ToArray();
        return Task.FromResult(Avaliar(ativos.Length, ativos.Count(p => p.Assinatura is EstadoAssinatura.NaoAssinado or EstadoAssinatura.Invalida)));
    }
    internal static ResultadoVerificacao Avaliar(int ativos, int semAssinatura = 0) => new("Inicialização", "\uE7E8",
        ativos > 15 ? NivelVerificacao.Problema : ativos >= 9 || semAssinatura > 0 ? NivelVerificacao.Atencao : NivelVerificacao.Ok,
        $"{ativos} programas abrem com o Windows", "Quanto mais programas iniciam juntos, mais a entrada no Windows pode demorar."
            + $" {semAssinatura} programa(s) ativo(s) sem assinatura ou com assinatura inválida.",
        new("Ver a lista", "pagina", "inicializacao"));
}

public sealed class VerificacaoLiberavel : Verificacao
{
    public override string Nome => "Espaço que dá para liberar";
    public override int Peso => 2;
    protected override Task<ResultadoVerificacao> Ler(CancellationToken ct)
    {
        var (bytes, projetos) = ResumoOtimizacao.Ler();
        // Limite próprio: a medição rápida do Windows não ocupa o orçamento de 8 s.
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(2));
        foreach (var item in Limpeza.Itens().Where(i => !i.EhLixeira))
            foreach (var pasta in item.Pastas)
                bytes += ArvoreDescartavel.Percorrer(pasta, false, limite.Token, f => Limpeza.Velho(f, item));
        return Task.FromResult(new ResultadoVerificacao(Nome, "\uEDA2", bytes > 5L * 1024 * 1024 * 1024 ? NivelVerificacao.Atencao : NivelVerificacao.Ok,
            $"Dá para liberar {Limpeza.Formatar(bytes)}" + (projetos ? "" : ". Analise seus projetos na Otimização"),
            "Estimativa do Windows e da última análise da Otimização nesta execução." + (limite.IsCancellationRequested ? " Medição parcial." : ""), new("Abrir a Otimização", "pagina", "limpeza")));
    }
}
