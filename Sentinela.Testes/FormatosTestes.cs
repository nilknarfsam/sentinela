using Sentinela.Servicos;

namespace Sentinela.Testes;

public class FormatosTestes
{
    [Theory]
    [InlineData(0, "0s")]
    [InlineData(45, "45s")]
    [InlineData(59, "59s")]
    [InlineData(60, "1min 00s")]
    [InlineData(125, "2min 05s")]
    [InlineData(3599, "59min 59s")]
    [InlineData(3600, "1h 00min")]
    [InlineData(7500, "2h 05min")]
    public void Duracao_FormataUnidades(int segundos, string esperado)
        => Assert.Equal(esperado, Formatos.Duracao(TimeSpan.FromSeconds(segundos)));

    [Fact]
    public void Quando_NuloMostraTraco() => Assert.Equal("—", Formatos.Quando(null));

    [Theory]
    [InlineData(0, "hoje às 14:05")]
    [InlineData(1, "ontem às 14:05")]
    [InlineData(3, "há 3 dias")]
    [InlineData(6, "há 6 dias")]
    public void Quando_FormataDataRecente(int dias, string esperado)
        => Assert.Equal(esperado, Formatos.Quando(DateTime.Today.AddDays(-dias).AddHours(14).AddMinutes(5)));

    [Theory]
    [InlineData(7)]
    [InlineData(30)]
    public void Quando_FormataDataAntiga(int dias)
    {
        var data = DateTime.Today.AddDays(-dias);
        Assert.Equal(data.ToString("dd/MM/yyyy"), Formatos.Quando(data));
    }
}
