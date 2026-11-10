using Sentinela.Servicos;
using Sentinela.Servicos.Checkup;

namespace Sentinela.Testes;

public class VigiaInicializacaoTestes
{
    [Fact]
    public void Novos_NadaNovo() => Assert.Empty(VigiaInicializacao.Novos(["origem|nome|comando"], ["origem|nome|comando"]));

    [Fact]
    public void Novos_UmNovo() => Assert.Equal(["origem|novo|comando"], VigiaInicializacao.Novos(["origem|nome|comando"], ["origem|nome|comando", "origem|novo|comando"]));

    [Fact]
    public void Novos_RemovidoNaoENovo() => Assert.Empty(VigiaInicializacao.Novos(["a", "b"], ["b"]));

    [Fact]
    public void Novos_IgnoraCaixaEDuplicatas() => Assert.Equal(["novo"], VigiaInicializacao.Novos(["ORIGEM|Nome|Comando"], ["origem|nome|comando", "novo", "NOVO"]));

    [Theory]
    [InlineData(2, 1, NivelVerificacao.Atencao)]
    [InlineData(16, 1, NivelVerificacao.Problema)]
    [InlineData(2, 0, NivelVerificacao.Ok)]
    public void Checkup_AssinaturasElevamNivelMinimo(int ativos, int semAssinatura, NivelVerificacao esperado)
    {
        var resultado = VerificacaoInicializacao.Avaliar(ativos, semAssinatura);
        Assert.Equal(esperado, resultado.Nivel);
        Assert.Contains($"{semAssinatura} programa(s) ativo(s) sem assinatura ou com assinatura inválida", resultado.Detalhe);
    }

    [Theory]
    [InlineData(@"C:\WindowsFake\app.exe", true)]
    [InlineData("%windir%\\System32\\app.exe", false)]
    [InlineData("%windir%\\SysWOW64\\app.exe", false)]
    [InlineData("%windir%\\WinSxS\\app.exe", false)]
    [InlineData("%windir%\\Microsoft.NET\\app.exe", false)]
    [InlineData("%windir%\\System32Fake\\app.exe", true)]
    [InlineData("%windir%\\Temp\\app.exe", true)]
    [InlineData("%windir%\\Tasks\\app.exe", true)]
    [InlineData("%ProgramFiles%\\App\\app.exe", true)]
    [InlineData("app.exe", true)]
    [InlineData("", true)]
    public void Terceiros_ExigeCaminhoForaDaPastaWindows(string comando, bool esperado)
        => Assert.Equal(esperado, InicializacaoSistema.ForaDoWindows(comando));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AvisaERegistraAntesDeGravarESemRepetirQuandoGravacaoFalha(bool lancar)
    {
        using var vigia = new VigiaInicializacao();
        var passos = new List<string>();
        vigia.Chegou += _ => passos.Add("aviso");
        bool Falhar(string caminho, string texto)
        {
            passos.Add("gravação");
            if (lancar) throw new IOException("Falha simulada");
            return false;
        }
        void Registrar(string texto) => passos.Add("diário");
        vigia.ProcessarRetrato([("a", "Antigo", true)], _ => "[\"a\"]", Falhar, Registrar);
        passos.Clear();
        vigia.ProcessarRetrato([("a", "Antigo", true), ("b", "Novo", true)], _ => throw new Exception(), Falhar, Registrar);
        Assert.Equal(["diário", "aviso", "gravação"], passos);
        passos.Clear();
        vigia.ProcessarRetrato([("a", "Antigo", true), ("b", "Novo", true)], _ => throw new Exception(), Falhar, Registrar);
        Assert.Equal(["gravação"], passos);
    }

    [Fact]
    public void ComandoSemExecutavelTemEditoraDesconhecida()
        => Assert.Equal("?", Assinaturas.LerComando("app.exe").Editora);
}
