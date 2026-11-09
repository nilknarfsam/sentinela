using System.Text.Json;

namespace Sentinela.Servicos;

public sealed record EntradaDiario(DateTime Data, string Tipo, string Mensagem, long? Bytes = null);

public static class Diario
{
    private static readonly object Trava = new();
    private static bool _desligado;

    public static bool Desligado
    {
        get { lock (Trava) return _desligado; }
        set { lock (Trava) _desligado = value; }
    }

    private static string Caminho => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinela", "diario.jsonl");

    public static void Registrar(string tipo, string mensagem, long? bytes = null)
    {
        lock (Trava)
        {
            if (_desligado) return;
            try
            {
                var caminho = Caminho;
                Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
                if (File.Exists(caminho) && new FileInfo(caminho).Length > 5 * 1024 * 1024)
                    File.Move(caminho, Path.Combine(Path.GetDirectoryName(caminho)!, "diario-anterior.jsonl"), true);
                File.AppendAllText(caminho, JsonSerializer.Serialize(new EntradaDiario(DateTime.Now, tipo, mensagem, bytes)) + Environment.NewLine);
            }
            catch { /* O diário não pode interromper o aplicativo. */ }
        }
    }

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
                    using var stream = new FileStream(arquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var leitor = new StreamReader(stream);
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
