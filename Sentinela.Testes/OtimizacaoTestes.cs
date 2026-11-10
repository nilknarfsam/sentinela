using System.Diagnostics;
using Sentinela.Servicos;

namespace Sentinela.Testes;

[Collection("Diário")]
public class OtimizacaoTestes : IDisposable
{
    private readonly string _raiz = Directory.CreateTempSubdirectory("Sentinela-3C-").FullName;
    private readonly List<string> _pastasAnteriores = Configuracoes.Atual.PastasProjetos;
    public OtimizacaoTestes() => Configuracoes.Atual.PastasProjetos = [_raiz];
    private string Pasta(string nome) => Directory.CreateDirectory(Path.Combine(_raiz, nome)).FullName;
    private static void Arquivo(string pasta, string nome, int bytes = 1) => File.WriteAllBytes(Path.Combine(pasta, nome), new byte[bytes]);

    [Fact]
    public void PastaTemporariaSemLinksPodeSerTravada()
    {
        using var trava = new TravaPastas(_raiz);
    }

    [Theory]
    [InlineData("pubspec.yaml", "build,.dart_tool")]
    [InlineData("package.json", "node_modules")]
    [InlineData("app.csproj", "bin,obj")]
    [InlineData("app.fsproj", "bin,obj")]
    [InlineData("app.vbproj", "bin,obj")]
    [InlineData("build.gradle", "build,.gradle")]
    [InlineData("build.gradle.kts", "build,.gradle")]
    [InlineData("arquivo.txt", "")]
    public void RegrasExigemMarcadorNaRaiz(string marcador, string esperado)
    {
        var pastas = new[] { "build", ".dart_tool", "node_modules", "bin", "obj", ".gradle" };
        Assert.Equal(esperado.Split(',', StringSplitOptions.RemoveEmptyEntries).Order(), LimpezaProjetos.SobrasDoProjeto([marcador], pastas).Order());
    }
    [Theory]
    [InlineData("flutter.bat", true)]
    [InlineData("flutter", true)]
    [InlineData("flutter.exe", false)]
    public void IdentificaSdk(string arquivo, bool esperado) => Assert.Equal(esperado, LimpezaProjetos.EhSdkFlutter([arquivo]));

