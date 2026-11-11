using Sentinela.Servicos;

namespace Sentinela.Testes;

public sealed class NavegadorSeguroTestes
{
    [Theory]
    [InlineData("https://github.com/nilknarfsam")]
    [InlineData("https://samfieldsoak.com.br")]
    [InlineData("https://www.linkedin.com/in/exemplo/")]
    public void AceitaHttpsAbsoluto(string url) => Assert.True(NavegadorSeguro.UrlValida(url));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://github.com/nilknarfsam")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ms-settings:windowsupdate")]
    [InlineData("C:\\Windows\\System32\\cmd.exe")]
    [InlineData("github.com/nilknarfsam")]
    [InlineData("https://usuario:senha@github.com/")]
    [InlineData("https://github.com/\"--outro")]
    [InlineData("https://github.com/\nlinha")]
    [InlineData(" https://github.com/nilknarfsam")]
    [InlineData("https://github.com/nilknarfsam ")]
    [InlineData("https:///x")]
    [InlineData("/select,C:\\")]
    [InlineData(",/e,C:\\")]
    [InlineData("-https://github.com")]
    [InlineData("https://github.com/\u202Eabc")]
    public void RecusaOQueNaoForHttpsSeguro(string? url)
    {
        Assert.False(NavegadorSeguro.UrlValida(url));
        Assert.Null(NavegadorSeguro.Montar(url));
        Assert.False(NavegadorSeguro.Abrir(url));
    }

    [Fact]
    public void MontaExplorerSemShellComUmUnicoArgumentoNormalizado()
    {
        var inicio = NavegadorSeguro.Montar("https://github.com/nilknarfsam");
        Assert.NotNull(inicio);
        Assert.EndsWith("explorer.exe", inicio!.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.True(Path.IsPathFullyQualified(inicio.FileName));
        Assert.False(inicio.UseShellExecute);
        Assert.Equal(["https://github.com/nilknarfsam"], inicio.ArgumentList);
        Assert.Equal("", inicio.Arguments);
    }

    [Fact]
    public void EspacoNoMeioViraEscapado()
    {
        var inicio = NavegadorSeguro.Montar("https://github.com/a b");
        Assert.NotNull(inicio);
        Assert.Equal("https://github.com/a%20b", inicio!.ArgumentList.Single());
    }
}
