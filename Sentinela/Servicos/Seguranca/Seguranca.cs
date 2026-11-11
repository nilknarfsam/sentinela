using System.Text.Json;
using Sentinela.Servicos.Checkup;
using Motor = Sentinela.Servicos.Checkup.Checkup;

namespace Sentinela.Servicos.Seguranca;

public static class Seguranca
{
    private static ResultadoCheckup? _ultimo;
    public static ResultadoCheckup? Ultimo => Volatile.Read(ref _ultimo);
    internal static string? PastaTeste;
    private static string Pasta => PastaTeste ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinela");
    private static string Caminho => Path.Combine(Pasta, "seguranca.json");
    internal static string CaminhoExclusoes => Path.Combine(Pasta, "exclusoes.json");
    public static Task CarregarUltimo() => Task.Run(() =>
    {
        var salvo = Motor.LerSalvo(Caminho);
        if (salvo != null) Interlocked.CompareExchange(ref _ultimo, salvo, null);
    });
    public static IReadOnlyList<IVerificacao> Padrao => CriarVerificacoes();
    private static IReadOnlyList<IVerificacao> CriarVerificacoes(CancellationToken ct = default)
    {
        // Uma única consulta compartilhada às preferências do Defender por execução.
        var preferencias = new Lazy<Task<PreferenciasDefender?>>(() => Task.Run(() => Leituras.LerPreferencias(ct)));
        return Array.AsReadOnly<IVerificacao>([new VerificacaoFirewall(), new VerificacaoWindowsUpdate(),
            new VerificacaoUac(), new VerificacaoSmartScreen(preferencias), new VerificacaoRansomware(preferencias),
            new VerificacaoDispositivo(), new VerificacaoExclusoes(preferencias), new VerificacaoContas()]);
    }
    public static Task<ResultadoCheckup> Executar(IEnumerable<IVerificacao>? verificacoes = null,
        TimeSpan? limitePorItem = null, CancellationToken ct = default) => Motor.ExecutarConjunto(
            () => verificacoes ?? CriarVerificacoes(ct), limitePorItem, ct, "seguranca", "Saúde da segurança", Caminho,
            r => Volatile.Write(ref _ultimo, r));

    internal sealed record ReferenciaExclusoes(string[]? Itens, bool Ausente)
    {
        internal bool Invalida => !Ausente && Itens == null;
    }
    internal static ReferenciaExclusoes ObterReferencia(string caminho)
    {
        try { _ = File.GetAttributes(caminho); }
        catch (FileNotFoundException) { return new(null, true); }
        catch (DirectoryNotFoundException) { return new(null, true); }
        catch { return new(null, false); }
        return new(LerReferencia(caminho), false);
    }
    internal static string[]? LerReferencia(string caminho)
    {
        try
        {
            var itens = JsonSerializer.Deserialize<string[]>(ArquivosSeguros.LerTexto(caminho, 64 * 1024) ?? "");
            return itens != null && itens.All(Leituras.ExclusaoValida) ? itens : null;
        }
        catch { return null; }
    }
    internal static bool GravarReferencia(string caminho, IEnumerable<string> itens)
    {
        try
        {
            if (App.CapturaArquivo != null) return false;
            var lista = itens.ToArray();
            if (lista.Any(i => !Leituras.ExclusaoValida(i))) return false;
            var texto = JsonSerializer.Serialize(lista.Distinct(StringComparer.OrdinalIgnoreCase));
            return System.Text.Encoding.UTF8.GetByteCount(texto) <= 64 * 1024 && ArquivosSeguros.GravarTexto(caminho, texto);
        }
        catch { return false; }
    }
}
