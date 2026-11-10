using Sentinela.Servicos;
using Sentinela.Servicos.Checkup;

namespace Sentinela.Testes;

public sealed class ProcessosTestes
{
    private const string Programa = @"C:\Program Files\Programa\app.exe";
    private static InfoProcesso Info(double cpu = 0, long memoria = 100, bool protegido = false, string nome = "chrome", int pid = 99) =>
        new(pid, nome, 123, Programa, "Editora", cpu, memoria, protegido, protegido ? "processo do Windows" : null);

    [Theory]
    [InlineData("app", 123, "app", 123, true)]
    [InlineData("app", 123, "app", 124, false)]
    [InlineData("app", 0, "app", 123, false)]
    [InlineData("app", 123, "app", 0, false)]
    [InlineData("app", 123, "outro", 123, false)]
    public void ReconfereIdentidade(string esperado, long inicioEsperado, string atual, long inicioAtual, bool ok) =>
        Assert.Equal(ok, Processos.Reconferir(esperado, inicioEsperado, atual, inicioAtual));

    [Theory]
    [InlineData(null, 10.0, 0)]
    [InlineData(10.0, null, 0)]
    [InlineData(null, null, 0)]
    [InlineData(10.0, 12.0, 25)]
    public void CpuComLeituraIndisponivel(double? anterior, double? atual, double esperado) =>
        Assert.Equal(esperado, Processos.CalcularCpu(anterior.HasValue ? TimeSpan.FromSeconds(anterior.Value) : null,
            atual.HasValue ? TimeSpan.FromSeconds(atual.Value) : null, TimeSpan.FromSeconds(2), 4));

    [Fact]
    public void HistoricoSeparaSegmentosAposPausa()
    {
        var historico = new HistoricoDesempenho();
        var inicio = DateTime.Now;
        foreach (var segundos in new[] { 0, 5, 20, 36, 41 }) historico.Adicionar(inicio.AddSeconds(segundos), 10, 20);
        Assert.Equal(new[] { true, false, false, true, false }, historico.Pontos.Select(p => p.IniciaSegmento));
    }

    [Fact]
    public void GrupoEmFinalizacaoAtualizaBinding()
    {
        var grupo = Assert.Single(Processos.Agrupar([Info()]));
        string? propriedade = null;
        grupo.PropertyChanged += (_, e) => propriedade = e.PropertyName;
        grupo.Finalizando = true;
        Assert.False(grupo.PodeFinalizar);
        Assert.Equal(nameof(grupo.PodeFinalizar), propriedade);
        grupo.Finalizando = false;
        Assert.True(grupo.PodeFinalizar);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(42)]
    public void PidsProtegidos(int pid) => Assert.True(Processos.Classificar(pid, "app", Programa, 42, @"C:\Windows").protegido);

    public static IEnumerable<object[]> NomesCriticos => new[]
    {
        "csrss", "wininit", "winlogon", "services", "lsass", "smss", "svchost", "dwm", "explorer", "fontdrvhost",
        "MsMpEng", "NisSrv", "SecurityHealthService", "Registry", "Memory Compression", "System", "Idle"
    }.SelectMany(n => new[] { new object[] { n }, new object[] { n.ToUpperInvariant() + ".EXE" } });

    [Theory]
    [MemberData(nameof(NomesCriticos))]
    public void NomeCriticoSempreProtegido(string nome) => Assert.True(Processos.Classificar(99, nome, Programa, 42, @"C:\Windows").protegido);

