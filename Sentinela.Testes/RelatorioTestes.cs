using Sentinela.Servicos;
using Sentinela.Servicos.Checkup;

namespace Sentinela.Testes;

public class RelatorioTestes
{
    private static readonly DateTime Inicio = new(2026, 11, 2);
    private static readonly DateTime Fim = Inicio.AddDays(7);
    private static StatusDefender Protegido => new(true, true, true, true, true, "Normal", "1", Inicio, Inicio, Inicio);
    private static RelatorioSemana Montar(params EntradaDiario[] entradas) => Relatorio.Montar(entradas, Inicio, Fim, Protegido, null, null);

    [Fact]
    public void ConciliacaoUsaNomeJanelaDeDoisMinutosEPeriodo()
    {
        EntradaDiario[] diario = [new(Inicio.AddHours(1), "ameaca", "Ameaça detectada: Virus:X · C:\\arquivo")];
        EventoDefender Evento(DateTime data, string nome, int id = 1116) => new(data, id, TipoEvento.Ameaca, "Ameaça detectada", nome + " · C:\\outro");
        var eventos = new[] {
            Evento(Inicio.AddHours(1).AddMinutes(-2), "virus:x"),
            Evento(Inicio.AddHours(1).AddMinutes(2), "Virus:X"),
            Evento(Inicio.AddHours(1).AddMinutes(2).AddTicks(1), "Virus:X"),
            Evento(Inicio.AddHours(1), "Virus:Y"),
            Evento(Inicio.AddTicks(-1), "Antes"), Evento(Fim, "Depois"),
            Evento(Inicio, "Falha de tratamento", 1118), Evento(Inicio, "Tratada", 1117) };
        var entradas = Relatorio.ConciliarAmeacas(diario, eventos, Inicio, Fim);
        Assert.Equal(3, entradas.Length);
        Assert.Contains("3 registro(s) de ameaça", Montar(entradas).Resumo);
        Assert.DoesNotContain(entradas, e => e.Mensagem.Contains("Antes") || e.Mensagem.Contains("Depois"));
    }

    [Theory]
    [InlineData("concluida", true)]
    [InlineData("cancelada", false)]
    [InlineData("falhou", false)]
    [InlineData("futuro", false)]
    public void ResultadoEstruturadoIndependeDeNomeOuMensagem(string resultado, bool concluida)
    {
        var e = new EntradaDiario(Inicio, "varredura", "Verificação de falha-cancelada.txt terminada", ResultadoVarredura: resultado, TipoVarreduraExecutada: TipoVarredura.Completa);
        var r = Montar(e);
        Assert.Equal(concluida ? 1 : 0, r.Varreduras);
        if (concluida) Assert.Contains("Completa: 1", Relatorio.Texto(r));
    }

    [Theory]
    [InlineData("Varredura completa terminada em 1 min. C:\\falhou\\cancelada.txt", true)]
    [InlineData("Varredura rápida cancelada. C:\\terminada.txt", false)]
    [InlineData("Varredura completa não rodou: motivo", false)]
    [InlineData("Verificação de falha.txt terminada em 1 min.", true)]
    [InlineData("Verificação de pasta cancelada.", false)]
    [InlineData("Verificação de pasta: não foi possível fazer a varredura: erro", false)]
    public void LegadoOlhaInicioDoResultado(string mensagem, bool concluida)
        => Assert.Equal(concluida, Relatorio.VarreduraConcluida(new(Inicio, "varredura", mensagem)));

    [Fact]
    public void InicioEErroNaoJustificamAvisoSemanal()
    {
        var r = Montar(new(Inicio, "inicio", "Iniciou"), new(Inicio, "erro", "Erro de rotina"));
        Assert.Equal(0, r.TotalEntradas);
        Assert.False(Relatorio.DeveAvisar(Fim.AddHours(9), null, true, r.TotalEntradas > 0));
        Assert.Contains("Erro de rotina", Relatorio.Texto(r));
    }

