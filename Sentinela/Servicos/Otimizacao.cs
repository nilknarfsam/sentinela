using System.Diagnostics;

namespace Sentinela.Servicos;

public sealed record PastaGrande(string Pasta, long Bytes, double Percentual)
{
    public string Nome => Pasta;
    public string Tamanho => Limpeza.Formatar(Bytes);
    public string Porcentagem => $"{Percentual:0.#}%";
}

public static class MaioresPastas
{
    public static Task<IReadOnlyList<PastaGrande>> Analisar(string raiz, CancellationToken ct = default, IProgress<string>? progresso = null) => Task.Run(async () =>
    {
        try
        {
            using var trava = new TravaPastas(raiz);
            var tamanhos = new System.Collections.Concurrent.ConcurrentBag<(string pasta, long bytes)>();
            await Parallel.ForEachAsync(Directory.GetDirectories(raiz), new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, (pasta, token) =>
            {
                try
                {
                    using var segura = new TravaPastas(pasta);
                    progresso?.Report(pasta);
                    tamanhos.Add((pasta, ArvoreDescartavel.Percorrer(pasta, false, token)));
                }
                catch { }
                return ValueTask.CompletedTask;
            });
            double total = tamanhos.Sum(t => (double)t.bytes);
            return (IReadOnlyList<PastaGrande>)tamanhos.OrderByDescending(t => t.bytes).Take(20)
                .Select(t => new PastaGrande(t.pasta, t.bytes, total == 0 ? 0 : 100 * t.bytes / total)).ToArray();
        }
        catch { return Array.Empty<PastaGrande>(); }
    });
}

public static class CachesDesenvolvimento
{
    public static List<ItemLimpeza> Itens()
    {
        var usuario = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        const string aviso = "Apagar é seguro, mas o próximo build baixa tudo de novo (mais lento e usa internet).";
        return [
            new() { Nome = "Gradle", Descricao = aviso, Pastas = [Path.Combine(usuario, ".gradle", "caches")], Marcado = false },
            new() { Nome = "npm", Descricao = aviso, Pastas = [Path.Combine(local, "npm-cache")], Marcado = false },
            new() { Nome = "NuGet", Descricao = aviso, Pastas = [Path.Combine(usuario, ".nuget", "packages")], Marcado = false },
            new() { Nome = "Pub", Descricao = aviso + " Depois rode flutter pub get nos projetos.", Pastas = [Path.Combine(local, "Pub", "Cache")], Marcado = false }
        ];
    }
}

public static class CachesNavegadores
{
    internal static bool Fechado(string processo)
    {
        try
        {
            var lista = Process.GetProcessesByName(processo);
            var fechado = lista.Length == 0;
            foreach (var p in lista) p.Dispose();
            return fechado;
        }
        catch { return false; }
    }
    public static List<ItemLimpeza> Itens()
    {
        var lista = new List<ItemLimpeza>();
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var (nome, processo, raiz) in new[] { ("Chrome", "chrome", Path.Combine(local, "Google", "Chrome", "User Data")), ("Edge", "msedge", Path.Combine(local, "Microsoft", "Edge", "User Data")) })
        {
            try
            {
                using var trava = new TravaPastas(raiz);
                foreach (var perfil in Directory.GetDirectories(raiz))
                {
                    try
                    {
                        using var segura = new TravaPastas(perfil);
                        var pastas = new[] { Path.Combine(perfil, "Cache", "Cache_Data"), Path.Combine(perfil, "Code Cache"), Path.Combine(perfil, "GPUCache") }.Where(Directory.Exists).ToArray();
                        if (pastas.Length == 0) continue;
                        lista.Add(new() { Nome = $"{nome} — {Path.GetFileName(perfil)}", Processo = processo, Marcado = false, Pastas = pastas,
                            Descricao = "Cache recriado ao navegar. Cookies, senhas e histórico são preservados." });
                    }
                    catch { /* Um perfil indisponível não impede os demais. */ }
                }
            }
            catch { }
        }
        return lista;
    }
}

// Apenas resultados desta execução. Não mede projetos dentro do Checkup.
public static class ResumoOtimizacao
{
    private static readonly object Trava = new();
    private static readonly Dictionary<string, long> Valores = [];
    public static void Atualizar(string secao, long bytes) { lock (Trava) Valores[secao] = bytes; }
    public static (long bytes, bool projetos) Ler() { lock (Trava) return (Valores.Where(v => v.Key != "Windows").Sum(v => v.Value), Valores.ContainsKey("Projetos")); }
}
