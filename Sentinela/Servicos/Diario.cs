using System.Text.Json;

namespace Sentinela.Servicos;

public sealed record EntradaDiario(DateTime Data, string Tipo, string Mensagem, long? Bytes = null, string? ResultadoVarredura = null,
    TipoVarredura? TipoVarreduraExecutada = null, string? NomeAmeaca = null);

public static class Diario
{
    private static readonly object Trava = new();
    private static bool _desligado;

    public static bool Desligado
    {
        get { lock (Trava) return _desligado; }
        set { lock (Trava) _desligado = value; }
    }

    internal static string? PastaTeste;

    private static string Caminho => Path.Combine(PastaTeste ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinela"), "diario.jsonl");

    public static void Registrar(string tipo, string mensagem, long? bytes = null, string? resultadoVarredura = null,
        TipoVarredura? tipoVarredura = null, string? nomeAmeaca = null, DateTime? data = null)
    {
        lock (Trava)
        {
            if (_desligado) return;
            try
            {
                var caminho = Caminho;
                ArquivosSeguros.Acrescentar(caminho,
                    JsonSerializer.Serialize(new EntradaDiario(data ?? DateTime.Now, tipo, mensagem, bytes, resultadoVarredura, tipoVarredura, nomeAmeaca)) + Environment.NewLine,
                    5 * 1024 * 1024);
            }
            catch { /* O diário não pode interromper o aplicativo. */ }
        }
    }

    internal static long SomarLiberado(IEnumerable<EntradaDiario> entradas, DateTime agora) => entradas
        .Where(e => e.Tipo == "limpeza" && e.Data >= agora.AddDays(-30) && e.Data <= agora && e.Bytes > 0)
        .Sum(e => e.Bytes ?? 0);

    public static List<EntradaDiario> Ler(int dias)
    {
        var entradas = new List<EntradaDiario>();
        try
        {
            var limite = DateTime.Now.AddDays(-dias);
            var caminho = Caminho;
            foreach (var arquivo in new[] { Path.Combine(Path.GetDirectoryName(caminho)!, "diario-anterior.jsonl"), caminho })
            {
                try
                {
                    var texto = ArquivosSeguros.LerTexto(arquivo, 6 * 1024 * 1024);
                    if (texto == null) continue;
                    using var leitor = new StringReader(texto);
                    while (leitor.ReadLine() is { } linha)
                    {
                        try
                        {
                            var entrada = JsonSerializer.Deserialize<EntradaDiario>(linha);
                            if (entrada != null && entrada.Tipo != null && entrada.Mensagem != null && entrada.Data >= limite)
                                entradas.Add(entrada);
                        }
                        catch (JsonException) { /* Ignora linhas inválidas. */ }
                    }
                }
                catch { /* Arquivo ausente ou indisponível. */ }
            }
            return entradas.OrderByDescending(e => e.Data).ToList();
        }
        catch { return entradas; /* O diário não pode interromper o aplicativo. */ }
    }
}
