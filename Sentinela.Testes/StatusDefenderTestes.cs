using Sentinela.Servicos;

namespace Sentinela.Testes;

public class StatusDefenderTestes
{
    [Theory]
    [InlineData(true, true, true, true, true)]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, true, false, false)]
    public void TudoLigado_ExigeAsQuatroProtecoes(bool antivirus, bool tempoReal, bool comportamento, bool downloads, bool esperado)
    {
        var status = new StatusDefender(antivirus, tempoReal, comportamento, downloads, false, "Normal", "1", null, null, null);
        Assert.Equal(esperado, status.TudoLigado);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(0, false)]
    public void AssinaturasVelhas_VerificaIdade(int dias, bool esperado)
    {
        var status = new StatusDefender(true, true, true, true, true, "Normal", "1", DateTime.Now.AddDays(-dias), null, null);
        Assert.Equal(esperado, status.AssinaturasVelhas);
    }

    [Fact]
    public void AssinaturasVelhas_SemDataNaoMarcaComoVelhas()
    {
        var status = new StatusDefender(true, true, true, true, true, "Normal", "1", null, null, null);
        Assert.False(status.AssinaturasVelhas);
    }
}
