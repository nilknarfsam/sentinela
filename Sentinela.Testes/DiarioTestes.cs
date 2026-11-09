using Sentinela.Servicos;

namespace Sentinela.Testes;

[CollectionDefinition("Diário", DisableParallelization = true)]
public class ColecaoDiario;

[Collection("Diário")]
public class DiarioTestes : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "Sentinela-Diario-" + Guid.NewGuid());
    private readonly bool _desligadoAnterior = Diario.Desligado;

    public DiarioTestes()
    {
        Directory.CreateDirectory(_pasta);
        Diario.PastaTeste = _pasta;
        Diario.Desligado = false;
    }

    [Fact]
    public void Registrar_ELerDevolvemEntrada()
    {
        var antes = DateTime.Now;
        Diario.Registrar("limpeza", "Arquivos removidos", 123);
        var entrada = Assert.Single(Diario.Ler(1));
        Assert.Equal("limpeza", entrada.Tipo);
        Assert.Equal("Arquivos removidos", entrada.Mensagem);
        Assert.Equal(123L, entrada.Bytes);
        Assert.InRange(entrada.Data, antes, DateTime.Now);
    }

    [Fact]
    public void Ler_IgnoraLinhaInvalida()
    {
        File.WriteAllText(Path.Combine(_pasta, "diario.jsonl"), "não é JSON\n");
        Diario.Registrar("teste", "Entrada válida");
        Assert.Equal("Entrada válida", Assert.Single(Diario.Ler(1)).Mensagem);
    }

    [Fact]
    public void Ler_UmDiaNaoDevolveEntradaDeTresDiasAtras()
    {
        var data = DateTime.Now.AddDays(-3).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        File.WriteAllText(Path.Combine(_pasta, "diario.jsonl"),
            $$"""{"Data":"{{data}}","Tipo":"teste","Mensagem":"Antiga","Bytes":null}""" + Environment.NewLine);
        Assert.Single(Diario.Ler(4));
        Assert.Empty(Diario.Ler(1));
    }

    [Fact]
    public void Registrar_DesligadoNaoGrava()
    {
        Diario.Desligado = true;
        Diario.Registrar("teste", "Não gravar");
        Assert.False(File.Exists(Path.Combine(_pasta, "diario.jsonl")));
        Assert.Empty(Diario.Ler(1));
    }

    [Fact]
    public void Ler_SemArquivoDevolveListaVazia() => Assert.Empty(Diario.Ler(1));

    public void Dispose()
    {
        Diario.PastaTeste = null;
        Diario.Desligado = _desligadoAnterior;
        Directory.Delete(_pasta, true);
    }
}
