namespace Sentinela.Servicos;

public sealed record SobraProjeto(string Projeto, string Tipo, string Pasta, long Bytes, string ComoRecriar);

public static class LimpezaProjetos
{
    internal static IReadOnlyList<string> SobrasDoProjeto(IEnumerable<string> arquivosNaRaiz, IEnumerable<string> pastasNaRaiz)
    {
        var arquivos = arquivosNaRaiz.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (arquivos.Contains("pubspec.yaml")) nomes.UnionWith(["build", ".dart_tool"]);
        if (arquivos.Contains("package.json")) nomes.Add("node_modules");
        if (arquivos.Any(a => new[] { ".csproj", ".fsproj", ".vbproj" }.Contains(Path.GetExtension(a), StringComparer.OrdinalIgnoreCase))) nomes.UnionWith(["bin", "obj"]);
        if (arquivos.Contains("build.gradle") || arquivos.Contains("build.gradle.kts")) nomes.UnionWith(["build", ".gradle"]);
        return pastasNaRaiz.Where(p => nomes.Contains(Path.GetFileName(p))).ToArray();
    }

    internal static bool EhSdkFlutter(IEnumerable<string> arquivosNoBin) => arquivosNoBin.Any(a =>
        string.Equals(Path.GetFileName(a), "flutter.bat", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetFileName(a), "flutter", StringComparison.OrdinalIgnoreCase));

    private static bool Sdk(string pasta)
    {
        var bin = Path.Combine(pasta, "bin");
        if (!Directory.Exists(bin)) return false;
        try { using var trava = new TravaPastas(bin); return EhSdkFlutter(Directory.EnumerateFiles(bin)); }
        catch { return false; }
    }

    private static (string tipo, string recriar) Descrever(string projeto, string sobra)
    {
        if (File.Exists(Path.Combine(projeto, "pubspec.yaml")) && sobra is "build" or ".dart_tool") return ("Flutter/Dart", "flutter pub get / próximo build");
        if (sobra == "node_modules") return ("Node", "npm install");
        if (sobra == ".gradle" || File.Exists(Path.Combine(projeto, "build.gradle")) || File.Exists(Path.Combine(projeto, "build.gradle.kts"))) return ("Android/Gradle", "Próximo build");
        return (".NET", "Próximo build");
    }

    public static Task<IReadOnlyList<SobraProjeto>> Analisar(IEnumerable<string> raizes, CancellationToken ct = default, IProgress<string>? progresso = null) => Task.Run(async () =>
    {
        var encontrados = new Dictionary<string, SobraProjeto>(StringComparer.OrdinalIgnoreCase);
        void Visitar(string pasta, int nivel)
        {
            if (ct.IsCancellationRequested || encontrados.Keys.Any(s => pasta.Equals(s, StringComparison.OrdinalIgnoreCase) || pasta.StartsWith(s + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) return;
            try
            {
                using var trava = new TravaPastas(pasta);
                if (Sdk(pasta) || Path.GetFileName(pasta).Equals(".git", StringComparison.OrdinalIgnoreCase)) return;
                progresso?.Report(pasta);
                var pastas = Directory.GetDirectories(pasta);
                var sobras = SobrasDoProjeto(Directory.GetFiles(pasta), pastas);
                foreach (var sobra in sobras)
                {
                    if ((File.GetAttributes(sobra) & FileAttributes.ReparsePoint) != 0) continue;
                    using var segura = new TravaPastas(sobra);
                    var (tipo, recriar) = Descrever(pasta, Path.GetFileName(sobra));
                    encontrados.TryAdd(sobra, new(pasta, tipo, sobra, 0, recriar));
                }
                if (nivel < 3)
                    foreach (var sub in pastas.Except(sobras, StringComparer.OrdinalIgnoreCase)) Visitar(sub, nivel + 1);
            }
            catch { }
        }
        try
        {
            foreach (var raiz in raizes.Select(r => Configuracoes.ValidarPastaProjeto(r, out var p, out _) ? p : null)
                .OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(r => r.Length))
            {
                try
                {
                    using var trava = new TravaPastas(raiz);
                    bool sdk = false;
                    for (var d = new DirectoryInfo(raiz); d != null; d = d.Parent) if (Sdk(d.FullName)) { sdk = true; break; }
                    if (!sdk) Visitar(raiz, 0);
                }
                catch { }
            }
            var lista = encontrados.Values.ToArray();
            await Parallel.ForEachAsync(Enumerable.Range(0, lista.Length), new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, (i, token) =>
            {
                progresso?.Report(lista[i].Projeto);
                lista[i] = lista[i] with { Bytes = ArvoreDescartavel.Percorrer(lista[i].Pasta, false, token) };
                return ValueTask.CompletedTask;
            });
            return (IReadOnlyList<SobraProjeto>)lista.OrderByDescending(s => s.Bytes).ToArray();
        }
        catch { return Array.Empty<SobraProjeto>(); }
    });

    public static long Limpar(SobraProjeto sobra, CancellationToken ct = default, ResultadoLimpeza? resultado = null)
    {
        try
        {
            if (ct.IsCancellationRequested || ArvoreDescartavel.PastaProibidaParaApagar(sobra.Projeto) || ArvoreDescartavel.PastaProibidaParaApagar(sobra.Pasta)) return 0;
            var projeto = ArvoreDescartavel.Normalizar(sobra.Projeto);
            if (!Configuracoes.Atual.PastasProjetosValidas().Any(r => projeto.Equals(r, StringComparison.OrdinalIgnoreCase)
                || projeto.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) return 0;
            using var trava = new TravaPastas(sobra.Projeto);
            // Também recusa projetos dentro de um SDK, mesmo se um registro antigo for reaproveitado.
            for (var d = new DirectoryInfo(sobra.Projeto); d != null; d = d.Parent) if (Sdk(d.FullName)) return 0;
            var pasta = Path.GetFullPath(sobra.Pasta);
            if (!string.Equals(Path.GetDirectoryName(pasta), Path.GetFullPath(sobra.Projeto), StringComparison.OrdinalIgnoreCase)) return 0;
            if (!SobrasDoProjeto(Directory.GetFiles(sobra.Projeto), Directory.GetDirectories(sobra.Projeto)).Contains(pasta, StringComparer.OrdinalIgnoreCase)) return 0;
            long bytes;
            using (var segura = new TravaPastas(pasta)) bytes = ArvoreDescartavel.Percorrer(pasta, true, ct, removerLinks: true, resultado: resultado);
            if (!ct.IsCancellationRequested && !Directory.EnumerateFileSystemEntries(pasta).Any())
                try { Directory.Delete(pasta); } catch { resultado?.RegistrarFalha(); }
            return bytes;
        }
        catch { return 0; }
    }
}

