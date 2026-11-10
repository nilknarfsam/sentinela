using System.Diagnostics;
using Sentinela.Servicos;
using Xunit.Abstractions;

namespace Sentinela.Testes;

public class LimpezaTestes(ITestOutputHelper saida) : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("Sentinela-Limpeza-").FullName;

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(42, "42 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(2048, "2 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1073741824, "1 GB")]
    public void Formatar_MostraUnidadesELimites(long bytes, string esperado)
        => Assert.Equal(esperado, Limpeza.Formatar(bytes));

    [Theory]
    [InlineData(48, 24, true)]
    [InlineData(1, 24, false)]
    [InlineData(0, 0, true)]
    [InlineData(-24, 0, true)]
    public void Velho_RespeitaHorasMinimas(int idadeHoras, int horasMinimas, bool esperado)
    {
        var arquivo = CriarArquivo(_pasta, "idade.txt", idadeHoras);
        Assert.Equal(esperado, Limpeza.Velho(new FileInfo(arquivo), Item(horasMinimas, _pasta)));
    }

    [Fact]
    public void Limpar_ApagaSomenteArquivosVelhos()
    {
        var subpasta = Directory.CreateDirectory(Path.Combine(_pasta, "subpasta")).FullName;
        var velho = CriarArquivo(subpasta, "velho.txt", 48);
        var recente = CriarArquivo(_pasta, "recente.txt", 1);
        var tamanho = new FileInfo(velho).Length;

        Assert.Equal(tamanho, Limpeza.Limpar(Item(24, _pasta)));
        Assert.False(File.Exists(velho));
        Assert.False(Directory.Exists(subpasta));
        Assert.True(File.Exists(recente));
        Assert.True(Directory.Exists(_pasta));
    }

    [Fact]
    public void Limpar_NaoSegueJunction()
    {
        var origem = Directory.CreateDirectory(Path.Combine(_pasta, "origem")).FullName;
        var destino = Directory.CreateDirectory(Path.Combine(_pasta, "destino")).FullName;
        var protegido = CriarArquivo(destino, "protegido.txt", 48);
        var velho = CriarArquivo(origem, "velho.txt", 48);
        var tamanho = new FileInfo(velho).Length;
        var juncao = Path.Combine(origem, "juncao");
        try
        {
            using var processo = Process.Start(new ProcessStartInfo(CaminhosSistema.Cmd, $"/c mklink /J \"{juncao}\" \"{destino}\"")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            });
            if (processo == null)
            {
                saida.WriteLine("Teste de junction dispensado: cmd.exe não iniciou.");
                return;
            }
            processo.WaitForExit();
            if (processo.ExitCode != 0)
            {
                // Sem permissão/suporte a junction, retorna sem falhar conforme o plano de testes.
                saida.WriteLine("Teste de junction dispensado: mklink /J falhou. " + processo.StandardError.ReadToEnd());
                return;
            }

            Assert.Equal(tamanho, Limpeza.Limpar(Item(24, origem)));
            Assert.False(File.Exists(velho));
            Assert.True(File.Exists(protegido));
            Assert.Equal("conteúdo", File.ReadAllText(protegido));
            Assert.True(Directory.Exists(juncao));
        }
        finally
        {
            // Remove apenas a junction, sem percorrer o destino, antes da limpeza da pasta temporária.
            if (Directory.Exists(juncao)) Directory.Delete(juncao);
        }
    }

    [Fact]
    public void Medir_SemPastasExistentesDevolveZero()
        => Assert.Equal(0L, Limpeza.Medir(Item(0, Path.Combine(_pasta, "inexistente"))));

    private static ItemLimpeza Item(int horasMinimas, params string[] pastas)
        => new() { Nome = "Teste", Descricao = "Arquivos de teste", HorasMinimas = horasMinimas, Pastas = pastas };

    private static string CriarArquivo(string pasta, string nome, int idadeHoras)
    {
        var caminho = Path.Combine(pasta, nome);
        File.WriteAllText(caminho, "conteúdo");
        File.SetLastWriteTime(caminho, DateTime.Now.AddHours(-idadeHoras));
        return caminho;
    }

    public void Dispose() => Directory.Delete(_pasta, true);
}
