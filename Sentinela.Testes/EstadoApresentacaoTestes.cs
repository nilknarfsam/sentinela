using Sentinela.Servicos;
using Sentinela.Servicos.Checkup;
using Sentinela.Apresentacao;
using Sentinela.Servicos.Seguranca;
using Sentinela.Paginas;

namespace Sentinela.Testes;

public sealed class EstadoApresentacaoTestes
{
    private static ResultadoVerificacao Item(string resumo, string detalhe = "Tudo verificado.")
        => new("Teste", "", NivelVerificacao.Ok, resumo, detalhe, null);

    private static ResultadoCheckup Resultado(params ResultadoVerificacao[] itens)
        => new(DateTime.Now, 99, itens, TimeSpan.Zero);

    private static ResultadoCheckup ResultadoNota(int nota, params ResultadoVerificacao[] itens)
        => new(DateTime.Now, nota, itens, TimeSpan.Zero);

    [Fact]
    public void Nenhum_item_indisponivel_e_completo()
        => Assert.Equal(EstadoApresentacao.Estado.Completo,
            EstadoApresentacao.Avaliar(Resultado(Item("Firewall ligado"))));

    [Theory]
    [InlineData("Não consegui verificar agora")]
    [InlineData("Precisa de administrador")]
    [InlineData("Valor normal", "Alguns dados precisam de administrador")]
    [InlineData("Valor normal", "Leitura parcial da segurança do dispositivo")]
    public void Falha_permissao_ou_detalhe_parcial_sinaliza_resultado_parcial(string resumo, string detalhe = "Tudo verificado.")
        => Assert.Equal(EstadoApresentacao.Estado.Parcial,
            EstadoApresentacao.Avaliar(Resultado(Item(resumo, detalhe), Item("Outro item confirmado"))));

    [Fact]
    public void Todos_indisponiveis_nao_se_apresentam_como_confirmados()
        => Assert.Equal(EstadoApresentacao.Estado.Indisponivel,
            EstadoApresentacao.Avaliar(Resultado(Item("Não consegui verificar agora"), Item("Abra como administrador"))));

    [Fact]
    public void Resultado_salvo_sem_detalhes_e_tratado_como_parcial()
    {
        var salvo = Resultado(Item("Dispositivo com problema", "Resultado da última verificação salva."));
        Assert.Equal(EstadoApresentacao.Estado.Parcial, EstadoApresentacao.Avaliar(salvo));
        Assert.Contains("detalhes limitados", EstadoApresentacao.Mensagem(salvo, "a proteção"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Alerta_legitimo_nao_e_indisponibilidade()
        => Assert.False(EstadoApresentacao.TemIndisponibilidade(Item("Há atualização esperando reinício")));

    [Fact]
    public void Firewall_desligado_com_perfil_nao_lido_e_parcial_sem_apagar_o_alerta()
    {
        var firewall = VerificacaoFirewall.Avaliar(false, null, true);
        Assert.Equal(NivelVerificacao.Problema, firewall.Nivel);
        Assert.True(EstadoApresentacao.TemIndisponibilidade(firewall));
        var resultado = Resultado(firewall, Item("Outro item confirmado"));
        Assert.Equal("Vermelho", EstadoApresentacao.Cor(resultado, "Verde"));
        Assert.Equal("Há problemas identificados · resultado parcial", EstadoApresentacao.Titulo(resultado, "Seguro"));
    }

    [Fact]
    public void Exclusoes_ignoradas_por_leitura_parcial_sinalizam_parcial()
    {
        var exclusoes = VerificacaoExclusoes.Avaliar([], [], ignoradas: true);
        Assert.Equal("Algumas exclusões não puderam ser lidas", exclusoes.Resumo);
        Assert.True(EstadoApresentacao.TemIndisponibilidade(exclusoes));
    }

    [Fact]
    public void BitLocker_indisponivel_sinaliza_parcial_mesmo_com_alerta_de_boot()
    {
        var dispositivo = VerificacaoDispositivo.Avaliar(false, true, null, admin: true);
        Assert.Equal(NivelVerificacao.Atencao, dispositivo.Nivel);
        Assert.True(EstadoApresentacao.TemIndisponibilidade(dispositivo));
    }

    [Fact]
    public void Referencia_ilegivel_nao_e_resultado_completo()
    {
        var exclusoes = VerificacaoExclusoes.Avaliar([], [], referenciaInvalida: true);
        Assert.True(EstadoApresentacao.TemIndisponibilidade(exclusoes));
    }

    [Fact]
    public void Nota_baixa_salva_permanece_em_vermelho_com_ressalva()
    {
        var salvo = ResultadoNota(45, Item("Precisa de atenção", "Resultado da última verificação salva."));
        Assert.Equal("Vermelho", EstadoApresentacao.Cor(salvo, "Ambar"));
        Assert.Equal("Há problemas identificados · resultado salvo · detalhes limitados", EstadoApresentacao.Titulo(salvo, "Ótimo"));
    }

    [Fact]
    public void Parcial_sem_problema_conhecido_permanece_em_ambar()
    {
        var parcial = Resultado(Item("Não consegui verificar agora"), Item("Firewall ligado"));
        Assert.Equal("Ambar", EstadoApresentacao.Cor(parcial, "Verde"));
        Assert.Equal("Resultado parcial", EstadoApresentacao.Titulo(parcial, "Seguro"));
    }

    [Fact]
    public void Sem_resultado_ainda_distingue_estado_nao_verificado()
        => Assert.Equal("Ainda não verificado", PaginaInicio.TextoCheckup(null, DateTime.Now).Texto);
}
