using System.Xml.Linq;
using Sentinela.Servicos;

namespace Sentinela.Testes;

public class TarefasDoInstaladorTestes
{
    [Theory]
    [InlineData("S-1-5-21-100-200-300-1001")]
    [InlineData("S-1-5-21-0-0-0-0")]
    [InlineData("S-1-12-1-111-222-333-444")]
    public void SidValido_AceitaSidsDeContaLocal(string sid)
        => Assert.True(TarefasDoInstalador.SidValido(sid));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("S-1-5-21-100-200-300")]
    [InlineData("S-1-5-32-544")]
    [InlineData("S-1-5-21-100-200-300-1001-extra")]
    [InlineData("S-1-5-18")]
    [InlineData(" S-1-5-21-100-200-300-1001")]
    [InlineData("S-1-5-21-100-200-300-1001 ")]
    [InlineData("S-1-5-21-100-200-300-1001\"")]
    [InlineData("S-1-5-21-100-200-300-1001 with-space")]
    [InlineData("S-1-5-21-100-200-300-1001\n<Actions>")]
    [InlineData("S-1-5-21-100-200-300-1001\n")]
    [InlineData("S-1-5-21-100-200-300-1001&whoami")]
    public void SidValido_RecusaFormatoIncorretoEInjecao(string? sid)
        => Assert.False(TarefasDoInstalador.SidValido(sid));

    [Fact]
    public void MontarXmlTarefa_DaAoUsuarioSoLeituraEExecucao()
    {
        const string sid = "S-1-5-21-100-200-300-1001";
        var xml = TarefasDoInstalador.MontarXmlTarefa(sid, "C:\\Program Files\\Sentinela\\Sentinela.exe", false);
        Assert.Contains("<SecurityDescriptor>D:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;FRFX;;;" + sid + ")</SecurityDescriptor>", xml);
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void MontarXmlTarefa_PreservaTarefaDesligada(bool ligada, string esperado)
    {
        var documento = XDocument.Parse(TarefasDoInstalador.MontarXmlTarefa(
            "S-1-5-21-100-200-300-1001", "C:\\Program Files\\Sentinela\\Sentinela.exe", true, ligada));
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        Assert.Equal(esperado, documento.Root!.Element(ns + "Settings")!.Element(ns + "Enabled")!.Value);
    }

    [Fact]
    public void InterpretarArgumentos_AceitaSomenteComandoEstritoDeInstalacao()
    {
        const string sid = "S-1-5-21-100-200-300-1001";
        Assert.True(TarefasDoInstalador.InterpretarArgumentos(["--instalar-tarefas", "--sid", sid], out var comando));
        Assert.Equal(TarefasDoInstalador.AcaoInstalador.InstalarTarefas, comando.Acao);
        Assert.Equal(sid, comando.Sid);
        Assert.False(TarefasDoInstalador.InterpretarArgumentos(["--instalar-tarefas", sid], out _));
        Assert.False(TarefasDoInstalador.InterpretarArgumentos(["--instalar-tarefas", "--sid", sid, "extra"], out _));
    }

    [Fact]
    public void ContemComandoInstalador_DetectaFlagMalformadaEmQualquerPosicao()
    {
        Assert.True(TarefasDoInstalador.ContemComandoInstalador(["--captura", "tela.png", "--instalar-tarefas"]));
        Assert.True(TarefasDoInstalador.ContemComandoInstalador(["--REMOVER-TAREFAS"]));
        Assert.False(TarefasDoInstalador.ContemComandoInstalador(["--captura", "tela.png"]));
    }

    [Fact]
    public void InterpretarArgumentos_AceitaRemocaoSemArgumentosExtras()
    {
        Assert.True(TarefasDoInstalador.InterpretarArgumentos(["--remover-tarefas"], out var comando));
        Assert.Equal(TarefasDoInstalador.AcaoInstalador.RemoverTarefas, comando.Acao);
        Assert.False(TarefasDoInstalador.InterpretarArgumentos(["--remover-tarefas", "extra"], out _));
    }

    [Fact]
    public void MontarXmlTarefa_EscapaCaminhoEConfiguraTarefaDeLogon()
    {
        const string sid = "S-1-5-21-100-200-300-1001";
        const string executavel = "C:\\Program Files\\Sentinela <Dados> & Teste\\Sentinela.exe";
        var xml = TarefasDoInstalador.MontarXmlTarefa(sid, executavel, true);
        Assert.Contains("encoding=\"utf-16\"", xml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;Dados&gt; &amp; Teste", xml, StringComparison.Ordinal);
        var documento = XDocument.Parse(xml);
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        Assert.Equal("1.2", documento.Root!.Attribute("version")!.Value);
        Assert.Equal(sid, documento.Root.Element(ns + "Principals")!.Element(ns + "Principal")!.Element(ns + "UserId")!.Value);
        Assert.Equal("InteractiveToken", documento.Root.Element(ns + "Principals")!.Element(ns + "Principal")!.Element(ns + "LogonType")!.Value);
        Assert.Equal("HighestAvailable", documento.Root.Element(ns + "Principals")!.Element(ns + "Principal")!.Element(ns + "RunLevel")!.Value);
        Assert.Equal(sid, documento.Root.Element(ns + "Triggers")!.Element(ns + "LogonTrigger")!.Element(ns + "UserId")!.Value);
        Assert.Equal("PT20S", documento.Root.Element(ns + "Triggers")!.Element(ns + "LogonTrigger")!.Element(ns + "Delay")!.Value);
        Assert.Equal(executavel, documento.Root.Element(ns + "Actions")!.Element(ns + "Exec")!.Element(ns + "Command")!.Value);
        Assert.Equal("--bandeja", documento.Root.Element(ns + "Actions")!.Element(ns + "Exec")!.Element(ns + "Arguments")!.Value);
        Assert.Equal("false", documento.Root.Element(ns + "Settings")!.Element(ns + "DisallowStartIfOnBatteries")!.Value);
        Assert.Equal("false", documento.Root.Element(ns + "Settings")!.Element(ns + "StopIfGoingOnBatteries")!.Value);
        Assert.Equal("Parallel", documento.Root.Element(ns + "Settings")!.Element(ns + "MultipleInstancesPolicy")!.Value);
        Assert.Equal("PT0S", documento.Root.Element(ns + "Settings")!.Element(ns + "ExecutionTimeLimit")!.Value);
        Assert.Equal("true", documento.Root.Element(ns + "Settings")!.Element(ns + "StartWhenAvailable")!.Value);
    }

    [Fact]
    public void MontarXmlTarefa_AcaoDeAbrirNaoTemGatilhoNemArgumento()
    {
        const string sid = "S-1-5-21-100-200-300-1001";
        var documento = XDocument.Parse(TarefasDoInstalador.MontarXmlTarefa(sid, "C:\\Program Files\\Sentinela\\Sentinela.exe", false));
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        Assert.Null(documento.Root!.Element(ns + "Triggers"));
        Assert.Null(documento.Root.Element(ns + "Actions")!.Element(ns + "Exec")!.Element(ns + "Arguments"));
    }
}
