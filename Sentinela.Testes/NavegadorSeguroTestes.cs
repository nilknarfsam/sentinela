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
    public void RecusaOQueNaoForHttpsSeguro(string? url)
    {
        Assert.False(NavegadorSeguro.UrlValida(url));
        Assert.False(NavegadorSeguro.Abrir(url));
    }
}
