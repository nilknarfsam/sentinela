using Sentinela.Servicos;

namespace Sentinela.Testes;

public class InicializacaoTestes
{
    [Fact]
    public void AtivoPeloValor_NuloEstaAtivo() => Assert.True(Inicializacao.AtivoPeloValor(null));

    [Fact]
    public void AtivoPeloValor_VazioEstaAtivo() => Assert.True(Inicializacao.AtivoPeloValor(Array.Empty<byte>()));

    [Theory]
    [InlineData(0x00, true)]
    [InlineData(0x01, false)]
    [InlineData(0x02, true)]
    [InlineData(0x03, false)]
    [InlineData(0x06, true)]
    [InlineData(0x07, false)]
    public void AtivoPeloValor_UsaParidadeDoPrimeiroByte(byte primeiro, bool esperado)
        => Assert.Equal(esperado, Inicializacao.AtivoPeloValor(new byte[] { primeiro, 0xFF, 0x00 }));

    [Fact]
    public void AtivoPeloValor_StringEstaAtiva() => Assert.True(Inicializacao.AtivoPeloValor("valor"));
}
