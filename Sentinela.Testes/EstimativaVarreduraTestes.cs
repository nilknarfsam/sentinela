using Sentinela.Paginas;
using Sentinela.Servicos;

namespace Sentinela.Testes;

public class EstimativaVarreduraTestes
{
    private static EventoDefender Evento(int minutos, string tipo = "Rápida", int idade = 0)
        => new(new DateTime(2026, 11, 9).AddDays(-idade), 1001, TipoEvento.Varredura,
            "Varredura terminada", tipo, TimeSpan.FromMinutes(minutos));

    [Fact]
    public void SemEventos_NaoEstima()
        => Assert.Null(PaginaVarredura.EstimarDuracao([], TipoVarredura.Rapida));

    [Fact]
    public void TresRapidas_UsaMedia()
        => Assert.Equal(TimeSpan.FromMinutes(4), PaginaVarredura.EstimarDuracao(
            [Evento(2), Evento(4, "Quick"), Evento(6)], TipoVarredura.Rapida));

    [Theory]
    [InlineData(TipoVarredura.Rapida, 2)]
    [InlineData(TipoVarredura.Completa, 50)]
    public void Mistura_UsaSomenteTipoPedido(TipoVarredura tipo, int minutos)
        => Assert.Equal(TimeSpan.FromMinutes(minutos), PaginaVarredura.EstimarDuracao(
            [Evento(2), Evento(40, "Completa"), Evento(60, "Full")], tipo));

    [Fact]
    public void UsaSomenteTresMaisRecentes()
        => Assert.Equal(TimeSpan.FromMinutes(4), PaginaVarredura.EstimarDuracao(
            [Evento(100, idade: 10), Evento(6, idade: 2), Evento(2), Evento(4, idade: 1)], TipoVarredura.Rapida));

    [Fact]
    public void Personalizada_NaoEstima()
        => Assert.Null(PaginaVarredura.EstimarDuracao([Evento(2)], TipoVarredura.Personalizada));

    [Fact]
    public void IgnoraEventosSemDuracaoOuSemConclusao()
    {
        var evento = Evento(2);
        Assert.Null(PaginaVarredura.EstimarDuracao(
            [evento with { Duracao = null }, evento with { Duracao = TimeSpan.Zero },
                evento with { Id = 1002 }, evento with { Titulo = "Varredura cancelada" }], TipoVarredura.Rapida));
    }
}
