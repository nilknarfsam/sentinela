using Sentinela.Servicos;

namespace Sentinela.Testes;

public class VigiaDefenderTestes
{
    private static readonly DateTime Data = new(2026, 11, 9, 12, 30, 0);

    [Fact]
    public void ConclusaoPreencheDuracaoEstruturada()
    {
        var evento = VigiaDefender.Interpretar(1001, Data, new Dictionary<string, string>
        {
            ["Scan Time Hours"] = "1", ["Scan Time Minutes"] = "2", ["Scan Time Seconds"] = "3",
        });
        Assert.Equal(new TimeSpan(1, 2, 3), evento?.Duracao);
        Assert.Null(VigiaDefender.Interpretar(1000, Data, new Dictionary<string, string>())?.Duracao);
    }

    [Fact]
    public void AmeacaIncluiNomeECaminho()
    {
        var evento = VigiaDefender.Interpretar(1116, Data, new Dictionary<string, string>
            { ["Threat Name"] = "Teste", ["Path"] = @"C:\arquivo.exe" });
        Assert.NotNull(evento);
        Assert.Equal(Data, evento.Data);
        Assert.Equal(TipoEvento.Ameaca, evento.Tipo);
        Assert.Equal("Ameaça detectada", evento.Titulo);
        Assert.Contains("Teste", evento.Detalhe);
        Assert.Contains(@"C:\arquivo.exe", evento.Detalhe);
    }

    [Theory]
    [InlineData("0", "2", "3", "2min 03s")]
    [InlineData("1", "5", "0", "1h 05min")]
    [InlineData("0", "0", "8", "8s")]
    [InlineData("1", "00", "0", "1h 00min")]
    [InlineData("abc", "abc", "abc", "0s")]
    [InlineData("-3", "-3", "-3", "0s")]
    public void VarreduraIncluiParametrosEDuracao(string horas, string minutos, string segundos, string esperado)
    {
        var evento = VigiaDefender.Interpretar(1001, Data, new Dictionary<string, string>
        {
            ["Scan Type"] = "Antimalware", ["Scan Parameters"] = "Verificação Rápida",
            ["Scan Time Hours"] = horas, ["Scan Time Minutes"] = minutos, ["Scan Time Seconds"] = segundos,
        });
        Assert.NotNull(evento);
        Assert.Equal(TipoEvento.Varredura, evento.Tipo);
        Assert.Equal("Varredura terminada", evento.Titulo);
        Assert.Equal("Verificação Rápida · " + esperado, evento.Detalhe);
    }

    [Fact]
    public void MudancaInternaNaoViraEvento()
    {
        Assert.Null(VigiaDefender.Interpretar(5007, Data, new Dictionary<string, string>
        {
            ["Old Value"] = @"HKLM\SOFTWARE\Microsoft\Windows Defender\Features\Controls\386 = 0x0",
            ["New Value"] = @"HKLM\SOFTWARE\Microsoft\Windows Defender\Features\Controls\386 = 0x1",
        }));
    }

    [Theory]
    [InlineData("Paths", @"C:\pasta com espaços\arquivo.exe", true)]
    [InlineData("Paths", @"C:\pasta com espaços\arquivo.exe", false)]
    [InlineData("Processes", "programa.exe", true)]
    [InlineData("Extensions", ".tmp", false)]
    [InlineData("Paths", @"C:\pasta = especial\arquivo.exe", true)]
    [InlineData("Paths", @"C:\pasta = especial\arquivo.exe", false)]
    public void ExclusaoTemCaminhoCorreto(string grupo, string caminho, bool adicionada)
    {
        var valor = @"HKLM\SOFTWARE\Microsoft\Windows Defender\Exclusions\" + grupo + @"\" + caminho + " = 0x0";
        var evento = VigiaDefender.Interpretar(5007, Data, new Dictionary<string, string>
        {
            ["New Value"] = adicionada ? valor : "", ["Old Value"] = adicionada ? "" : valor,
        });
        Assert.NotNull(evento);
        Assert.Equal(TipoEvento.Configuracao, evento.Tipo);
        Assert.Equal($"Exclusão {(adicionada ? "adicionada" : "removida")}: {caminho}", evento.Detalhe);
    }

    [Theory]
    [InlineData("DisableRealtimeMonitoring")]
    [InlineData("disableantispyware")]
    [InlineData("DISABLEANTIVIRUS")]
    [InlineData("DisableBehaviorMonitoring")]
    [InlineData("DisableIOAVProtection")]
    public void AjusteSensivelViraConfiguracao(string nome)
    {
        var evento = VigiaDefender.Interpretar(5007, Data, new Dictionary<string, string>
            { ["Old Value"] = nome + " = 0x0", ["New Value"] = nome + " = 0x1" });
        Assert.Equal(TipoEvento.Configuracao, evento?.Tipo);
        Assert.Equal("Configuração do Defender mudou", evento?.Titulo);
        Assert.Null(VigiaDefender.Interpretar(5007, Data, new Dictionary<string, string>
            { ["Old Value"] = nome + " = 0x1", ["New Value"] = nome + " = 0x0" }));
    }

