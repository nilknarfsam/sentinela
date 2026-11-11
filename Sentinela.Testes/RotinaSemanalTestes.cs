using Sentinela.Servicos;

namespace Sentinela.Testes;

public class RotinaSemanalTestes
{
    [Fact]
    public void PrimeiraExecucaoInicializaSemRecuperarHorarioAntigo()
    {
        var config = new Configuracoes();
        var agora = new DateTime(2026, 11, 11, 13, 0, 0);
        Assert.True(config.PrepararRotinaSemanal(agora));
        Assert.Equal(agora, config.UltimaTentativaVarreduraSemanal);
        Assert.False(config.PrepararRotinaSemanal(agora.AddHours(1)));
        Assert.False(RotinaSemanal.DeveVarrer(agora, config.DiaVarreduraSemanal, config.HoraVarreduraSemanal,
            null, config.UltimaTentativaVarreduraSemanal, TimeSpan.FromHours(1), false, true));
    }

    [Fact]
    public void LigarOuMudarHorarioReiniciaReferenciaDesligarPreserva()
    {
        var antes = new DateTime(2026, 11, 1);
        var agora = antes.AddDays(10);
        var config = new Configuracoes { UltimaTentativaVarreduraSemanal = antes };
        config.AlterarRotinaSemanal(false, DayOfWeek.Sunday, 12, agora);
        Assert.Equal(antes, config.UltimaTentativaVarreduraSemanal);
        config.AlterarRotinaSemanal(true, DayOfWeek.Sunday, 12, agora);
        Assert.Equal(agora, config.UltimaTentativaVarreduraSemanal);
        config.AlterarRotinaSemanal(true, DayOfWeek.Monday, 12, agora.AddHours(1));
        Assert.Equal(agora.AddHours(1), config.UltimaTentativaVarreduraSemanal);
        config.AlterarRotinaSemanal(true, DayOfWeek.Monday, 13, agora.AddHours(2));
        Assert.Equal(agora.AddHours(2), config.UltimaTentativaVarreduraSemanal);
        config.AlterarRotinaSemanal(true, DayOfWeek.Monday, 13, agora.AddHours(3));
        Assert.Equal(agora.AddHours(2), config.UltimaTentativaVarreduraSemanal);
    }

    [Fact]
    public void AvisoReverteMemoriaSeSalvarFalharENaoMarcaSemBandeja()
    {
        var antes = new DateTime(2026, 11, 2);
        var config = new Configuracoes { UltimoRelatorioAvisado = antes };
        Assert.False(config.MarcarRelatorioAvisado(antes.AddDays(7), true, () => false));
        Assert.Equal(antes, config.UltimoRelatorioAvisado);
        Assert.False(config.MarcarRelatorioAvisado(antes.AddDays(7), false, () => throw new Exception("Não deveria salvar")));
        Assert.Equal(antes, config.UltimoRelatorioAvisado);
        Assert.True(config.MarcarRelatorioAvisado(antes.AddDays(7), true, () => true));
        Assert.Equal(antes.AddDays(7), config.UltimoRelatorioAvisado);
    }

    [Theory]
    [InlineData("2026-11-08T12:00:00", DayOfWeek.Sunday, 12, "2026-11-08T12:00:00")]
    [InlineData("2026-11-08T11:59:59", DayOfWeek.Sunday, 12, "2026-11-01T12:00:00")]
    [InlineData("2026-11-09T00:00:00", DayOfWeek.Sunday, 23, "2026-11-08T23:00:00")]
    [InlineData("2026-11-08T00:00:00", DayOfWeek.Monday, 0, "2026-11-02T00:00:00")]
    [InlineData("2026-11-11T23:00:00", DayOfWeek.Wednesday, 23, "2026-11-11T23:00:00")]
    [InlineData("2027-01-01T00:00:00", DayOfWeek.Thursday, 23, "2026-12-31T23:00:00")]
    public void CalculaUltimoMomento(string agora, DayOfWeek dia, int hora, string esperado)
        => Assert.Equal(DateTime.Parse(esperado), RotinaSemanal.MomentoMarcado(DateTime.Parse(agora), dia, hora));

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(59, 0, true)]
    [InlineData(60, 0, true)]
    [InlineData(61, 9, false)]
    [InlineData(61, 10, true)]
    [InlineData(4320, 9, false)]
    [InlineData(4320, 10, true)]
    public void RecuperacaoDependeDeOciosidade(int atraso, int ocioso, bool esperado)
    {
        var momento = new DateTime(2026, 11, 8, 12, 0, 0);
        Assert.Equal(esperado, RotinaSemanal.DeveVarrer(momento.AddMinutes(atraso), DayOfWeek.Sunday, 12,
            null, null, TimeSpan.FromMinutes(ocioso), false, true));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void CompletaOuTentativaNoCicloImpedeRepeticao(int minutos, bool esperado)
    {
        var momento = new DateTime(2026, 11, 8, 12, 0, 0);
        var data = momento.AddMinutes(minutos);
        Assert.Equal(esperado, RotinaSemanal.DeveVarrer(momento.AddMinutes(30), DayOfWeek.Sunday, 12, data, null, TimeSpan.Zero, false, true));
        Assert.Equal(esperado, RotinaSemanal.DeveVarrer(momento.AddMinutes(30), DayOfWeek.Sunday, 12, null, data, TimeSpan.Zero, false, true));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void RespeitaDesligadaEVarreduraEmAndamento(bool varrendo, bool ligada)
        => Assert.False(RotinaSemanal.DeveVarrer(new(2026, 11, 8, 12, 0, 0), DayOfWeek.Sunday, 12,
            null, null, TimeSpan.FromDays(1), varrendo, ligada));

    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    public void HoraInvalidaNaoDispara(int hora) => Assert.False(RotinaSemanal.DeveVarrer(DateTime.Now,
        DayOfWeek.Sunday, hora, null, null, TimeSpan.FromDays(1), false, true));
}
