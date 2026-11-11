using System.Diagnostics;
using Sentinela.Servicos;

namespace Sentinela.Testes;

public class ArquivosSegurosTestes : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "Sentinela-Seguros-" + Guid.NewGuid());
    private string Caminho => Path.Combine(_pasta, "arquivo.json");

    public ArquivosSegurosTestes() => Directory.CreateDirectory(_pasta);

    [Theory]
    [InlineData("relativo.html", "completo")]
    [InlineData(@"C:\relatorio.html", "raiz")]
    [InlineData(@"C:\", "raiz")]
    [InlineData(@"C:\Pasta\relatorio.html:stream", "fluxo alternativo")]
    [InlineData(@"C:\Pasta:stream\relatorio.html", "fluxo alternativo")]
    [InlineData(@"\\servidor\pasta\relatorio.html", "UNC")]
    [InlineData(@"\\?\C:\Pasta\relatorio.html", "dispositivo")]
    [InlineData(@"\\.\C:\Pasta\relatorio.html", "dispositivo")]
    [InlineData(@"C:\Pasta\relatorio.exe", "extensão")]
    [InlineData(@"C:\Pasta\relatorio.html.exe", "extensão")]
    [InlineData(@"C:\Pasta.\relatorio.html", "ponto ou espaço")]
    [InlineData(@"C:\Pasta \relatorio.html", "ponto ou espaço")]
    public void ExportacaoRecusaCaminhosPerigosos(string caminho, string motivo)
    {
        Assert.False(ArquivosSeguros.GravarExportacao(caminho, "texto", out var recusa));
        Assert.Contains(motivo, recusa);
    }

    [Theory]
    [InlineData(Environment.SpecialFolder.Windows)]
    [InlineData(Environment.SpecialFolder.ProgramFiles)]
    [InlineData(Environment.SpecialFolder.ProgramFilesX86)]
    [InlineData(Environment.SpecialFolder.CommonApplicationData)]
    public void ExportacaoRecusaPastasProtegidas(Environment.SpecialFolder especial)
    {
        var pasta = Environment.GetFolderPath(especial);
        Assert.False(string.IsNullOrEmpty(pasta));
        Assert.False(ArquivosSeguros.GravarExportacao(Path.Combine(pasta, "SentinelaTeste", "relatorio.html"), "texto", out var motivo));
        Assert.Contains("protegida", motivo);
    }

    [Fact]
    public void ExportacaoNaoSobrescreveOutroTipoEAceitaHtm()
    {
        File.WriteAllText(Caminho, "intacto");
        Assert.False(ArquivosSeguros.GravarExportacao(Caminho, "novo", out var motivo));
        Assert.Contains("sobrescrever", motivo);
        Assert.Equal("intacto", File.ReadAllText(Caminho));
        Assert.True(ArquivosSeguros.GravarExportacao(Path.Combine(_pasta, "relatorio.HTM"), "texto"));
    }

    [Theory]
    [InlineData(0x9000001Au, true)]
    [InlineData(0x9000101Au, true)]
    [InlineData(0x9000F01Au, true)]
    [InlineData(0xA0000003u, false)]
    [InlineData(0xA000000Cu, false)]
    [InlineData(0x8000001Bu, false)]
    [InlineData(0u, false)]
    [InlineData(0x9001001Au, false)]
    public void ClassificaSomenteTagsDeNuvem(uint tag, bool aceita) => Assert.Equal(aceita, ArquivosSeguros.TagDeNuvem(tag));

    [Fact]
    public void ExportacaoLimitaBytesUtf8ESubstituiArquivoComSeguranca()
    {
        var Caminho = Path.Combine(_pasta, "relatorio.html");
        var conteudo = new string('á', 1024 * 1024);
        Assert.True(ArquivosSeguros.GravarExportacao(Caminho, conteudo));
        Assert.Equal(2 * 1024 * 1024, new FileInfo(Caminho).Length);
        Assert.False(ArquivosSeguros.GravarExportacao(Caminho, conteudo + "a"));
        Assert.Equal(conteudo, File.ReadAllText(Caminho));
        Assert.True(ArquivosSeguros.GravarExportacao(Caminho, "substituído"));
        Assert.Equal("substituído", File.ReadAllText(Caminho));
        Assert.False(ArquivosSeguros.GravarExportacao("relativo.html", "texto"));
    }

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
            Assert.False(ArquivosSeguros.GravarExportacao(Path.Combine(filho, "relatorio.html"), "texto", out var motivo));
            Assert.Contains("junção", motivo);
            Assert.False(Directory.Exists(Path.Combine(alvo, "filho")));
            var arquivo = Path.Combine(link, "arquivo.json");
            Assert.False(ArquivosSeguros.GravarTexto(arquivo, "novo"));
            Assert.False(ArquivosSeguros.GravarExportacao(arquivo, "novo"));
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
        Assert.False(ArquivosSeguros.GravarExportacao(Caminho, "não gravar"));
        var linkHtml = Path.Combine(_pasta, "link.html");
        File.CreateSymbolicLink(linkHtml, alvo);
        Assert.False(ArquivosSeguros.GravarExportacao(linkHtml, "não gravar", out var motivo));
        Assert.Contains("link", motivo);
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
