using System.Diagnostics;
using Sentinela.Servicos;

namespace Sentinela.Testes;

public class ArquivosSegurosTestes : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "Sentinela-Seguros-" + Guid.NewGuid());
    private string Caminho => Path.Combine(_pasta, "arquivo.json");

    public ArquivosSegurosTestes() => Directory.CreateDirectory(_pasta);

    [Fact]
    public void GravaLeEAceitaLimiteExato()
    {
        Assert.True(ArquivosSeguros.GravarTexto(Caminho, "ação"));
        Assert.Equal("ação", ArquivosSeguros.LerTexto(Caminho, 6));
        Assert.Null(ArquivosSeguros.LerTexto(Caminho, 5));
        Assert.True(ArquivosSeguros.GravarTexto(Caminho, "novo"));
        Assert.Equal("novo", ArquivosSeguros.LerTexto(Caminho, 6));
    }

    [Fact]
    public void TemporarioPreExistenteNaoEReutilizado()
    {
        File.WriteAllText(Caminho + ".tmp", "intacto");
        var outro = Caminho + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(outro, "também intacto");
        Assert.True(ArquivosSeguros.GravarTexto(Caminho, "novo"));
        Assert.Equal("intacto", File.ReadAllText(Caminho + ".tmp"));
        Assert.Equal("também intacto", File.ReadAllText(outro));
        Assert.Equal(3, Directory.GetFiles(_pasta).Length);
    }

    [Fact]
    public void JunctionNaPastaPaiRecusaTodasOperacoesSemCriarFilho()
    {
        var alvo = Path.Combine(_pasta, "alvo");
        var link = Path.Combine(_pasta, "junction");
        Directory.CreateDirectory(alvo);
        using var processo = Process.Start(new ProcessStartInfo(CaminhosSistema.Cmd)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            ArgumentList = { "/c", "mklink", "/J", link, alvo }
        });
        if (processo == null) return;
        processo.WaitForExit();
        if (processo.ExitCode != 0) return;
        try
        {
            var filho = Path.Combine(link, "filho");
            Assert.False(ArquivosSeguros.PastaSegura(link));
            Assert.False(ArquivosSeguros.PastaSegura(filho));
            Assert.False(Directory.Exists(Path.Combine(alvo, "filho")));
            var arquivo = Path.Combine(link, "arquivo.json");
            Assert.False(ArquivosSeguros.GravarTexto(arquivo, "novo"));
            Assert.False(ArquivosSeguros.Acrescentar(arquivo, "novo", 100));
            Assert.Null(ArquivosSeguros.LerTexto(arquivo, 100));
        }
        finally { Directory.Delete(link); }
    }

    [Fact]
    public void DestinoSymlinkNaoAlteraAlvo()
    {
        var alvo = Path.Combine(_pasta, "alvo.json");
        File.WriteAllText(alvo, "intacto");
        try { File.CreateSymbolicLink(Caminho, alvo); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { return; }
        Assert.Null(ArquivosSeguros.LerTexto(Caminho, 100));
        Assert.False(ArquivosSeguros.Acrescentar(Caminho, "não gravar", 100));
        Assert.True(ArquivosSeguros.GravarTexto(Caminho, "novo"));
        Assert.Equal("intacto", File.ReadAllText(alvo));
        Assert.Null(new FileInfo(Caminho).LinkTarget);
        Assert.Equal("novo", ArquivosSeguros.LerTexto(Caminho, 100));
    }

    [Fact]
    public void AcrescentarMantemRotacao()
    {
        var anterior = Path.Combine(_pasta, "diario-anterior.jsonl");
        Assert.True(ArquivosSeguros.Acrescentar(Caminho, "12345", 4));
        Assert.True(ArquivosSeguros.Acrescentar(Caminho, "novo", 4));
        Assert.Equal("12345", File.ReadAllText(anterior));
        Assert.Equal("novo", File.ReadAllText(Caminho));
    }

    [Fact]
    public void EntradasInvalidasNuncaLancam()
    {
        Assert.False(ArquivosSeguros.PastaSegura("\0"));
        Assert.False(ArquivosSeguros.GravarTexto("\0", "x"));
        Assert.False(ArquivosSeguros.Acrescentar("\0", "x", 10));
        Assert.Null(ArquivosSeguros.LerTexto("\0", 10));
        Assert.Null(ArquivosSeguros.LerTexto(Caminho, -1));
    }

    public void Dispose() => Directory.Delete(_pasta, true);
}
