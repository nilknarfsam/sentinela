using Sentinela.Servicos;
using Sentinela.Servicos.Checkup;

namespace Sentinela.Testes;

public sealed class SaudeDiscoTestes
{
    private static DiscoFisico Disco => new("Disco teste", "SSD NVMe", 1024L * 1024 * 1024, "Saudável", "Funcionando", null, null, null, null, null, null, false);

    [Theory]
    [InlineData("Com defeito", "Disco com defeito: faça backup já")]
    [InlineData("Com aviso", "O disco avisou problema: faça backup")]
    public void EstadoTemPrioridade(string saude, string motivo)
    {
        var r = SaudeDisco.Avaliar(Disco with { Saude = saude, DesgastePercent = 90, Temperatura = 70 });
        Assert.Equal(NivelVerificacao.Problema, r.nivel);
        Assert.Equal(motivo, r.motivo);
    }

    [Theory]
    [InlineData(69, NivelVerificacao.Ok)]
    [InlineData(70, NivelVerificacao.Atencao)]
    [InlineData(89, NivelVerificacao.Atencao)]
    [InlineData(90, NivelVerificacao.Problema)]
    public void Desgaste(int desgaste, NivelVerificacao nivel) =>
        Assert.Equal(nivel, SaudeDisco.Avaliar(Disco with { DesgastePercent = desgaste, DadosAvancados = true }).nivel);

    [Theory]
    [InlineData("SSD NVMe", 59, NivelVerificacao.Ok)]
    [InlineData("SSD NVMe", 60, NivelVerificacao.Atencao)]
    [InlineData("SSD NVMe", 69, NivelVerificacao.Atencao)]
    [InlineData("SSD NVMe", 70, NivelVerificacao.Problema)]
    [InlineData("SSD SATA", 59, NivelVerificacao.Ok)]
    [InlineData("SSD SATA", 60, NivelVerificacao.Atencao)]
    [InlineData("SSD SATA", 69, NivelVerificacao.Atencao)]
    [InlineData("SSD SATA", 70, NivelVerificacao.Problema)]
    [InlineData("HD", 54, NivelVerificacao.Ok)]
    [InlineData("HD", 55, NivelVerificacao.Atencao)]
    [InlineData("HD", 70, NivelVerificacao.Atencao)]
    public void Temperatura(string tipo, int temperatura, NivelVerificacao nivel) =>
        Assert.Equal(nivel, SaudeDisco.Avaliar(Disco with { Tipo = tipo, Temperatura = temperatura, DadosAvancados = true }).nivel);

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    public void Erros(long leitura, long escrita) => Assert.Equal(NivelVerificacao.Atencao,
        SaudeDisco.Avaliar(Disco with { ErrosLeitura = leitura, ErrosEscrita = escrita, DadosAvancados = true }).nivel);

    [Fact]
    public void SemDadosNaoPiora() => Assert.Equal(NivelVerificacao.Ok, SaudeDisco.Avaliar(Disco).nivel);

    [Fact]
    public void TemperaturaCriticaSuperaDesgasteDeAtencao() => Assert.Equal(NivelVerificacao.Problema,
        SaudeDisco.Avaliar(Disco with { DesgastePercent = 70, Temperatura = 70 }).nivel);

    [Theory]
    [InlineData(3, 11, "HD")]
    [InlineData(3, 7, "HD")]
    [InlineData(4, 17, "SSD NVMe")]
    [InlineData(4, 11, "SSD SATA")]
    [InlineData(4, 7, "SSD USB")]
    [InlineData(4, 0, "SSD")]
    [InlineData(0, 17, "Desconhecido")]
    public void Tipo(int media, int bus, string esperado) => Assert.Equal(esperado, SaudeDisco.Tipo(media, bus));

