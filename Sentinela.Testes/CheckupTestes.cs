using Sentinela.Paginas;
using Sentinela.Servicos;
using Sentinela.Servicos.Checkup;

namespace Sentinela.Testes;

[Collection("Diário")]
public sealed class CheckupTestes : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "Sentinela-Checkup-" + Guid.NewGuid());
    private readonly bool _desligado = Diario.Desligado;
    public CheckupTestes()
    {
        Diario.PastaTeste = _pasta;
        Diario.Desligado = false;
    }
    public void Dispose()
    {
        Diario.PastaTeste = null;
        Diario.Desligado = _desligado;
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, true);
    }

    [Fact]
    public void NotaVazia() => Assert.Equal(100, Checkup.CalcularNota([]));
    [Fact]
    public void NotaTodosOk() => Assert.Equal(100, Checkup.CalcularNota([(NivelVerificacao.Ok, 3), (NivelVerificacao.Ok, 1)]));
    [Fact]
    public void NotaPonderada() => Assert.Equal(56, Checkup.CalcularNota([(NivelVerificacao.Ok, 3), (NivelVerificacao.Atencao, 2), (NivelVerificacao.Problema, 4)]));
    [Fact]
    public void NotaArredondada() => Assert.Equal(63, Checkup.CalcularNota([(NivelVerificacao.Ok, 1), (NivelVerificacao.Atencao, 15)]));

    [Theory]
    [InlineData(true, 1, NivelVerificacao.Problema)]
    [InlineData(true, 8, NivelVerificacao.Problema)]
    [InlineData(false, 7, NivelVerificacao.Atencao)]
    [InlineData(false, 6.99, NivelVerificacao.Ok)]
    public void Reinicio(bool pendente, double dias, NivelVerificacao nivel)
    {
        var r = VerificacaoReinicio.Avaliar(pendente, TimeSpan.FromDays(dias));
        Assert.Equal(nivel, r.Nivel);
        Assert.Equal(pendente ? "ms-settings:windowsupdate" : null, r.Acao?.Alvo);
    }

    [Theory]
    [InlineData(1000, 99, NivelVerificacao.Problema)]
    [InlineData(1000, 100, NivelVerificacao.Atencao)]
    [InlineData(30, 9.99, NivelVerificacao.Problema)]
    [InlineData(30, 10, NivelVerificacao.Ok)]
    [InlineData(1000, 199.99, NivelVerificacao.Atencao)]
    [InlineData(1000, 200, NivelVerificacao.Ok)]
    public void EspacoLimites(double total, double livre, NivelVerificacao nivel)
        => Assert.Equal(nivel, VerificacaoEspaco.Avaliar([new("C:", "Disco", total, livre)]).Nivel);

    [Fact]
    public void EspacoCitaPiorDiscoEListaTodos()
    {
        var r = VerificacaoEspaco.Avaliar([new("D:", "Dados", 1000, 180), new("C:", "Sistema", 900, 45), new("E:", "Extra", 100, 90)]);
        Assert.Equal(NivelVerificacao.Problema, r.Nivel);
        Assert.Equal("C: 45 GB livres (5%)", r.Resumo);
        Assert.Contains("D:", r.Detalhe);
        Assert.Contains("C:", r.Detalhe);
        Assert.Contains("E:", r.Detalhe);
        Assert.Equal("limpeza", r.Acao?.Alvo);
    }
    [Fact]
    public void EspacoSemDiscosNaoAfirmaQueEstaOk() => Assert.Equal(NivelVerificacao.Atencao, VerificacaoEspaco.Avaliar([]).Nivel);

    [Fact]
    public void DriversVazio()
    {
        var r = VerificacaoDrivers.Avaliar([]);
        Assert.Equal(NivelVerificacao.Ok, r.Nivel);
        Assert.Equal("Todos os dispositivos funcionando", r.Resumo);
        Assert.Equal("O Sentinela não instala drivers sozinho: use o Windows Update e o app da NVIDIA.", r.Detalhe);
    }
    [Fact]
    public void DriversSoDesativados() => Assert.Equal(NivelVerificacao.Atencao, VerificacaoDrivers.Avaliar([new("Áudio", 22), new("Rede", 22)]).Nivel);
    [Fact]
    public void DriversDesconectadosIgnorados() => Assert.Equal(NivelVerificacao.Ok, VerificacaoDrivers.Avaliar([new("USB", 45)]).Nivel);
    [Fact]
    public void DriversComProblema()
    {
        var r = VerificacaoDrivers.Avaliar([new("Rede", 10), new("Áudio", 22), new("USB", 45)]);
        Assert.Equal(NivelVerificacao.Problema, r.Nivel);
        Assert.Equal("2 dispositivo(s) com problema", r.Resumo);
        Assert.Contains("Rede (código 10)", r.Detalhe);
        Assert.DoesNotContain("USB", r.Detalhe);
    }

    [Theory]
    [InlineData("381b4222-f694-41f0-9685-ff5bb260df2e", "Equilibrado", NivelVerificacao.Ok)]
    [InlineData("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", "Alto desempenho", NivelVerificacao.Ok)]
    [InlineData("a1841308-3542-4c4b-8bd3-cdc28aa24d6d", "Plano Economia de energia deixa o PC mais lento", NivelVerificacao.Atencao)]
    [InlineData("e9a42b02-d5df-448d-aa00-03f14749eb61", "Desempenho máximo", NivelVerificacao.Ok)]
    [InlineData("11111111-2222-3333-4444-555555555555", "Plano personalizado", NivelVerificacao.Ok)]
    public void EnergiaPorGuid(string guid, string resumo, NivelVerificacao nivel)
    {
        var r = VerificacaoEnergia.Avaliar($"GUID do Esquema de Energia: {guid} (texto que não determina o plano)");
        Assert.Equal(nivel, r.Nivel);
        Assert.Equal(resumo, r.Resumo);
        Assert.Equal(nivel == NivelVerificacao.Atencao ? "powercfg.cpl" : null, r.Acao?.Alvo);
    }
    [Fact]
    public void EnergiaSemGuid() => Assert.Equal(NivelVerificacao.Atencao, VerificacaoEnergia.Avaliar("Alto desempenho").Nivel);
    [Fact]
    public void EnergiaEmIngles() => Assert.Equal("Alto desempenho", VerificacaoEnergia.Avaliar("Power Scheme GUID: 8C5E7FDA-E8BF-4A96-9A85-A6E23A8C635C (High performance)").Resumo);

    [Theory]
    [InlineData(8, NivelVerificacao.Ok)]
    [InlineData(9, NivelVerificacao.Atencao)]
    [InlineData(15, NivelVerificacao.Atencao)]
    [InlineData(16, NivelVerificacao.Problema)]
    public void InicializacaoLimites(int ativos, NivelVerificacao nivel)
    {
        var r = VerificacaoInicializacao.Avaliar(ativos);
        Assert.Equal(nivel, r.Nivel);
        Assert.Equal($"{ativos} programas abrem com o Windows", r.Resumo);
    }

    [Fact]
    public async Task ExecucaoIsolaTimeoutExcecaoECalculaNota()
    {
        var resultado = await Checkup.Executar([
            new Falsa("Lenta", 3, async ct =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return VerificacaoInicializacao.Avaliar(0);
            }),
            new Falsa("Falha", 2, _ => throw new InvalidOperationException("Falha de leitura")),
            new Falsa("Boa", 1, _ => Task.FromResult(VerificacaoInicializacao.Avaliar(8)))], TimeSpan.FromSeconds(1));
        Assert.Equal(3, resultado.Itens.Count);
        Assert.All(resultado.Itens.Take(2), r =>
        {
            Assert.Equal(NivelVerificacao.Atencao, r.Nivel);
            Assert.Equal("Não consegui verificar agora", r.Resumo);
        });
        Assert.Equal(NivelVerificacao.Ok, resultado.Itens[2].Nivel);
        Assert.Equal(67, resultado.Nota);
        Assert.Equal("Checkup de desempenho: nota 67", Assert.Single(Diario.Ler(1)).Mensagem);
        Assert.True(resultado.Duracao < TimeSpan.FromSeconds(4));
    }

    [Fact]
    public async Task ExecucaoCanceladaNaoLanca()
    {
        using var ct = new CancellationTokenSource();
        ct.Cancel();
        var r = await Checkup.Executar([new Falsa("Cancelada", 1, t => Task.FromCanceled<ResultadoVerificacao>(t))], ct: ct.Token);
        Assert.Equal(NivelVerificacao.Atencao, Assert.Single(r.Itens).Nivel);
    }

    [Theory]
    [InlineData("ms-settings:windowsupdate", true)]
    [InlineData("devmgmt.msc", true)]
    [InlineData("powercfg.cpl", true)]
    [InlineData("cmd.exe", false)]
    [InlineData("powercfg.cpl /qualquer", false)]
    [InlineData(null, false)]
    public void AberturaSomenteAlvosFixos(string? alvo, bool permitido) => Assert.Equal(permitido, PaginaDesempenho.AlvoPermitido(alvo));

    private sealed class Falsa(string nome, int peso, Func<CancellationToken, Task<ResultadoVerificacao>> verificar) : IVerificacao
    {
        public string Nome => nome;
        public int Peso => peso;
        public Task<ResultadoVerificacao> Verificar(CancellationToken ct) => verificar(ct);
    }
}
