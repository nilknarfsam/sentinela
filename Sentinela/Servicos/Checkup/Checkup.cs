using System.Diagnostics;

namespace Sentinela.Servicos.Checkup;

public enum NivelVerificacao { Ok, Atencao, Problema }
public sealed record AcaoResolver(string Texto, string Tipo, string? Alvo);
public sealed record ResultadoVerificacao(string Nome, string Icone, NivelVerificacao Nivel, string Resumo, string Detalhe, AcaoResolver? Acao);
public sealed record ResultadoCheckup(DateTime Data, int Nota, IReadOnlyList<ResultadoVerificacao> Itens, TimeSpan Duracao);
public interface IVerificacao
{
    string Nome { get; }
    int Peso { get; }
    Task<ResultadoVerificacao> Verificar(CancellationToken ct);
}

public static class Checkup
{
    private static ResultadoCheckup? _ultimo;
    public static ResultadoCheckup? Ultimo => Volatile.Read(ref _ultimo);
    internal static string? PastaTeste;
    private static string Caminho => Path.Combine(PastaTeste ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinela"), "checkup.json");
    public static Task CarregarUltimo() => Task.Run(() =>
    {
        var salvo = LerSalvo(Caminho);
        if (salvo != null) Interlocked.CompareExchange(ref _ultimo, salvo, null);
    });

    private sealed record ItemSalvo(string Nome, NivelVerificacao Nivel, string Resumo);
    private sealed record CheckupSalvo(DateTime Data, int Nota, ItemSalvo[] Itens);
    internal static ResultadoCheckup? LerSalvo(string caminho)
    {
        try
        {
            var salvo = System.Text.Json.JsonSerializer.Deserialize<CheckupSalvo>(ArquivosSeguros.LerTexto(caminho, 64 * 1024) ?? "");
            if (salvo == null || salvo.Data == default || salvo.Nota is < 0 or > 100 || salvo.Itens == null
                || salvo.Itens.Any(i => i == null || string.IsNullOrWhiteSpace(i.Nome) || i.Resumo == null || !Enum.IsDefined(i.Nivel))) return null;
            return new(salvo.Data, salvo.Nota, Array.AsReadOnly(salvo.Itens.Select(i =>
                new ResultadoVerificacao(i.Nome, "\uE9D9", i.Nivel, i.Resumo, "Resultado da última verificação salva.", null)).ToArray()), TimeSpan.Zero);
        }
        catch { return null; }
    }
    internal static bool GravarSalvo(string caminho, ResultadoCheckup resultado)
    {
        try
        {
            var texto = System.Text.Json.JsonSerializer.Serialize(new CheckupSalvo(resultado.Data, resultado.Nota,
                resultado.Itens.Select(i => new ItemSalvo(i.Nome, i.Nivel, i.Resumo)).ToArray()));
            return System.Text.Encoding.UTF8.GetByteCount(texto) <= 64 * 1024 && ArquivosSeguros.GravarTexto(caminho, texto);
        }
        catch { return false; }
    }

    public static IReadOnlyList<IVerificacao> Padrao { get; } = Array.AsReadOnly<IVerificacao>(
        [new VerificacaoReinicio(), new VerificacaoEspaco(), new VerificacaoDrivers(), new VerificacaoEnergia(), new VerificacaoInicializacao(), new VerificacaoLiberavel(), new VerificacaoPeso(), new VerificacaoSaudeDiscos()]);

    // Inclusive a enumeração e o diário ficam fora da thread da tela.
    public static Task<ResultadoCheckup> Executar(IEnumerable<IVerificacao>? verificacoes = null,
        TimeSpan? limitePorItem = null, CancellationToken ct = default) => Task.Run(async () =>
    {
        var relogio = Stopwatch.StartNew();
        var resultados = new List<(ResultadoVerificacao item, int peso)>();
        try
        {
            var tarefas = (verificacoes ?? Padrao).Select(async v =>
            {
                var nome = "Verificação";
                var peso = 1;
                using var cancelamento = CancellationTokenSource.CreateLinkedTokenSource(ct);
                try
                {
                    nome = v.Nome;
                    peso = v.Peso;
                    cancelamento.CancelAfter(limitePorItem ?? TimeSpan.FromSeconds(8));
                    // Protege também contra implementações síncronas que bloqueiam ou lançam.
                    var tarefa = Task.Run(() => v.Verificar(cancelamento.Token));
                    _ = tarefa.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    var item = await tarefa.WaitAsync(cancelamento.Token).ConfigureAwait(false);
                    return (item, peso);
                }
                catch { return (Indisponivel(nome), peso); }
            }).ToArray();
            resultados.AddRange(await Task.WhenAll(tarefas).ConfigureAwait(false));
        }
        catch { resultados.Add((Indisponivel("Checkup de desempenho"), 1)); }
        var nota = CalcularNota(resultados.Select(r => (r.item.Nivel, r.peso)));
        Diario.Registrar("checkup", $"Checkup de desempenho: nota {nota}");
        var resultado = new ResultadoCheckup(DateTime.Now, nota, Array.AsReadOnly(resultados.Select(r => r.item).ToArray()), relogio.Elapsed);
        Volatile.Write(ref _ultimo, resultado);
        if (App.CapturaArquivo == null) GravarSalvo(Caminho, resultado);
        return resultado;
    });

    internal static ResultadoVerificacao Indisponivel(string nome) => new(nome, "\uE9D9", NivelVerificacao.Atencao,
        "Não consegui verificar agora", "Tente novamente em alguns instantes.", null);

    internal static int CalcularNota(IEnumerable<(NivelVerificacao nivel, int peso)> itens)
    {
        double soma = 0, pesos = 0;
        foreach (var (nivel, peso) in itens.Where(i => i.peso > 0))
        {
            soma += (nivel == NivelVerificacao.Ok ? 100 : nivel == NivelVerificacao.Atencao ? 60 : 20) * (double)peso;
            pesos += peso;
        }
        return pesos == 0 ? 100 : (int)Math.Round(soma / pesos, MidpointRounding.AwayFromZero);
    }
}

/// <summary>Fronteira das leituras: nunca propaga falhas nem executa leitura na tela.</summary>
public abstract class Verificacao : IVerificacao
{
    public abstract string Nome { get; }
    public abstract int Peso { get; }
    protected abstract Task<ResultadoVerificacao> Ler(CancellationToken ct);
    public Task<ResultadoVerificacao> Verificar(CancellationToken ct) => Task.Run(async () =>
    {
        try { ct.ThrowIfCancellationRequested(); return await Ler(ct).ConfigureAwait(false); }
        catch { return Checkup.Indisponivel(Nome); }
    });
}