    [Theory]
    [InlineData("0x5", false)]
    [InlineData("0x0", true)]
    [InlineData("0x4", true)]
    public void AdulteracaoAvisaSomenteAoDesligar(string novo, bool alerta)
    {
        var evento = VigiaDefender.Interpretar(5007, Data, new Dictionary<string, string>
            { ["Old Value"] = "TamperProtection = 0x1", ["New Value"] = "TamperProtection = " + novo });
        if (alerta)
        {
            Assert.NotNull(evento);
            Assert.Equal(TipoEvento.Configuracao, evento.Tipo);
            Assert.Equal("Configuração do Defender mudou", evento.Titulo);
        }
        else Assert.Null(evento);
    }

    [Theory]
    [InlineData("DisableRealtimeMonitoring = 0x1", "disablerealtimemonitoring = 0X1")]
    [InlineData(@"\Exclusions\Paths\C:\teste = 0x0", @"\Exclusions\Paths\C:\teste = 0x0")]
    public void ValoresIguaisIgnorados(string antigo, string novo)
        => Assert.Null(VigiaDefender.Interpretar(5007, Data, new Dictionary<string, string>
            { ["Old Value"] = antigo, ["New Value"] = novo }));

    [Theory]
    [InlineData(1117, TipoEvento.AmeacaTratada, "Ameaça tratada")]
    [InlineData(1118, TipoEvento.Ameaca, "Falha ao tratar ameaça")]
    [InlineData(1119, TipoEvento.Ameaca, "Falha ao tratar ameaça")]
    [InlineData(1000, TipoEvento.Varredura, "Varredura iniciada")]
    [InlineData(1002, TipoEvento.Varredura, "Varredura cancelada")]
    [InlineData(1005, TipoEvento.Varredura, "Varredura falhou")]
    [InlineData(2000, TipoEvento.Assinaturas, "Assinaturas atualizadas")]
    [InlineData(2001, TipoEvento.Assinaturas, "Falha ao atualizar assinaturas")]
    [InlineData(5000, TipoEvento.Protecao, "Proteção em tempo real ligada")]
    [InlineData(5001, TipoEvento.Protecao, "Proteção em tempo real DESLIGADA")]
    [InlineData(5004, TipoEvento.Protecao, "Configuração da proteção em tempo real mudou")]
    [InlineData(5010, TipoEvento.Protecao, "Proteção contra spyware/vírus desligada")]
    [InlineData(5012, TipoEvento.Protecao, "Proteção contra spyware/vírus desligada")]
    public void IdMapeadoTemTipoETitulo(int id, TipoEvento tipo, string titulo)
    {
        var evento = VigiaDefender.Interpretar(id, Data, new Dictionary<string, string>());
        Assert.NotNull(evento);
        Assert.Equal(tipo, evento.Tipo);
        Assert.Equal(titulo, evento.Titulo);
    }

    [Fact]
    public void RecentesComZeroDiasNaoLanca() => Assert.NotNull(VigiaDefender.LerRecentes(0));

    [Fact]
    public void IdDesconhecidoIgnorado() => Assert.Null(VigiaDefender.Interpretar(9999, Data, new Dictionary<string, string>()));

    [Theory]
    [InlineData(1116)] [InlineData(1117)] [InlineData(1118)] [InlineData(1119)]
    [InlineData(1000)] [InlineData(1001)] [InlineData(1002)] [InlineData(1005)]
    [InlineData(2000)] [InlineData(2001)] [InlineData(5000)] [InlineData(5001)]
    [InlineData(5004)] [InlineData(5010)] [InlineData(5012)] [InlineData(5007)]
    public void CamposAusentesNaoLancam(int id)
    {
        var erro = Record.Exception(() => VigiaDefender.Interpretar(id, Data, new Dictionary<string, string>()));
        Assert.Null(erro);
    }

    [Fact]
    public void Interpretar1116_LimpaPrefixoDoCaminho()
    {
        var campos = new Dictionary<string, string>
        {
            ["Threat Name"] = "Virus:DOS/EICAR_Test_File",
            ["Path"] = @"file:_C:\Temp\eicar.com",
        };
        var evento = VigiaDefender.Interpretar(1116, Data, campos);
        Assert.NotNull(evento);
        Assert.Contains(@"C:\Temp\eicar.com", evento!.Detalhe);
        Assert.DoesNotContain("file:", evento.Detalhe);
    }
}
