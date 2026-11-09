using Sentinela.Servicos;

namespace Sentinela.Testes;

public class DefenderTestes
{
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