    [Fact]
    public async Task SdkNuncaListadoNemPorRaizInterna()
    {
        var sdk = Pasta("sdk");
        Arquivo(Directory.CreateDirectory(Path.Combine(sdk, "bin")).FullName, "flutter.bat");
        Arquivo(sdk, "pubspec.yaml");
        Directory.CreateDirectory(Path.Combine(sdk, ".dart_tool"));
        Assert.Empty(await LimpezaProjetos.Analisar([_raiz]));
        Assert.Empty(await LimpezaProjetos.Analisar([sdk]));
        Assert.Equal(0, LimpezaProjetos.Limpar(new(sdk, "Flutter", Path.Combine(sdk, ".dart_tool"), 0, "")));
        Assert.True(Directory.Exists(Path.Combine(sdk, ".dart_tool")));
    }
    private static bool Junction(string origem, string alvo)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(CaminhosSistema.Cmd, $"/c mklink /J \"{origem}\" \"{alvo}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
            p?.WaitForExit();
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }
    [Fact]
    public async Task LimpezaEMedicaoPreservamAlvoDaJunction()
    {
        var projeto = Pasta("projeto");
        Arquivo(projeto, "pubspec.yaml");
        var build = Directory.CreateDirectory(Path.Combine(projeto, "build")).FullName;
        Arquivo(build, "descartavel", 15);
        var dart = Directory.CreateDirectory(Path.Combine(projeto, ".dart_tool")).FullName;
        Arquivo(dart, "descartavel", 5);
        var cache = Pasta("pub cache");
        Arquivo(cache, "preservado", 1000);
        var link = Path.Combine(build, "plugins");
        if (!Junction(link, cache)) return;
        try
        {
            var sobras = await LimpezaProjetos.Analisar([_raiz]);
            Assert.Equal(2, sobras.Count);
            Assert.Equal(20, sobras.Sum(s => s.Bytes));
            Assert.Equal(20, sobras.Sum(s => LimpezaProjetos.Limpar(s)));
            Assert.True(File.Exists(Path.Combine(cache, "preservado")));
            Assert.False(Directory.Exists(build));
            Assert.False(Directory.Exists(dart));
        }
        finally { if (Directory.Exists(link)) Directory.Delete(link); }
    }
    [Fact]
    public async Task RecusaMarcadorRemovidoEPastaQueVirouLink()
    {
        var projeto = Pasta("projeto");
        Arquivo(projeto, "package.json");
        var sobra = Directory.CreateDirectory(Path.Combine(projeto, "node_modules")).FullName;
        Arquivo(sobra, "arquivo", 10);
        var lista = await LimpezaProjetos.Analisar([_raiz]);
        File.Delete(Path.Combine(projeto, "package.json"));
        Assert.Equal(0, LimpezaProjetos.Limpar(Assert.Single(lista)));
        Assert.True(File.Exists(Path.Combine(sobra, "arquivo")));
        Arquivo(projeto, "package.json");
        var movida = Path.Combine(projeto, "movida");
        Directory.Move(sobra, movida);
        if (!Junction(sobra, movida)) return;
        try { Assert.Equal(0, LimpezaProjetos.Limpar(lista[0])); Assert.True(File.Exists(Path.Combine(movida, "arquivo"))); }
        finally { Directory.Delete(sobra); }
    }
    [Fact]
    public async Task MaioresOrdenaCalculaPercentualENaoSegueJunction()
    {
        Arquivo(Pasta("menor"), "arquivo", 10);
        Arquivo(Pasta("maior"), "arquivo", 30);
        var link = Path.Combine(_raiz, "link");
        if (!Junction(link, Path.Combine(_raiz, "maior"))) return;
        try
        {
            var lista = await MaioresPastas.Analisar(_raiz);
            Assert.Equal(2, lista.Count);
            Assert.Equal(30, lista[0].Bytes);
            Assert.Equal(75, lista[0].Percentual);
            Assert.Equal(10, lista[1].Bytes);
        }
        finally { Directory.Delete(link); }
    }
    [Fact]
    public async Task CancelamentoNaoLanca()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Empty(await LimpezaProjetos.Analisar([_raiz], cts.Token));
        Assert.Empty(await MaioresPastas.Analisar(_raiz, cts.Token));
    }
    [Fact]
    public async Task IgnoraGitLimitaProfundidadeEDeduplicaRaizes()
    {
        var projeto = Pasta("projeto");
        Arquivo(projeto, "package.json");
        var sobra = Directory.CreateDirectory(Path.Combine(projeto, "node_modules")).FullName;
        Arquivo(sobra, "package.json");
        Directory.CreateDirectory(Path.Combine(sobra, "node_modules"));
        var git = Pasta(".git"); Arquivo(git, "pubspec.yaml"); Directory.CreateDirectory(Path.Combine(git, "build"));
        var profundo = Pasta(Path.Combine("a", "b", "c", "d"));
        Arquivo(profundo, "pubspec.yaml"); Directory.CreateDirectory(Path.Combine(profundo, "build"));
        var lista = await LimpezaProjetos.Analisar([sobra, projeto, _raiz]);
        Assert.Equal(sobra, Assert.Single(lista).Pasta);
    }
    [Fact]
    public void CachesComecamDesmarcados() => Assert.All(CachesDesenvolvimento.Itens(), i => Assert.False(i.Marcado));
    [Fact]
    public void SomaSomenteLimpezasPositivasNosUltimos30Dias()
    {
        var agora = DateTime.Now;
        EntradaDiario[] entradas = [new(agora, "limpeza", "", 10), new(agora.AddDays(-29), "limpeza", "", 20), new(agora.AddDays(-31), "limpeza", "", 100), new(agora, "checkup", "", 100), new(agora, "limpeza", "", -1), new(agora.AddDays(1), "limpeza", "", 100)];
        Assert.Equal(30, Diario.SomarLiberado(entradas, agora));
    }
    [Fact]
    public void PastasProtegidasNormalizadasSaoRecusadasAntesDeEnumerar()
    {
        string[] proibidas = [@"C:\", @"D:\", "", "relativo", @"C:relativo", @"\\?\C:\", @"\\.\C:\"];
        Environment.SpecialFolder[] especiais = [Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.MyDocuments,
            Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.Windows, Environment.SpecialFolder.System,
            Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.CommonApplicationData,
            Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.MyVideos];
        proibidas = proibidas.Concat(especiais.Select(Environment.GetFolderPath))
            .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"))
            .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32")).ToArray();
        foreach (var pasta in proibidas)
        {
            Assert.True(ArvoreDescartavel.PastaProibidaParaApagar(pasta), pasta);
            bool enumerou = false;
            Assert.Equal(0, ArvoreDescartavel.Percorrer(pasta, true, aoEnumerar: () =>
            {
                enumerou = true;
                // Mesmo diante de uma regressão, interrompe antes de enumerar uma pasta real.
                throw new InvalidOperationException("Pasta protegida não deve ser enumerada");
            }));
            Assert.False(enumerou);
            Assert.Equal(0, Limpeza.Limpar(new() { Nome = "Protegida", Descricao = "", Pastas = [pasta] }));
            Assert.Equal(0, LimpezaProjetos.Limpar(new(pasta, "", pasta, 0, "")));
            if (!string.IsNullOrEmpty(pasta) && Path.IsPathFullyQualified(pasta))
                Assert.True(ArvoreDescartavel.PastaProibidaParaApagar(Path.Combine(pasta, "x", "..") + Path.DirectorySeparatorChar));
        }
        Assert.False(ArvoreDescartavel.PastaProibidaParaApagar(@"C:\PROJETOS\x\build"));
        Assert.False(ArvoreDescartavel.PastaProibidaParaApagar(Path.Combine(Path.GetTempPath(), "algo")));
    }

    [Fact]
    public async Task ConfiguracaoInvalidaIgnoradaEProjetoExigeRaizAtual()
    {
        var projeto = Pasta("projeto");
        Arquivo(projeto, "package.json");
        var sobra = Directory.CreateDirectory(Path.Combine(projeto, "node_modules")).FullName;
        Arquivo(sobra, "arquivo", 10);
        string[] invalidas = ["", "relativa", @"C:\", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Path.Combine(_raiz, "nao existe")];
        var json = System.Text.Json.JsonSerializer.Serialize(new Configuracoes { PastasProjetos = invalidas.Append(Path.Combine(_raiz, ".")).ToList() });
        var config = System.Text.Json.JsonSerializer.Deserialize<Configuracoes>(json)!;
        Assert.Equal([_raiz], config.PastasProjetosValidas());
        Assert.Equal(invalidas.Length, config.PastasIgnoradas.Count);
        Assert.All(config.PastasIgnoradas, p => Assert.Contains("Ignorada: ", p.ToString()));
        Assert.Empty(await LimpezaProjetos.Analisar(invalidas));
        var item = Assert.Single(await LimpezaProjetos.Analisar(config.PastasProjetos));
        Configuracoes.Atual.PastasProjetos = [Pasta("outra")];
        Assert.Equal(0, LimpezaProjetos.Limpar(item));
        Assert.True(File.Exists(Path.Combine(sobra, "arquivo")));
    }

    [Fact]
    public async Task RemoveSomenteLeituraEmSobraTemporaria()
    {
        var projeto = Pasta("projeto");
        Arquivo(projeto, "package.json");
        var sobra = Directory.CreateDirectory(Path.Combine(projeto, "node_modules")).FullName;
        Arquivo(sobra, "arquivo", 10);
        File.SetAttributes(Path.Combine(sobra, "arquivo"), FileAttributes.ReadOnly);
        var resultado = new ResultadoLimpeza();
        Assert.Equal(10, LimpezaProjetos.Limpar(Assert.Single(await LimpezaProjetos.Analisar([_raiz])), resultado: resultado));
        Assert.Equal(0, resultado.NaoApagados);
        Assert.False(Directory.Exists(sobra));
    }

    [Fact]
    public async Task ProjetoComAncestralJunctionNaoELimpo()
    {
        var real = Pasta("real");
        var projeto = Directory.CreateDirectory(Path.Combine(real, "projeto")).FullName;
        Arquivo(projeto, "package.json");
        var sobra = Directory.CreateDirectory(Path.Combine(projeto, "node_modules")).FullName;
        Arquivo(sobra, "arquivo", 10);
        var link = Path.Combine(_raiz, "link");
        if (!Junction(link, real)) return;
        try
        {
            var viaLink = Path.Combine(link, "projeto");
            Assert.Empty(await LimpezaProjetos.Analisar([viaLink]));
            Assert.Equal(0, LimpezaProjetos.Limpar(new(viaLink, "Node", Path.Combine(viaLink, "node_modules"), 10, "")));
            Assert.True(File.Exists(Path.Combine(sobra, "arquivo")));
        }
        finally { Directory.Delete(link); }
    }

    [Fact]
    public void WindowsRespeitaIdadeECancelamentoEContaArquivoEmUso()
    {
        foreach (var modelo in Limpeza.Itens().Where(i => i.HorasMinimas > 0))
        {
            var pasta = Pasta(Guid.NewGuid().ToString());
            Arquivo(pasta, "recente", 5);
            Arquivo(pasta, "antigo", 10);
            File.SetLastWriteTime(Path.Combine(pasta, "antigo"), DateTime.Now.AddHours(-modelo.HorasMinimas - 1));
            var item = new ItemLimpeza { Nome = modelo.Nome, Descricao = "", Pastas = [pasta], HorasMinimas = modelo.HorasMinimas };
            Assert.Equal(10, Limpeza.Medir(item));
            Assert.Equal(0, Limpeza.Limpar(item, new CancellationToken(true)));
            Assert.True(File.Exists(Path.Combine(pasta, "antigo")));
            Assert.Equal(10, Limpeza.Limpar(item));
            Assert.True(File.Exists(Path.Combine(pasta, "recente")));
        }
        var bloqueada = Pasta("em uso");
        Arquivo(bloqueada, "aberto", 10);
        using var arquivo = new FileStream(Path.Combine(bloqueada, "aberto"), FileMode.Open, FileAccess.Read, FileShare.None);
        var resultado = new ResultadoLimpeza();
        Assert.Equal(0, Limpeza.Limpar(new() { Nome = "", Descricao = "", Pastas = [bloqueada] }, resultado: resultado));
        Assert.Equal(1, resultado.NaoApagados);
    }

    [Fact]
    public void CancelamentoDurantePercursoParaEntreArquivos()
    {
        Arquivo(_raiz, "um", 10);
        Arquivo(_raiz, "dois", 10);
        using var ct = new CancellationTokenSource();
        int visitados = 0;
        Assert.Equal(10, ArvoreDescartavel.Percorrer(_raiz, true, ct.Token, aceitar: _ =>
        {
            if (++visitados == 2) ct.Cancel();
            return true;
        }));
        Assert.Single(Directory.GetFiles(_raiz));
    }

    public void Dispose()
    {
        Configuracoes.Atual.PastasProjetos = _pastasAnteriores;
        Directory.Delete(_raiz, true);
    }
}
