using Sentinela.Paginas;
using Sentinela.Servicos;
using Sentinela.Servicos.Checkup;

namespace Sentinela.Testes;

public sealed class IntegracaoDesempenhoTestes : IDisposable
{
    private readonly string _arquivo = Path.Combine(Path.GetTempPath(), "Sentinela-checkup-" + Guid.NewGuid() + ".json");
    private static readonly DateTime Agora = new(2026, 11, 10, 15, 0, 0);
    private static ResultadoCheckup Resultado(DateTime data, int nota = 86) => new(data, nota,
        [new("Espaço livre", "icone", NivelVerificacao.Atencao, "Pouco espaço", "Detalhe privado", new("Resolver", "pagina", "limpeza"))], TimeSpan.FromSeconds(2));

    [Fact]
    public void PersistenciaGravaELeResumoSemAcao()
    {
        var original = Resultado(Agora);
        Assert.True(Checkup.GravarSalvo(_arquivo, original));
        var salvo = Assert.IsType<ResultadoCheckup>(Checkup.LerSalvo(_arquivo));
        Assert.Equal(original.Data, salvo.Data);
        Assert.Equal(original.Nota, salvo.Nota);
        var item = Assert.Single(salvo.Itens);
        Assert.Equal(original.Itens[0].Nome, item.Nome);
        Assert.Equal(original.Itens[0].Nivel, item.Nivel);
        Assert.Equal(original.Itens[0].Resumo, item.Resumo);
        Assert.Null(item.Acao);
        Assert.DoesNotContain("Detalhe privado", File.ReadAllText(_arquivo));
    }

    [Fact]
    public void ArquivoMaiorQue64KbIgnorado()
    {
        Assert.True(Checkup.GravarSalvo(_arquivo, Resultado(Agora)));
        File.AppendAllText(_arquivo, new string(' ', 64 * 1024));
        Assert.Null(Checkup.LerSalvo(_arquivo));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"Data\":\"2026-11-10\",\"Nota\":101,\"Itens\":[]}")]
    [InlineData("{\"Data\":\"2026-11-10\",\"Nota\":86,\"Itens\":[null]}")]
    public void JsonInvalidoIgnorado(string texto)
    {
        File.WriteAllText(_arquivo, texto);
        Assert.Null(Checkup.LerSalvo(_arquivo));
    }

    [Fact]
    public void GravacaoAcimaDoLimitePreservaAnterior()
    {
        Assert.True(Checkup.GravarSalvo(_arquivo, Resultado(Agora)));
        var grande = Resultado(Agora) with { Itens = [new("Teste", "", NivelVerificacao.Ok, new string('á', 64 * 1024), "", null)] };
        Assert.False(Checkup.GravarSalvo(_arquivo, grande));
        Assert.NotNull(Checkup.LerSalvo(_arquivo));
    }

    [Theory]
    [InlineData(true, 7, -1, true)]
    [InlineData(true, 7, 0, false)]
    [InlineData(false, 7, -1, false)]
    [InlineData(true, 6, -1, false)]
    [InlineData(true, 7, null, true)]
    [InlineData(true, 7, 1, false)]
    public void RegraDoLembrete(bool pendente, int dias, int? ultimoDias, bool esperado)
        => Assert.Equal(esperado, LembreteReinicio.DeveLembrar(pendente, TimeSpan.FromDays(dias),
            ultimoDias.HasValue ? Agora.AddDays(ultimoDias.Value) : null, Agora));

    [Theory]
    [InlineData("checkup", true, false)]
    [InlineData("processo", true, false)]
    [InlineData("inicializacao", true, false)]
    [InlineData("limpeza", true, true)]
    [InlineData("varredura", false, false)]
    [InlineData("alerta", false, false)]
    [InlineData("Ameaca", false, false)]
    [InlineData("ajuste", false, false)]
    public void FiltrosSelecionamTiposCertos(string tipo, bool desempenho, bool limpeza)
    {
        Assert.Equal(desempenho, PaginaHistorico.SelecionaFiltro("desempenho", tipo));
        Assert.Equal(limpeza, PaginaHistorico.SelecionaFiltro("limpeza", tipo));
    }

    [Fact]
    public void CartaoSemCheckup()
        => Assert.Equal(("Ainda não verificado", "TextoFraco", false), PaginaInicio.TextoCheckup(null, Agora));

    [Fact]
    public void CartaoRecente()
        => Assert.Equal(("86 de 100 · verificado ontem às 14:10", "Verde", false),
            PaginaInicio.TextoCheckup(Resultado(Agora.Date.AddDays(-1).AddHours(14).AddMinutes(10)), Agora));

    [Theory]
    [InlineData(7, false)]
    [InlineData(7.01, true)]
    [InlineData(8, true)]
    public void CartaoAntigo(double dias, bool antigo)
        => Assert.Equal(antigo, PaginaInicio.TextoCheckup(Resultado(Agora.AddDays(-dias)), Agora).Antigo);

    [Theory]
    [InlineData(85, "Verde")]
    [InlineData(84, "Ambar")]
    [InlineData(60, "Ambar")]
    [InlineData(59, "Vermelho")]
    public void CorPelaFaixa(int nota, string cor)
        => Assert.Equal(cor, PaginaInicio.TextoCheckup(Resultado(Agora, nota), Agora).Cor);

    public void Dispose() { if (File.Exists(_arquivo)) File.Delete(_arquivo); }
}
