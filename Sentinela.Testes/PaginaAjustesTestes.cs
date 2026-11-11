using Sentinela.Paginas;

namespace Sentinela.Testes;

public sealed class PaginaAjustesTestes
{
    [Theory]
    [InlineData("https://github.com/nilknarfsam")]
    [InlineData("https://samfieldsoak.com.br")]
    [InlineData("https://www.instagram.com/samfieldsoak")]
    [InlineData("https://www.linkedin.com/in/franklin-carvalho-45032a164/")]
    public void PermiteSomenteOsQuatroLinksAutorizados(string alvo)
        => Assert.True(PaginaAjustes.AlvoAjudaPermitido(alvo));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://github.com/nilknarfsam")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("https://github.com.evil.test/nilknarfsam")]
    [InlineData("https://github.com/nilknarfsam.evil.test")]
    [InlineData("https://github.com/nilknarfsam/extra")]
    [InlineData("https://github.com/nilknarfsam/")]
    [InlineData("https://github.com/nilknarfsam?utm_source=share")]
    [InlineData("https://github.com/nilknarfsam#perfil")]
    [InlineData("https://samfieldsoak.com.br/")]
    [InlineData("https://samfieldsoak.com.br/?utm_source=share")]
    [InlineData("https://www.instagram.com/samfieldsoak/")]
    [InlineData("https://www.instagram.com/samfieldsoak?utm_source=share")]
    [InlineData("https://www.linkedin.com/in/franklin-carvalho-45032a164")]
    [InlineData("https://www.linkedin.com/in/franklin-carvalho-45032a164/?utm_source=share")]
    [InlineData("https://www.linkedin.com/in/franklin-carvalho-45032a164/#sobre")]
    [InlineData("https://www.linkedin.com.evil.test/in/franklin-carvalho-45032a164/")]
    [InlineData("https://www.linkedin.com/in/franklin-carvalho-45032a164/extra")]
    public void RecusaUrlNaoAutorizadaOuVariante(string? alvo)
        => Assert.False(PaginaAjustes.AlvoAjudaPermitido(alvo));
}
