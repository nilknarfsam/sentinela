using System.Text.Json;
using Sentinela.Paginas;
using Sentinela.Servicos;
using Sentinela.Servicos.Checkup;
using Sentinela.Servicos.Seguranca;
using Servico = Sentinela.Servicos.Seguranca.Seguranca;

namespace Sentinela.Testes;

[Collection("Diário")]
public sealed class SegurancaTestes : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "Sentinela-Seguranca-" + Guid.NewGuid());
    private readonly bool _desligado = Diario.Desligado;
    public SegurancaTestes()
    {
        Servico.PastaTeste = Diario.PastaTeste = _pasta;
        Diario.Desligado = false;
    }
    public void Dispose()
    {
        Servico.PastaTeste = Diario.PastaTeste = null;
        Diario.Desligado = _desligado;
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, true);
    }

    [Theory]
    [InlineData(true, true, true, NivelVerificacao.Ok)]
    [InlineData(false, true, true, NivelVerificacao.Problema)]
    [InlineData(true, false, true, NivelVerificacao.Problema)]
    [InlineData(true, true, false, NivelVerificacao.Problema)]
    [InlineData(null, true, true, NivelVerificacao.Atencao)]
    [InlineData(null, null, null, NivelVerificacao.Atencao)]
    [InlineData(null, true, false, NivelVerificacao.Problema)]
    public void Firewall(bool? dominio, bool? particular, bool? publico, NivelVerificacao nivel)
    {
        var resultado = VerificacaoFirewall.Avaliar(dominio, particular, publico);
        Assert.Equal(nivel, resultado.Nivel);
        Assert.Equal("windowsdefender://network", resultado.Acao?.Alvo);
        if (publico == false) Assert.Contains("Pública", resultado.Resumo);
    }

    [Theory]
    [InlineData(0.0, false, NivelVerificacao.Ok)]
    [InlineData(30.0, false, NivelVerificacao.Ok)]
    [InlineData(30.01, false, NivelVerificacao.Atencao)]
    [InlineData(60.0, false, NivelVerificacao.Atencao)]
    [InlineData(60.01, false, NivelVerificacao.Problema)]
    [InlineData(1.0, true, NivelVerificacao.Atencao)]
    [InlineData(61.0, true, NivelVerificacao.Problema)]
    [InlineData(null, false, NivelVerificacao.Atencao)]
    [InlineData(null, true, NivelVerificacao.Atencao)]
    public void Atualizacoes(double? dias, bool pausado, NivelVerificacao nivel)
    {
        var agora = new DateTime(2026, 11, 11, 12, 0, 0);
        var resultado = VerificacaoWindowsUpdate.Avaliar(dias.HasValue ? agora.AddDays(-dias.Value) : null,
            pausado ? agora.AddDays(1) : agora.AddDays(-1), agora);
        Assert.Equal(nivel, resultado.Nivel);
        if (dias == null && !pausado) Assert.Equal("Não consegui ler a data", resultado.Resumo);
    }

    [Theory]
    [InlineData(1, 5, NivelVerificacao.Ok)]
    [InlineData(0, 5, NivelVerificacao.Problema)]
    [InlineData(1, 0, NivelVerificacao.Atencao)]
    [InlineData(null, null, NivelVerificacao.Ok)]
    [InlineData(1, null, NivelVerificacao.Ok)]
    [InlineData(null, 0, NivelVerificacao.Atencao)]
    [InlineData(null, 5, NivelVerificacao.Ok)]
    public void Uac(int? ligado, int? consentimento, NivelVerificacao nivel)
    {
        var resultado = VerificacaoUac.Avaliar(ligado, consentimento);
        Assert.Equal(nivel, resultado.Nivel);
        Assert.Equal("uac", resultado.Acao?.Alvo);
        Assert.True(Path.IsPathFullyQualified(CaminhosSistema.ConfiguracoesUac));
        Assert.Equal("UserAccountControlSettings.exe", Path.GetFileName(CaminhosSistema.ConfiguracoesUac));
    }

    [Theory]
    [InlineData(null, null, 1, NivelVerificacao.Ok)]
    [InlineData(1, "Warn", 1, NivelVerificacao.Ok)]
    [InlineData(0, "Warn", 1, NivelVerificacao.Problema)]
    [InlineData(null, "Off", 1, NivelVerificacao.Problema)]
    [InlineData(null, "off", 0, NivelVerificacao.Problema)]
    [InlineData(null, null, 0, NivelVerificacao.Atencao)]
    [InlineData(null, null, 2, NivelVerificacao.Atencao)]
    [InlineData(null, null, null, NivelVerificacao.Atencao)]
    public void SmartScreen(int? politica, string? estado, int? pua, NivelVerificacao nivel)
        => Assert.Equal(nivel, VerificacaoSmartScreen.Avaliar(politica, estado, pua).Nivel);

    [Theory]
    [InlineData(1, NivelVerificacao.Ok)]
    [InlineData(0, NivelVerificacao.Atencao)]
    [InlineData(2, NivelVerificacao.Atencao)]
    [InlineData(null, NivelVerificacao.Atencao)]
    public void Ransomware(int? estado, NivelVerificacao nivel)
    {
        var resultado = VerificacaoRansomware.Avaliar(estado);
        Assert.Equal(nivel, resultado.Nivel);
        Assert.Equal("Opcional. Protege Documentos e Imagens, mas pode bloquear programas legítimos; ligue se aceitar liberar programas um a um.", resultado.Detalhe);
    }

    [Theory]
    [InlineData(true, true, true, true, NivelVerificacao.Ok)]
    [InlineData(true, true, false, true, NivelVerificacao.Ok)]
    [InlineData(false, true, true, true, NivelVerificacao.Atencao)]
    [InlineData(true, false, true, true, NivelVerificacao.Ok)]
    [InlineData(null, null, null, true, NivelVerificacao.Ok)]
    [InlineData(true, true, null, true, NivelVerificacao.Ok)]
    [InlineData(true, null, true, true, NivelVerificacao.Ok)]
    [InlineData(null, null, null, false, NivelVerificacao.Ok)]
    [InlineData(true, null, null, false, NivelVerificacao.Ok)]
    [InlineData(false, null, null, false, NivelVerificacao.Atencao)]
    public void Dispositivo(bool? seguro, bool? tpm, bool? bitlocker, bool admin, NivelVerificacao nivel)
    {
        var resultado = VerificacaoDispositivo.Avaliar(seguro, tpm, bitlocker, admin);
        Assert.Equal(nivel, resultado.Nivel);
        if (tpm == false) Assert.Contains("Este PC não tem TPM (limite da placa-mãe)", resultado.Detalhe);
        if (!admin && tpm == null) Assert.Contains("precisa de administrador", resultado.Detalhe);
        if (bitlocker == false) Assert.Contains("opcional", resultado.Detalhe);
    }

    [Fact]
    public void TpmPresenteDesativadoNaoEConfundidoComLimiteDaPlaca()
    {
        var resultado = VerificacaoDispositivo.Avaliar(true, false, true, true, tpmPresente: true);
        Assert.Equal(NivelVerificacao.Ok, resultado.Nivel);
        Assert.Contains("TPM presente, mas desativado", resultado.Detalhe);
        Assert.DoesNotContain("não tem TPM", resultado.Detalhe);
    }

    [Fact]
    public void ExclusoesNenhuma() => Assert.Equal(NivelVerificacao.Ok, VerificacaoExclusoes.Avaliar([], null).Nivel);
    [Fact]
    public void ExclusoesPrimeiraVez()
    {
        var resultado = VerificacaoExclusoes.Avaliar([@"Pasta: C:\projetos"], null);
        Assert.Equal(NivelVerificacao.Atencao, resultado.Nivel);
        Assert.Equal("Confira se você reconhece estas exclusões", resultado.Resumo);
        Assert.Equal(new[] { @"Pasta: C:\projetos" }, VerificacaoExclusoes.ListaExibida(resultado));
    }
    [Fact]
    public void ExclusoesConhecidasSemDiferenciarMaiusculas()
    {
        var resultado = VerificacaoExclusoes.Avaliar([@"Pasta: C:\projetos", "Processo: app.exe", "Extensão: .tmp"],
            [@"PASTA: c:\PROJETOS", "processo: APP.EXE", "EXTENSÃO: .TMP"]);
        Assert.Equal(NivelVerificacao.Ok, resultado.Nivel);
        Assert.Contains("app.exe", resultado.Detalhe);
    }
    [Fact]
    public void ExclusaoNova()
    {
        var resultado = VerificacaoExclusoes.Avaliar([@"Pasta: C:\projetos", "Processo: novo.exe"], [@"Pasta: C:\projetos"]);
        Assert.Equal(NivelVerificacao.Problema, resultado.Nivel);
        Assert.Contains("Exclusão nova no Defender: Processo: novo.exe", resultado.Resumo);
        Assert.Contains("Vírus costumam fazer isso para se esconder.", resultado.Resumo);
        Assert.Equal("windowsdefender://threatsettings", resultado.Acao?.Alvo);
    }
    [Fact]
    public void ReferenciaVaziaDetectaPrimeiraExclusao() => Assert.Equal(NivelVerificacao.Problema, VerificacaoExclusoes.Avaliar(["Processo: novo.exe"], []).Nivel);
    [Theory]
    [InlineData(null)]
    [InlineData("N/A: Must be an administrator to view exclusions")]
    [InlineData("N/A")]
    [InlineData("Pasta: N/A: Must be an administrator...")]
    public void ExclusoesSemAdmin(string? texto)
    {
        var resultado = VerificacaoExclusoes.Avaliar(texto == null ? null : new[] { texto }, []);
        Assert.Equal(NivelVerificacao.Atencao, resultado.Nivel);
        Assert.Equal("Abra como administrador", resultado.Resumo);
        Assert.Null(VerificacaoExclusoes.ListaExibida(resultado));
    }
    [Fact]
    public void ReferenciaLeGravaEValidaLimite()
    {
        var caminho = Path.Combine(_pasta, "exclusoes.json");
        Assert.Null(Servico.LerReferencia(caminho));
        Assert.True(Servico.GravarReferencia(caminho, [@"Pasta: C:\projetos", "Processo: app.exe"]));
        Assert.Equal(new[] { @"Pasta: C:\projetos", "Processo: app.exe" }, Servico.LerReferencia(caminho));
        Assert.False(Servico.GravarReferencia(caminho, [new string('a', 65536)]));
        Assert.False(Servico.GravarReferencia(caminho, ["N/A: Must be an administrator"]));
        Assert.Equal(2, Servico.LerReferencia(caminho)!.Length);
        Assert.True(Servico.GravarReferencia(caminho, []));
        Assert.Empty(Servico.LerReferencia(caminho)!);
        Assert.True(ArquivosSeguros.GravarTexto(caminho, new string(' ', 65537)));
        Assert.Null(Servico.LerReferencia(caminho));
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("[\"\"]")]
    [InlineData("[\"N/A: Must be an administrator\"]")]
    [InlineData("inválido")]
    public void ReferenciaInvalida(string texto)
    {
        var caminho = Path.Combine(_pasta, "exclusoes.json");
        Assert.True(ArquivosSeguros.GravarTexto(caminho, texto));
        Assert.Null(Servico.LerReferencia(caminho));
    }

    [Theory]
    [InlineData("-501", false, true, NivelVerificacao.Problema)]
    [InlineData("-500", false, true, NivelVerificacao.Atencao)]
    [InlineData("-1001", false, false, NivelVerificacao.Atencao)]
    [InlineData("-1001", false, true, NivelVerificacao.Ok)]
    [InlineData("-501", true, false, NivelVerificacao.Ok)]
    [InlineData("-500", true, false, NivelVerificacao.Ok)]
    [InlineData("-1001", false, null, NivelVerificacao.Atencao)]
    public void Contas(string finalSid, bool desativada, bool? senha, NivelVerificacao nivel)
    {
        var resultado = VerificacaoContas.Avaliar([new("S-1-5-21-123" + finalSid, "Maria", desativada, senha)]);
        Assert.Equal(nivel, resultado.Nivel);
        if (!desativada && senha == false) Assert.Contains("A conta Maria pode entrar sem senha", resultado.Detalhe);
    }
    [Fact]
    public void ContasIndisponiveis() => Assert.Equal(NivelVerificacao.Atencao, VerificacaoContas.Avaliar(null).Nivel);
    [Fact]
    public void ConvidadoTemPrioridade() => Assert.Equal(NivelVerificacao.Problema, VerificacaoContas.Avaliar([
        new("S-1-5-21-500", "Administrador", false, false), new("S-1-5-21-501", "Visitante", false, true)]).Nivel);

    [Theory]
    [InlineData("windowsdefender://network", true)]
    [InlineData("ms-settings:windowsupdate", true)]
    [InlineData("windowsdefender://appbrowser", true)]
    [InlineData("windowsdefender://ransomwareprotection", true)]
    [InlineData("windowsdefender://devicesecurity", true)]
    [InlineData("windowsdefender://threatsettings", true)]
    [InlineData("ms-settings:otherusers", true)]
    [InlineData("uac", true)]
    [InlineData("cmd.exe", false)]
    [InlineData("http://exemplo.com", false)]
    [InlineData("ms-settings:", false)]
    [InlineData("windowsdefender://", false)]
    [InlineData("ms-settings:windowsupdate argumento", false)]
    [InlineData("uac /algo", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void AlvoFechado(string? alvo, bool permitido) => Assert.Equal(permitido, PaginaSeguranca.AlvoPermitido(alvo));

    [Fact]
    public async Task SalvaMesmoFormatoDoCheckup()
    {
        var resultado = await Servico.Executar([new Falsa("Boa", 3, _ => Task.FromResult(VerificacaoFirewall.Avaliar(true, true, true))),
            new Falsa("Ruim", 1, _ => Task.FromResult(VerificacaoUac.Avaliar(0, 0)))]);
        Assert.Equal(80, resultado.Nota);
        Assert.Same(resultado, Servico.Ultimo);
        var caminho = Path.Combine(_pasta, "seguranca.json");
        var salvo = Checkup.LerSalvo(caminho);
        Assert.NotNull(salvo);
        Assert.Equal(resultado.Data, salvo.Data);
        Assert.Equal(resultado.Nota, salvo.Nota);
        Assert.Equal(resultado.Itens.Select(i => (i.Nome, i.Nivel, i.Resumo)), salvo.Itens.Select(i => (i.Nome, i.Nivel, i.Resumo)));
        using var json = JsonDocument.Parse(ArquivosSeguros.LerTexto(caminho, 65536)!);
        Assert.Equal(3, json.RootElement.EnumerateObject().Count());
        Assert.Equal("Saúde da segurança: nota 80", Assert.Single(Diario.Ler(1)).Mensagem);
    }
    [Fact]
    public async Task TempoLimiteEFalhaNaoImpedemOutrasVerificacoes()
    {
        var resultado = await Servico.Executar([new Falsa("Lenta", 1, async ct => { await Task.Delay(5000, ct); return VerificacaoUac.Avaliar(1, 5); }),
            new Falsa("Falha", 1, _ => throw new IOException()), new Falsa("Boa", 1, _ => Task.FromResult(VerificacaoUac.Avaliar(1, 5)))], TimeSpan.FromMilliseconds(100));
        Assert.Equal(3, resultado.Itens.Count);
        Assert.Equal(73, resultado.Nota);
        Assert.All(resultado.Itens.Take(2), i => Assert.Equal("Não consegui verificar agora", i.Resumo));
        Assert.True(resultado.Duracao < TimeSpan.FromSeconds(3));
    }
    [Fact]
    public void OitoVerificacoesComPesosEAcoes()
    {
        var lista = Servico.Padrao;
        Assert.Equal(new[] { 3, 3, 3, 2, 1, 2, 3, 2 }, lista.Select(i => i.Peso));
        Assert.Equal(8, lista.Select(i => i.Nome).Distinct().Count());
    }
    [Fact]
    public void ResultadoSalvoNaoPermiteReconhecerExclusoesSemLista()
    {
        var atual = VerificacaoExclusoes.Avaliar(["Processo: app.exe"], null);
        var caminho = Path.Combine(_pasta, "seguranca.json");
        Assert.True(Checkup.GravarSalvo(caminho, new(DateTime.Now, 60, [atual], TimeSpan.Zero)));
        var salvo = Assert.Single(Checkup.LerSalvo(caminho)!.Itens);
        Assert.Null(VerificacaoExclusoes.ListaExibida(salvo));
    }
    [Fact]
    public void PrimeiraLeituraVaziaCriaReferenciaEDetectaNova()
    {
        var caminho = Servico.CaminhoExclusoes;
        Assert.True(Servico.ObterReferencia(caminho).Ausente);
        Assert.Equal(NivelVerificacao.Ok, VerificacaoExclusoes.ProcessarLeitura([], caminho).Nivel);
        Assert.Empty(Servico.LerReferencia(caminho)!);
        var resultado = VerificacaoExclusoes.ProcessarLeitura(["Processo: novo.exe"], caminho);
        Assert.Equal(NivelVerificacao.Problema, resultado.Nivel);
        Assert.Contains("Exclusão nova", resultado.Resumo);
    }
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void NaoCriaReferenciaEmCapturaOuComItensIgnorados(bool captura, bool ignoradas)
    {
        VerificacaoExclusoes.ProcessarLeitura([], Servico.CaminhoExclusoes, captura, ignoradas);
        Assert.False(File.Exists(Servico.CaminhoExclusoes));
    }
    [Fact]
    public void PrimeiraLeituraComExclusoesOuSemAdminNaoGrava()
    {
        Assert.Equal(NivelVerificacao.Atencao, VerificacaoExclusoes.ProcessarLeitura(["Processo: app.exe"], Servico.CaminhoExclusoes).Nivel);
        VerificacaoExclusoes.ProcessarLeitura(null, Servico.CaminhoExclusoes);
        Assert.False(File.Exists(Servico.CaminhoExclusoes));
    }
    [Theory]
    [InlineData("inválido")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("[\"\"]")]
    [InlineData("[\"   \"]")]
    [InlineData("[\"N/A: Must be an administrator\"]")]
    [InlineData("[\"Processo: app.exe\\nProcesso: outro.exe\"]")]
    [InlineData("[\"N/A\"]")]
    [InlineData("[\"Pasta: N/A\"]")]
    [InlineData("[\"Processo: app.exe\\r\"]")]
    public void ReferenciaIlegivelExigeReconhecimentoMesmoComListaVazia(string texto)
    {
        var caminho = Servico.CaminhoExclusoes;
        Assert.True(ArquivosSeguros.GravarTexto(caminho, texto));
        foreach (var atuais in new[] { Array.Empty<string>(), new[] { "Processo: app.exe" } })
        {
            var resultado = VerificacaoExclusoes.ProcessarLeitura(atuais, caminho);
            Assert.Equal(NivelVerificacao.Atencao, resultado.Nivel);
            Assert.Equal("A lista de referência das exclusões está ilegível", resultado.Resumo);
            Assert.Equal(atuais, VerificacaoExclusoes.ListaExibida(resultado));
            Assert.Equal(texto, ArquivosSeguros.LerTexto(caminho, 65536));
        }
        Assert.True(Servico.GravarReferencia(caminho, []));
        Assert.False(Servico.ObterReferencia(caminho).Invalida);
    }
    [Theory]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void ExclusoesComQuebraDeLinhaSaoIgnoradasENuncaGravadas(string quebra)
    {
        var ruim = "Processo: app.exe" + quebra + "Processo: outro.exe";
        var resultado = VerificacaoExclusoes.Avaliar([ruim, "Processo: conhecido.exe"], ["Processo: conhecido.exe"]);
        Assert.Contains("quebra de linha", resultado.Detalhe);
        Assert.DoesNotContain("outro.exe", resultado.Detalhe);
        Assert.Equal(new[] { "Processo: conhecido.exe" }, VerificacaoExclusoes.ListaExibida(resultado));
        Assert.False(Servico.GravarReferencia(Servico.CaminhoExclusoes, [ruim]));
        Assert.Equal(NivelVerificacao.Atencao, VerificacaoExclusoes.Avaliar([], [ruim]).Nivel);
    }
    [Fact]
    public void BitlockerIndisponivelNaoReduzNota()
    {
        var resultado = VerificacaoDispositivo.Avaliar(true, true, null, true);
        Assert.Equal(NivelVerificacao.Ok, resultado.Nivel);
        Assert.Contains("BitLocker não disponível", resultado.Detalhe);
        Assert.Equal(100, Checkup.CalcularNota([(resultado.Nivel, 2)]));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BiosAntigoGeraAtencao(bool admin)
    {
        var resultado = VerificacaoDispositivo.Avaliar(null, true, true, admin, true);
        Assert.Equal(NivelVerificacao.Atencao, resultado.Nivel);
        Assert.Equal("Inicialização segura não suportada (BIOS antigo)", resultado.Resumo);
    }
    [Theory]
    [InlineData(1899, false)]
    [InlineData(1999, false)]
    [InlineData(2000, true)]
    [InlineData(2026, true)]
    public void DataMinimaDaUltimaInstalacao(int ano, bool conhecida)
    {
        var data = new DateTime(ano, 1, 1);
        Assert.Equal(conhecida ? data : (DateTime?)null, VerificacaoWindowsUpdate.NormalizarUltimaInstalacao(data));
        Assert.Null(VerificacaoWindowsUpdate.NormalizarUltimaInstalacao(null));
    }
    [Theory]
    [InlineData("2026-11-12T12:00:00")]
    [InlineData("2026-11-12T12:00:00Z")]
    [InlineData("2026-11-12T08:00:00-04:00")]
    public void PausaInterpretaUtcEConverteParaLocal(string texto)
    {
        var esperado = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc).ToLocalTime();
        Assert.Equal(esperado, VerificacaoWindowsUpdate.LerFimPausa(texto));
        Assert.Equal(DateTimeKind.Local, VerificacaoWindowsUpdate.LerFimPausa(texto)!.Value.Kind);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void QualquerPrazoFuturoPausaAtualizacoes(int indice)
    {
        var textos = new[] { "2026-11-10T12:00:00Z", "inválido", (string?)null };
        textos[indice] = "2026-11-12T12:00:00Z";
        var agora = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc).ToLocalTime();
        var resultado = VerificacaoWindowsUpdate.Avaliar(agora, VerificacaoWindowsUpdate.LerFimPausa(textos), agora);
        Assert.Equal(NivelVerificacao.Atencao, resultado.Nivel);
        Assert.Equal("Atualizações pausadas", resultado.Resumo);
        Assert.Null(VerificacaoWindowsUpdate.LerFimPausa(null, "inválido"));
    }
    [Fact]
    public void HistoricoIncluiSegurancaEmProtecao() => Assert.True(PaginaHistorico.SelecionaFiltro("protecao", "seguranca"));

    private sealed class Falsa(string nome, int peso, Func<CancellationToken, Task<ResultadoVerificacao>> ler) : IVerificacao
    {
        public string Nome => nome;
        public int Peso => peso;
        public Task<ResultadoVerificacao> Verificar(CancellationToken ct) => ler(ct);
    }
}