    [Theory]
    [InlineData(@"C:\Windows\System32\app.exe")]
    [InlineData(@"c:\WINDOWS\SysWOW64\app.exe")]
    [InlineData(@"C:\Windows\WinSxS\pasta\app.exe")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relativo.exe")]
    public void CaminhosProtegidos(string? caminho) => Assert.True(Processos.Classificar(99, "app", caminho, 42, @"C:\Windows").protegido);

    [Theory]
    [InlineData(Programa)]
    [InlineData(@"C:\WindowsOutro\app.exe")]
    public void ProgramaComumNaoProtegido(string caminho)
    {
        var resultado = Processos.Classificar(99, "app", caminho, 42, @"C:\Windows");
        Assert.False(resultado.protegido);
        Assert.Null(resultado.motivo);
    }

    [Theory]
    [InlineData(2, 2, 4, 25)]
    [InlineData(8, 2, 4, 100)]
    [InlineData(100, 2, 4, 100)]
    [InlineData(-1, 2, 4, 0)]
    [InlineData(1, 0, 4, 0)]
    [InlineData(1, -1, 4, 0)]
    [InlineData(1, 2, 0, 0)]
    [InlineData(1, 2, -1, 0)]
    public void CpuNormalizada(double delta, double intervalo, int nucleos, double esperado) =>
        Assert.Equal(esperado, Processos.CalcularCpu(TimeSpan.FromSeconds(delta), TimeSpan.FromSeconds(intervalo), nucleos));

    [Fact]
    public void AgrupaNomeIgnorandoCaixaEExtensaoMantendoPids()
    {
        var grupos = Processos.Agrupar([Info(20, 100, pid: 10), Info(25, 200, nome: "CHROME.exe", pid: 11), Info(nome: "outro")]);
        var chrome = Assert.Single(grupos, g => g.Nome.StartsWith("Google Chrome"));
        Assert.Equal("Google Chrome (2 processos)", chrome.Nome);
        Assert.Equal(45, chrome.CpuPercent);
        Assert.Equal(300, chrome.MemoriaBytes);
        Assert.Equal(new[] { 10, 11 }, chrome.Processos.Select(p => p.Pid));
        Assert.True(chrome.PodeFinalizar);
        Assert.Equal("Editora", chrome.Editora);
    }

    [Fact]
    public void GrupoComMembroProtegidoNaoPodeFinalizar()
    {
        var grupo = Assert.Single(Processos.Agrupar([Info(), Info(protegido: true)]));
        Assert.True(grupo.Protegido);
        Assert.False(grupo.PodeFinalizar);
        Assert.Contains("Protegido", grupo.Dica);
    }

    [Theory]
    [InlineData(19.9, 79, NivelVerificacao.Ok)]
    [InlineData(20, 79, NivelVerificacao.Atencao)]
    [InlineData(39.9, 79, NivelVerificacao.Atencao)]
    [InlineData(40, 79, NivelVerificacao.Problema)]
    [InlineData(0, 80, NivelVerificacao.Atencao)]
    [InlineData(0, 89, NivelVerificacao.Atencao)]
    [InlineData(0, 90, NivelVerificacao.Problema)]
    public void LimitesPeso(double cpu, double memoria, NivelVerificacao esperado) =>
        Assert.Equal(esperado, VerificacaoPeso.NivelPeso(100, memoria, Info(cpu)));

    [Theory]
    [InlineData(79, NivelVerificacao.Ok)]
    [InlineData(80, NivelVerificacao.Atencao)]
    [InlineData(90, NivelVerificacao.Problema)]
    public void CpuProtegidaNaoConta(double memoria, NivelVerificacao esperado) =>
        Assert.Equal(esperado, VerificacaoPeso.NivelPeso(100, memoria, Info(100, protegido: true)));

    [Fact]
    public void SemProgramaUsaApenasMemoria() => Assert.Equal(NivelVerificacao.Atencao, VerificacaoPeso.NivelPeso(100, 80, null));

    [Fact]
    public void HistoricoMantem720Pontos()
    {
        var historico = new HistoricoDesempenho();
        var inicio = new DateTime(2026, 11, 10);
        for (int i = 0; i < 1000; i++) historico.Adicionar(inicio.AddSeconds(i * 5), i % 100, 50);
        Assert.Equal(720, historico.Pontos.Count);
        Assert.Equal(inicio.AddSeconds(280 * 5), historico.Pontos[0].Instante);
        Assert.Equal(inicio.AddSeconds(999 * 5), historico.Pontos[^1].Instante);
    }

    [Fact]
    public void HistoricoRespeitaIntervaloELimites()
    {
        var historico = new HistoricoDesempenho();
        var inicio = DateTime.Now;
        historico.Adicionar(inicio, -1, 101);
        historico.Adicionar(inicio.AddSeconds(4), 50, 50);
        var ponto = Assert.Single(historico.Pontos);
        Assert.Equal(0, ponto.CpuPercent);
        Assert.Equal(100, ponto.MemoriaPercent);
        historico.Adicionar(inicio.AddHours(2), 10, 20);
        Assert.Equal(inicio.AddHours(2), Assert.Single(historico.Pontos).Instante);
    }

    [Fact]
    public async Task AmostraCanceladaNaoLanca()
    {
        using var ct = new CancellationTokenSource();
        ct.Cancel();
        Assert.Empty(await Processos.Amostrar(TimeSpan.FromSeconds(2), ct.Token));
    }

    [Fact]
    public void PastasInvalidasPermanecemConfiguradas()
    {
        var configuracoes = new Configuracoes { PastasProjetos = [@"Z:\Sentinela-pasta-inexistente"] };
        Parallel.For(0, 20, _ => Assert.Empty(configuracoes.PastasProjetosValidas()));
        Assert.Single(configuracoes.PastasProjetos);
        Assert.Single(configuracoes.ListarPastasProjetos());
    }
}
