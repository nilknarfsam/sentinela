using Sentinela.Servicos;

namespace Sentinela.Testes;

public class DefenderTestes
{
    [Theory]
    [InlineData("Trojan:Teste", null, "-Restore -Name \"Trojan:Teste\"")]
    [InlineData("Teste", @"C:\Minha pasta\arquivo.exe", "-Restore -Name \"Teste\" -FilePath \"C:\\Minha pasta\\arquivo.exe\"")]
    [InlineData("Teste", @"C:\", "-Restore -Name \"Teste\" -FilePath \"C:\\\\\"")]
    public void ArgumentosRestaurar_Formata(string nome, string? caminho, string esperado)
        => Assert.Equal(esperado, Defender.ArgumentosRestaurar(nome, caminho));

    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("Teste", "")]
    [InlineData("Teste", "  ")]
    [InlineData("amea\"ça", null)]
    [InlineData("amea\nça", null)]
    [InlineData("Teste", "C:\\ar\"quivo")]
    [InlineData("Teste", "C:\\ar\tquivo")]
    public void ArgumentosRestaurar_RejeitaEntradaInvalida(string nome, string? caminho)
        => Assert.Throws<ArgumentException>(() => Defender.ArgumentosRestaurar(nome, caminho));

    [Theory]
    [InlineData(TipoVarredura.Rapida, null, "-Scan -ScanType 1")]
    [InlineData(TipoVarredura.Completa, null, "-Scan -ScanType 2")]
    [InlineData(TipoVarredura.Personalizada, @"C:\Minha pasta\arquivo.txt", "-Scan -ScanType 3 -File \"C:\\Minha pasta\\arquivo.txt\"")]
    [InlineData(TipoVarredura.Personalizada, @"C:\", "-Scan -ScanType 3 -File \"C:\\\\\"")]
    public void ArgumentosVarredura_Formata(TipoVarredura tipo, string? caminho, string esperado)
        => Assert.Equal(esperado, Defender.ArgumentosVarredura(tipo, caminho));

    [Theory]
    [InlineData(@"\\servidor\pasta")]
    [InlineData(@"\\.\PhysicalDrive0")]
    [InlineData(@"\\?\C:\x")]
    [InlineData("relativo")]
    [InlineData("C:x")]
    [InlineData("C:\\pasta\u0001arquivo")]
    [InlineData("C:\\x*")]
    [InlineData("C:\\x?")]
    [InlineData("C:\\x<")]
    [InlineData("C:\\x>")]
    [InlineData("C:\\x|")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("C:\\pasta\\ar\"quivo")]
    public void ArgumentosVarredura_RejeitaCaminhoInvalido(string? caminho)
        => Assert.Throws<ArgumentException>(() => Defender.ArgumentosVarredura(TipoVarredura.Personalizada, caminho));

    [Theory]
    [InlineData(@"file:_C:\x\a.exe", @"C:\x\a.exe")]
    [InlineData("sem prefixo", "sem prefixo")]
    public void LimparRecurso_RemovePrefixo(string recurso, string esperado)
        => Assert.Equal(esperado, Defender.LimparRecurso(recurso));

    [Theory]
    [InlineData(1, "Baixa")]
    [InlineData(2, "Moderada")]
    [InlineData(4, "Alta")]
    [InlineData(5, "Grave")]
    [InlineData(999, "Desconhecida")]
    public void Gravidade_TraduzCodigo(int codigo, string esperado)
        => Assert.Equal(esperado, Defender.Gravidade(codigo));

    [Theory]
    [InlineData(1, "Detectada")]
    [InlineData(2, "Limpa")]
    [InlineData(3, "Em quarentena")]
    [InlineData(4, "Removida")]
    [InlineData(5, "Permitida")]
    [InlineData(6, "Bloqueada")]
    [InlineData(102, "Falha na quarentena")]
    [InlineData(103, "Falha ao remover")]
    [InlineData(107, "Falha ao bloquear")]
    [InlineData(999, "Situação 999")]
    public void Situacao_TraduzCodigo(int codigo, string esperado)
        => Assert.Equal(esperado, Defender.Situacao(codigo));
}