    [Theory]
    [InlineData("tratar", "As ameaças ativas foram tratadas pelo Defender.", "1 tratamento(s)")]
    [InlineData("restaurar", "Restaurado", "1 restauração(ões)")]
    [InlineData("tratar", "Não foi possível: acesso negado", "falha(s)")]
    [InlineData("restaurar", "Falha ao restaurar", "falha(s)")]
    public void ResumoIncluiTratamentoRestauracaoEFalha(string tipo, string mensagem, string esperado)
        => Assert.Contains(esperado, Montar(new EntradaDiario(Inicio, tipo, mensagem)).Resumo);

    [Fact]
    public void HtmlEscapaResumoNotasEDetalhesSemRecursosExternos()
    {
        const string malicioso = "<script>&\" http://x https://x <img src=http://x>";
        var resultado = new ResultadoCheckup(Inicio, 50,
            [new(malicioso, "", NivelVerificacao.Problema, malicioso, malicioso, null)], TimeSpan.Zero);
        var r = Relatorio.Montar([new(Inicio, "ameaca", malicioso)], Inicio, Fim, Protegido, resultado, resultado);
        r = r with { Resumo = malicioso };
        var html = Relatorio.GerarHtml(r);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src=", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;&amp;&quot;", html);
        Assert.Contains(malicioso, System.Net.WebUtility.HtmlDecode(html));
        Assert.Equal(5, html.Split("&lt;script&gt;").Length - 1); // resumo, ameaça, nome/resumo/detalhe de segurança
    }

    [Fact]
    public void SemanaVaziaComProtecaoConfirmada()
    {
        var r = Montar();
        Assert.Equal(0, r.TotalEntradas);
        Assert.Equal(0, r.Varreduras);
        Assert.Equal(0, r.BytesLiberados);
        Assert.Equal("Semana tranquila: nenhuma ameaça e proteção ligada no estado atual.", r.Resumo);
        Assert.Contains("ainda não verificado", Relatorio.Texto(r));
    }

    [Fact]
    public void SemanaComAmeacaTratamentoRestauracaoEAlerta()
    {
        var r = Montar(new(Inicio, "ameaca", "Vírus X"), new(Inicio.AddHours(1), "tratar", "Vírus X removido"),
            new(Inicio.AddHours(2), "restaurar", "Arquivo Y"), new(Inicio, "alerta", "Exclusão nova"));
        Assert.Contains("1 registro(s) de ameaça", r.Resumo);
        var texto = Relatorio.Texto(r);
        Assert.Contains("Vírus X removido", texto);
        Assert.Contains("Arquivo Y", texto);
        Assert.Contains("Exclusão nova", texto);
        Assert.Contains("02/11/2026", texto);
    }

    [Fact]
    public void SomaSomenteLimpezasPositivasNoIntervalo()
    {
        var r = Montar(new(Inicio.AddTicks(-1), "limpeza", "Antes", 1000), new(Inicio, "limpeza", "No início", 10),
            new(Fim.AddTicks(-1), "limpeza", "No fim", 20), new(Fim, "limpeza", "Fora", 500),
            new(Inicio, "varredura", "Completa", 500), new(Inicio, "limpeza", "Sem medição"), new(Inicio, "limpeza", "Inválida", -1));
        Assert.Equal(30, r.BytesLiberados);
        Assert.Contains(Limpeza.Formatar(30), Relatorio.Texto(r));
    }

    [Fact]
    public void ContabilizaTiposDesconhecidosSemPerderConteudo()
    {
        var r = Montar(new(Inicio, "novo", "Evento futuro"), new(Inicio, "outro", "Detalhe"));
        Assert.Equal(2, r.Secoes.Single(s => s.Titulo == "Outros eventos (2)").Linhas.Count);
        Assert.Contains("Evento futuro", Relatorio.Texto(r));
    }

    [Theory]
    [InlineData("2026-11-09T08:00:00", "2026-11-02", "2026-11-09")]
    [InlineData("2026-11-15T23:59:59", "2026-11-02", "2026-11-09")]
    [InlineData("2027-01-04T09:00:00", "2026-12-28", "2027-01-04")]
    public void SemanaPassadaDeSegundaADomingo(string agora, string inicio, string fim)
        => Assert.Equal((DateTime.Parse(inicio), DateTime.Parse(fim)), Relatorio.Periodo(DateTime.Parse(agora), true));

    [Fact]
    public void UltimosSeteDiasPreservaHora()
        => Assert.Equal((Fim.AddHours(14).AddDays(-7), Fim.AddHours(14)), Relatorio.Periodo(Fim.AddHours(14), false));

    [Theory]
    [InlineData("2026-11-09T08:59:59", null, true, true, false)]
    [InlineData("2026-11-09T09:00:00", null, true, true, true)]
    [InlineData("2026-11-15T23:59:00", "2026-11-02", true, true, true)]
    [InlineData("2026-11-11T10:00:00", "2026-11-09", true, true, false)]
    [InlineData("2026-11-11T10:00:00", null, false, true, false)]
    [InlineData("2026-11-11T10:00:00", null, true, false, false)]
    [InlineData("2026-11-16T09:00:00", "2026-11-09", true, true, true)]
    public void AvisoSemanal(string agora, string? ultimo, bool ligado, bool entradas, bool esperado)
        => Assert.Equal(esperado, Relatorio.DeveAvisar(DateTime.Parse(agora), ultimo == null ? null : DateTime.Parse(ultimo), ligado, entradas));

    [Fact]
    public void HtmlEscapaNomeECaminhoEIncluiImpressao()
    {
        var r = Montar(new EntradaDiario(Inicio, "ameaca", "<script>alert('x')&</script> C:\\<pasta>&arquivo"));
        var html = Relatorio.GerarHtml(r);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&lt;pasta&gt;&amp;arquivo", html);
        Assert.DoesNotContain("<script", html.ToLowerInvariant());
        Assert.DoesNotContain("http", html.ToLowerInvariant());
        Assert.Contains("@media print", html);
    }

    [Fact]
    public void NotasEItensDeSegurancaMantemDatasEDetalhes()
    {
        var resultado = new ResultadoCheckup(Inicio.AddDays(2), 60,
            [new("Firewall", "", NivelVerificacao.Atencao, "Verifique", "Detalhe do firewall", null)], TimeSpan.Zero);
        var texto = Relatorio.Texto(Relatorio.Montar([], Inicio, Fim, Protegido, resultado, resultado));
        Assert.Contains("Desempenho: 60/100 em 04/11/2026", texto);
        Assert.Contains("Segurança: 60/100", texto);
        Assert.Contains("Atenção: Firewall", texto);
        Assert.Contains("Detalhe do firewall", texto);
    }

    [Fact]
    public void FalhasECancelamentosNaoContamComoConcluidos()
    {
        var r = Montar(new(Inicio, "varredura", "Varredura completa terminada"), new(Inicio, "varredura", "Varredura rápida cancelada"),
            new(Inicio, "varredura", "Varredura completa: não foi possível fazer a varredura"), new(Inicio, "varredura", "Varredura falhou"),
            new(Inicio, "varredura", "Varredura completa não rodou: o Defender recusou o pedido. Tente abrir o Sentinela como administrador."));
        Assert.Equal(1, r.Varreduras);
        Assert.Contains("cancelada", Relatorio.Texto(r));
        Assert.Contains("Última completa no Defender", Relatorio.Texto(r));
    }

    [Fact]
    public void ResumoPriorizaProtecaoEAlertas()
    {
        Assert.Contains("desligada", Relatorio.Montar([], Inicio, Fim, Protegido with { TempoReal = false }, null, null).Resumo);
        Assert.Contains("Não consegui confirmar", Relatorio.Montar([], Inicio, Fim, null, null, null).Resumo);
        Assert.Contains("1 alerta(s)", Montar(new EntradaDiario(Inicio, "alerta", "Aviso")).Resumo);
    }

    [Fact]
    public void TodosOsTiposConhecidosTemSecao()
    {
        string[] tipos = ["varredura", "ameaca", "tratar", "restaurar", "inicializacao", "alerta", "limpeza", "checkup", "seguranca", "assinaturas", "status", "ajuste", "processo", "offline", "inicio", "erro"];
        var r = Montar(tipos.Select(t => new EntradaDiario(Inicio, t, "Registro de " + t)).ToArray());
        Assert.Empty(r.Secoes.Single(s => s.Titulo == "Outros eventos (0)").Linhas);
        foreach (var tipo in tipos) Assert.Contains("Registro de " + tipo, Relatorio.Texto(r));
    }
}
