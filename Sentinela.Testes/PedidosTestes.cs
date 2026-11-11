using System.Text.Json;
using Sentinela.Servicos;

namespace Sentinela.Testes;

[CollectionDefinition("Pedidos", DisableParallelization = true)]
public class ColecaoPedidos;

[Collection("Pedidos")]
public class PedidosTestes : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "Sentinela-Pedidos-" + Guid.NewGuid());
    private static readonly DateTime Agora = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    public PedidosTestes()
    {
        Directory.CreateDirectory(_pasta);
        Pedidos.PastaTeste = _pasta;
    }

    [Theory]
    [InlineData("mostrar", null)]
    [InlineData("verificar", @"C:\Minha pasta\arquivo.txt")]
    public void Interpretar_AceitaPedidoValido(string acao, string? caminho)
    {
        var pedido = new Pedido(acao, caminho);
        Assert.Equal(pedido, Pedidos.Interpretar(JsonSerializer.Serialize(pedido), Agora, Agora));
    }

    [Theory]
    [InlineData("nao e json")]
    [InlineData("null")]
    [InlineData("{}")]
    public void Interpretar_RejeitaJsonInvalido(string json)
        => Assert.Null(Pedidos.Interpretar(json, Agora, Agora));

    [Theory]
    [InlineData("desconhecida", null)]
    [InlineData("verificar", "relativo.txt")]
    [InlineData("verificar", "")]
    [InlineData("verificar", null)]
    [InlineData("verificar", "C:\\pasta\\ar\"quivo")]
    [InlineData("verificar", "C:\\pasta\\*.exe")]
    [InlineData("verificar", "C:arquivo.txt")]
    public void Interpretar_RejeitaPedidoInvalido(string acao, string? caminho)
        => Assert.Null(Pedidos.Interpretar(JsonSerializer.Serialize(new Pedido(acao, caminho)), Agora, Agora));

    [Fact]
    public void Interpretar_RejeitaCaminhoLongo()
        => Assert.Null(Pedidos.Interpretar(JsonSerializer.Serialize(new Pedido("verificar", "C:\\" + new string('a', 998))), Agora, Agora));

    [Fact]
    public void Interpretar_RejeitaPedidoAntigo()
        => Assert.Null(Pedidos.Interpretar("{\"Acao\":\"mostrar\"}", Agora.AddMinutes(-3), Agora));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Enviar_ECaixaEntregamUmaVez(bool antesDeIniciar)
    {
        var recebido = new TaskCompletionSource<Pedido>(TaskCreationOptions.RunContinuationsAsynchronously);
        var quantidade = 0;
        var pedido = new Pedido("verificar", _pasta);
        using var caixa = new CaixaDePedidos();
        caixa.Chegou += p => { Interlocked.Increment(ref quantidade); recebido.TrySetResult(p); };
        if (antesDeIniciar) Assert.True(Pedidos.Enviar(pedido));
        caixa.Iniciar();
        if (!antesDeIniciar) Assert.True(Pedidos.Enviar(pedido));
        Assert.Equal(pedido, await recebido.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        await EsperarConsumo(_pasta);
        caixa.Dispose();
        Assert.Equal(1, quantidade);
        Assert.Empty(Directory.GetFiles(_pasta));
    }

    [Fact]
    public async Task Caixa_ApagaInvalidoEContinuaLendo()
    {
        File.WriteAllText(Path.Combine(_pasta, "invalido.json"), "invalido");
        var recebido = new TaskCompletionSource<Pedido>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var caixa = new CaixaDePedidos();
        caixa.Chegou += p => recebido.TrySetResult(p);
        caixa.Iniciar();
        Assert.True(Pedidos.Enviar(new Pedido("mostrar", null)));
        await recebido.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // O processamento inicial e o watcher podem chegar em ordens diferentes.
        for (int i = 0; i < 100 && Directory.GetFiles(_pasta).Length > 0; i++) await Task.Delay(20);
        Assert.Empty(Directory.GetFiles(_pasta));
    }

    [Fact]
    public void Enviar_FalhaSemLancar()
    {
        var arquivo = Path.Combine(_pasta, "arquivo");
        File.WriteAllText(arquivo, "");
        Pedidos.PastaTeste = arquivo;
        Assert.False(Pedidos.Enviar(new Pedido("mostrar", null)));
    }


    [Theory]
    [InlineData(@"\\servidor\pasta")]
    [InlineData(@"\\.\PhysicalDrive0")]
    [InlineData(@"\\?\C:\x")]
    [InlineData("relativo")]
    [InlineData("C:x")]
    [InlineData("C:\\pasta\u0001arquivo")]
    [InlineData("C:\\pasta\tarquivo")]
    [InlineData("C:\\x?")]
    [InlineData("C:\\x*")]
    [InlineData("C:\\x\"")]
    [InlineData("C:\\x<")]
    [InlineData("C:\\x>")]
    [InlineData("C:\\x|")]
    public void CaminhosInvalidos_SaoRejeitados(string caminho)
    {
        Assert.False(Pedidos.CaminhoLocalValido(caminho));
        var pedido = new Pedido("verificar", caminho);
        Assert.Null(Pedidos.Validar(pedido));
        Assert.Null(Pedidos.Interpretar(JsonSerializer.Serialize(pedido), Agora, Agora));
    }

    [Theory]
    [InlineData(@"C:\Users\x")]
    [InlineData("Z:/pasta")]
    public void CaminhoLocalValido_NaoConsultaUnidade(string caminho)
    {
        Assert.True(Pedidos.CaminhoLocalValido(caminho));
        Assert.NotNull(Pedidos.Validar(new Pedido("verificar", caminho)));
    }

    private static async Task EsperarConsumo(string pasta)
    {
        for (var i = 0; i < 200 && Directory.GetFiles(pasta, "*.json").Length > 0; i++)
            await Task.Delay(25);
        Assert.Empty(Directory.GetFiles(pasta, "*.json"));
    }

    [Fact]
    public async Task Caixa_Descarta5000BytesSemEntregar()
    {
        File.WriteAllText(Path.Combine(_pasta, "grande.json"),
            JsonSerializer.Serialize(new Pedido("mostrar", null)).PadRight(5000));
        var quantidade = 0;
        using var caixa = new CaixaDePedidos();
        caixa.Chegou += _ => Interlocked.Increment(ref quantidade);
        caixa.Iniciar();
        Assert.Null(caixa.Erro);
        await EsperarConsumo(_pasta);
        Assert.Equal(0, quantidade);
    }

    [Fact]
    public async Task Caixa_ApagaSymlinkSemLerNemApagarAlvo()
    {
        var alvo = Path.Combine(_pasta, "alvo.txt");
        var link = Path.Combine(_pasta, "link.json");
        var conteudo = JsonSerializer.Serialize(new Pedido("mostrar", null));
        File.WriteAllText(alvo, conteudo);
        try { File.CreateSymbolicLink(link, alvo); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { return; }
        var quantidade = 0;
        using var caixa = new CaixaDePedidos();
        caixa.Chegou += _ => Interlocked.Increment(ref quantidade);
        caixa.Iniciar();
        await EsperarConsumo(_pasta);
        Assert.Equal(0, quantidade);
        Assert.Equal(conteudo, File.ReadAllText(alvo));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Caixa_RecusaJunctionSemApagarConteudo(bool naPastaPai)
    {
        var alvo = Path.Combine(_pasta, "alvo");
        var link = Path.Combine(_pasta, "junction");
        Directory.CreateDirectory(alvo);
        var arquivo = Path.Combine(alvo, "preservar.json");
        File.WriteAllText(arquivo, JsonSerializer.Serialize(new Pedido("mostrar", null)));
        using var processo = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(CaminhosSistema.Cmd,
            $"/c mklink /J \"{link}\" \"{alvo}\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
        if (processo == null) return;
        processo.WaitForExit();
        if (processo.ExitCode != 0) return;
        try
        {
            Pedidos.PastaTeste = naPastaPai ? Path.Combine(link, "pedidos") : link;
            using var caixa = new CaixaDePedidos();
            caixa.Iniciar();
            Assert.False(string.IsNullOrWhiteSpace(caixa.Erro));
            Assert.True(File.Exists(arquivo));
            Assert.False(Directory.Exists(Path.Combine(alvo, "pedidos")));
        }
        finally { Directory.Delete(link); }
    }

    [Fact]
    public async Task Caixa_LimitaLoteA20EApagaExcesso()
    {
        for (var i = 0; i < 25; i++)
            Assert.True(Pedidos.Enviar(new Pedido("verificar", @"C:\Users\x")));
        var quantidade = 0;
        using var caixa = new CaixaDePedidos();
        caixa.Chegou += _ => Interlocked.Increment(ref quantidade);
        caixa.Iniciar();
        await EsperarConsumo(_pasta);
        Assert.InRange(quantidade, 1, 20);
    }

    [Fact]
    public async Task Caixa_AgrupaMostrarPorDoisSegundos()
    {
        for (var i = 0; i < 10; i++) Assert.True(Pedidos.Enviar(new Pedido("mostrar", null)));
        var quantidade = 0;
        using var caixa = new CaixaDePedidos();
        caixa.Chegou += _ => Interlocked.Increment(ref quantidade);
        caixa.Iniciar();
        await EsperarConsumo(_pasta);
        Assert.Equal(1, quantidade);
    }

    [Fact]
    public async Task Caixa_IniciarEDisposeSaoIdempotentesEPrendemPastas()
    {
        var pasta = Path.Combine(_pasta, "pedidos");
        Pedidos.PastaTeste = pasta;
        var caixa = new CaixaDePedidos();
        try
        {
            caixa.Iniciar();
            caixa.Iniciar();
            Assert.Null(caixa.Erro);
            Assert.Throws<IOException>(() => Directory.Move(pasta, pasta + "-movida"));
            Assert.Throws<IOException>(() => Directory.Move(_pasta, _pasta + "-movida"));
        }
        finally { caixa.Dispose(); caixa.Dispose(); }
        caixa.Iniciar();
        await EsperarLiberacao(pasta);
    }

    private static async Task EsperarLiberacao(string pasta)
    {
        for (var i = 0; ; i++)
        {
            try { Directory.Move(pasta, pasta + "-movida"); return; }
            catch (IOException) when (i < 100) { await Task.Delay(20); }
        }
    }

    [Fact]
    public async Task Caixa_DisposeNaoEsperaConsumidorELiberaHandlesDepois()
    {
        var pasta = Path.Combine(_pasta, "pedidos");
        Pedidos.PastaTeste = pasta;
        Assert.True(Pedidos.Enviar(new Pedido("mostrar", null)));
        using var liberar = new ManualResetEventSlim();
        var entrou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var caixa = new CaixaDePedidos();
        caixa.Chegou += _ => { entrou.TrySetResult(); liberar.Wait(TimeSpan.FromSeconds(10)); };
        caixa.Iniciar();
        try
        {
            await entrou.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var cronometro = System.Diagnostics.Stopwatch.StartNew();
            caixa.Dispose();
            caixa.Dispose();
            Assert.True(cronometro.Elapsed < TimeSpan.FromSeconds(2.5));
        }
        finally { liberar.Set(); }
        await EsperarLiberacao(pasta);
    }

    public void Dispose()
    {
        Pedidos.PastaTeste = null;
        Directory.Delete(_pasta, true);
    }
}