    [Theory]
    [InlineData(0, "Saudável")]
    [InlineData(1, "Com aviso")]
    [InlineData(2, "Com defeito")]
    [InlineData(5, "Desconhecida")]
    public void Saude(int estado, string esperado) => Assert.Equal(esperado, SaudeDisco.Saude(estado));

    [Fact]
    public void EstadoOperacional() => Assert.Equal("Funcionando, Degradado", SaudeDisco.Operacional([2, 3]));

    [Fact]
    public void ConsultaEscapaChave() => Assert.Equal(
        "ASSOCIATORS OF {MSFT_PhysicalDisk.ObjectId='a\\\\b\\'c'} WHERE AssocClass = MSFT_PhysicalDiskToStorageReliabilityCounter",
        SaudeDisco.ConsultaContadores("a\\b'c"));

    [Fact]
    public void PiorDiscoDefineNivel()
    {
        var r = VerificacaoSaudeDiscos.Avaliar([Disco, Disco with { Nome = "Com desgaste", DesgastePercent = 70 },
            Disco with { Nome = "Com defeito", Saude = "Com defeito" }], false);
        Assert.Equal(NivelVerificacao.Problema, r.Nivel);
        Assert.Equal("Com defeito: Disco com defeito: faça backup já", r.Resumo);
        Assert.Contains("Disco teste", r.Detalhe);
        Assert.Contains("Com desgaste", r.Detalhe);
        Assert.Contains("desgaste 70%", r.Detalhe);
        Assert.Contains("Abra como administrador para ver desgaste e temperatura.", r.Detalhe);
        Assert.Equal("discos", r.Acao?.Alvo);
    }

    [Fact]
    public void TodosSaudaveis() => Assert.Equal("Todos os 3 discos saudáveis", VerificacaoSaudeDiscos.Avaliar([Disco, Disco, Disco], true).Resumo);

    [Fact]
    public void SemDiscosIndisponivel() => Assert.Equal(NivelVerificacao.Atencao, VerificacaoSaudeDiscos.Avaliar([], false).Nivel);

    [Fact]
    public void UmDiscoSaudavel() => Assert.Equal("Disco saudável", VerificacaoSaudeDiscos.Avaliar([Disco], true).Resumo);

    [Fact]
    public void TodosDesconhecidos()
    {
        var r = VerificacaoSaudeDiscos.Avaliar([Disco with { Saude = "Desconhecida" }, Disco with { Saude = "Desconhecida" }], true);
        Assert.Equal(NivelVerificacao.Atencao, r.Nivel);
        Assert.Equal("Não consegui ler a saúde dos discos", r.Resumo);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    [InlineData(101, null)]
    [InlineData(255, null)]
    [InlineData(null, null)]
    [InlineData("inválido", null)]
    public void ValidaDesgaste(object? valor, int? esperado) => Assert.Equal(esperado, SaudeDisco.Desgaste(valor));

    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    [InlineData(35, 35)]
    [InlineData(null, null)]
    [InlineData("inválido", null)]
    public void ValidaTemperaturas(object? valor, int? esperado) => Assert.Equal(esperado, SaudeDisco.Temperatura(valor));

    [Fact]
    public void OperacionalApareceSoNoDetalheQuandoNaoFunciona()
    {
        var disco = Disco with { Operacional = "Degradado" };
        Assert.DoesNotContain("Degradado", SaudeDisco.EstadoTexto(disco));
        Assert.Contains("(Degradado)", SaudeDisco.Descrever(disco));
        Assert.Contains("Funcionando", SaudeDisco.EstadoTexto(Disco));
    }

    [Fact]
    public void IntegradaComPesoTres() => Assert.Equal(3, Assert.Single(Checkup.Padrao.OfType<VerificacaoSaudeDiscos>()).Peso);

    [Fact]
    public async Task LeituraCanceladaNaoLanca()
    {
        using var ct = new CancellationTokenSource();
        ct.Cancel();
        Assert.Empty(await SaudeDisco.Ler(ct.Token));
    }
}
