using Sentinela.Servicos;

namespace Sentinela.Testes;

[Collection("Diário")]
public sealed class LimpezaComponentesTestes
{
    [Fact]
    public void ComandoCompletoOculto()
    {
        var inicio = LimpezaComponentes.CriarInicio();
        Assert.Equal(Path.Combine(Environment.SystemDirectory, "Dism.exe"), inicio.FileName);
        Assert.True(Path.IsPathFullyQualified(inicio.FileName));
        Assert.Equal("/Online /Cleanup-Image /StartComponentCleanup", inicio.Arguments);
        Assert.False(inicio.UseShellExecute);
        Assert.True(inicio.CreateNoWindow);
        Assert.True(inicio.RedirectStandardOutput);
        Assert.True(inicio.RedirectStandardError);
        Assert.Equal(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage, inicio.StandardOutputEncoding?.CodePage);
        Assert.Equal(inicio.StandardOutputEncoding, inicio.StandardErrorEncoding);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task ExecutavelFalsoDevolveCodigoSaidaEDiario(int codigo)
    {
        var pasta = Path.Combine(Path.GetTempPath(), "Sentinela-Componentes-" + Guid.NewGuid());
        var desligado = Diario.Desligado;
        Diario.PastaTeste = pasta;
        Diario.Desligado = false;
        try
        {
            var r = await LimpezaComponentes.ExecutarInterno(CaminhosSistema.Cmd, $"/d /c \"echo saida-teste & echo erro-teste 1>&2 & exit /b {codigo}\"");
            Assert.Equal(codigo, r.Codigo);
            Assert.Contains("saida-teste", r.Saida);
            Assert.Contains("erro-teste", r.Saida);
            var entrada = Assert.Single(Diario.Ler(1));
            Assert.Equal("limpeza", entrada.Tipo);
            Assert.Contains($"código {codigo}", entrada.Mensagem);
        }
        finally
        {
            Diario.PastaTeste = null;
            Diario.Desligado = desligado;
            if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
        }
    }

    [Fact]
    public async Task FalhaNaoLanca()
    {
        var anterior = Diario.Desligado;
        Diario.Desligado = true;
        try
        {
            var r = await LimpezaComponentes.ExecutarInterno(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe"), "");
            Assert.Null(r.Codigo);
            Assert.Contains("Não foi possível concluir", r.Saida);
        }
        finally { Diario.Desligado = anterior; }
    }
}
