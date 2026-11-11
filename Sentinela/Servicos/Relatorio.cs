using System.Net;
using System.Text;
using Sentinela.Servicos.Checkup;

namespace Sentinela.Servicos;

internal sealed record SecaoRelatorio(string Titulo, IReadOnlyList<string> Linhas);
internal sealed record RelatorioSemana(DateTime Inicio, DateTime Fim, string Resumo,
    int TotalEntradas, int Varreduras, long BytesLiberados, IReadOnlyList<SecaoRelatorio> Secoes);

internal static class Relatorio
{
    // Intervalos sempre incluem o início e excluem o fim.
    internal static (DateTime Inicio, DateTime Fim) Periodo(DateTime agora, bool semanaPassada)
    {
        if (!semanaPassada) return (agora.AddDays(-7), agora);
        var segunda = agora.Date.AddDays(-((6 + (int)agora.DayOfWeek) % 7));
        return (segunda.AddDays(-7), segunda);
    }

    internal static bool DeveAvisar(DateTime agora, DateTime? ultimoAvisado, bool avisosLigados, bool temEntradas)
    {
        var segunda = Periodo(agora, true).Fim;
        return avisosLigados && temEntradas && agora >= segunda.AddHours(9) && !(ultimoAvisado >= segunda);
    }

    internal static RelatorioSemana Montar(IEnumerable<EntradaDiario> entradas, DateTime inicio, DateTime fim,
        StatusDefender? status, ResultadoCheckup? desempenho, ResultadoCheckup? seguranca)
    {
        var lista = entradas.Where(e => e.Data >= inicio && e.Data < fim).OrderBy(e => e.Data).ToArray();
        var secoes = new List<SecaoRelatorio>();
        string Linha(EntradaDiario e) => $"{e.Data:dd/MM/yyyy HH:mm} — {e.Mensagem}";
        void Eventos(string titulo, params string[] tipos) => secoes.Add(new(titulo,
            lista.Where(e => tipos.Contains(e.Tipo)).Select(Linha).ToArray()));
        var varreduras = lista.Where(e => e.Tipo == "varredura").ToArray();
        var concluidas = varreduras.Where(VarreduraConcluida).ToArray();
        var linhasVarredura = new List<string> { $"{concluidas.Length} varredura(s) concluída(s).",
            status?.UltimaVarreduraCompleta is { } completa ? $"Última completa no Defender: {completa:dd/MM/yyyy HH:mm}" : "Última completa no Defender: não informada." };
        linhasVarredura.AddRange(concluidas.GroupBy(e => (e.TipoVarreduraExecutada == TipoVarredura.Rapida || e.Mensagem.StartsWith("Varredura rápida", StringComparison.OrdinalIgnoreCase)) ? "Rápida"
            : (e.TipoVarreduraExecutada == TipoVarredura.Completa || e.Mensagem.StartsWith("Varredura completa", StringComparison.OrdinalIgnoreCase)) ? "Completa" : "Personalizada ou tipo não informado")
            .Select(g => $"{g.Key}: {g.Count()}"));
        linhasVarredura.AddRange(varreduras.Select(Linha));
        secoes.Add(new("Varreduras (tipo e resultado)", linhasVarredura.ToArray()));
        Eventos("Ameaças, tratamentos e restaurações", "ameaca", "tratar", "restaurar");
        Eventos("Mudanças de inicialização e alertas do vigia", "inicializacao", "alerta");
        long bytes = 0;
        foreach (var entrada in lista.Where(e => e.Tipo == "limpeza" && e.Bytes > 0))
            bytes = (long)Math.Min(long.MaxValue, (decimal)bytes + entrada.Bytes!.Value);
        secoes.Add(new("Espaço liberado", new[] { Limpeza.Formatar(bytes) }
            .Concat(lista.Where(e => e.Tipo == "limpeza").Select(Linha)).ToArray()));
        var notas = new List<string>();
        void Nota(string nome, ResultadoCheckup? resultado)
        {
            notas.Add(resultado == null ? $"{nome}: ainda não verificado." : $"{nome}: {resultado.Nota}/100 em {resultado.Data:dd/MM/yyyy HH:mm}");
        }
        Nota("Desempenho", desempenho);
        Nota("Segurança", seguranca);
        if (seguranca != null) notas.AddRange(seguranca.Itens.Where(i => i.Nivel != NivelVerificacao.Ok)
            .Select(i => $"{(i.Nivel == NivelVerificacao.Atencao ? "Atenção" : "Problema")}: {i.Nome} — {i.Resumo}. {i.Detalhe}"));
        secoes.Add(new("Últimas notas e pontos de segurança", notas.ToArray()));
        Eventos("Verificações de desempenho e segurança", "checkup", "seguranca");
        Eventos("Atualizações e estado da proteção", "assinaturas", "status");
        Eventos("Ajustes, processos e varreduras offline", "ajuste", "processo", "offline");
        Eventos("Funcionamento do Sentinela", "inicio", "erro");
        string[] conhecidos = ["varredura", "ameaca", "tratar", "restaurar", "inicializacao", "alerta", "limpeza",
            "checkup", "seguranca", "assinaturas", "status", "ajuste", "processo", "offline", "inicio", "erro"];
        var outros = lista.Where(e => !conhecidos.Contains(e.Tipo)).ToArray();
        secoes.Add(new($"Outros eventos ({outros.Length})", outros.Select(e => $"{e.Tipo}: {Linha(e)}").ToArray()));
        var ameacas = lista.Count(e => e.Tipo == "ameaca");
        var alertas = lista.Count(e => e.Tipo == "alerta");
        var tratamentos = lista.Count(e => e.Tipo == "tratar");
        var restauracoes = lista.Count(e => e.Tipo == "restaurar");
        var falhasTratamento = lista.Count(e => (e.Tipo is "tratar" or "restaurar") && TratamentoFalhou(e.Mensagem));
        var resumo = falhasTratamento > 0 ? $"Atenção: {falhasTratamento} falha(s) de tratamento ou restauração. Confira o relatório."
            : ameacas > 0 ? $"Atenção: {ameacas} registro(s) de ameaça na semana. Confira os tratamentos no relatório."
            : tratamentos + restauracoes > 0 ? $"A semana teve {tratamentos} tratamento(s) e {restauracoes} restauração(ões). Confira o relatório."
            : status == null ? "Não consegui confirmar o estado atual da proteção. Confira o Defender."
            : !status.TudoLigado ? "Atenção: o estado atual do Defender tem proteção desligada."
            : alertas > 0 ? $"A semana teve {alertas} alerta(s). Confira o relatório."
            : seguranca?.Itens.Any(i => i.Nivel == NivelVerificacao.Problema) == true ? "Há problemas na última verificação de segurança. Confira o relatório."
            : seguranca?.Itens.Any(i => i.Nivel == NivelVerificacao.Atencao) == true ? "Há pontos de atenção na última verificação de segurança. Confira o relatório."
            : "Semana tranquila: nenhuma ameaça e proteção ligada no estado atual.";
        return new(inicio, fim, resumo, lista.Count(e => e.Tipo is not ("inicio" or "erro")), concluidas.Length, bytes, secoes.ToArray());
    }

    internal static bool VarreduraConcluida(EntradaDiario entrada)
    {
        if (entrada.ResultadoVarredura != null) return entrada.ResultadoVarredura == "concluida";
        // Compatibilidade: só o começo do resultado, nunca nomes/caminhos no detalhe.
        var mensagem = entrada.Mensagem;
        if (mensagem.StartsWith("Verificação de ", StringComparison.OrdinalIgnoreCase))
            return System.Text.RegularExpressions.Regex.IsMatch(mensagem, @"^Verificação de .+ terminada em ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (var prefixo in new[] { "Varredura rápida ", "Varredura completa ", "Varredura rápida: ", "Varredura completa: ", "Varredura " })
            if (mensagem.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)) { mensagem = mensagem[prefixo.Length..]; break; }
        return !new[] { "cancelad", "falh", "não rodou:", "não foi possível" }
            .Any(p => mensagem.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TratamentoFalhou(string mensagem) => new[] { "Não foi possível", "Falha", "Erro" }
        .Any(p => mensagem.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    internal static string NomeAmeaca(string detalhe)
    {
        var nome = detalhe.Split('·', 2)[0].Trim();
        foreach (var prefixo in new[] { "Ameaça detectada: ", "Falha ao tratar ameaça: " })
            if (nome.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)) nome = nome[prefixo.Length..];
        return nome;
    }

    internal static EntradaDiario[] ConciliarAmeacas(IEnumerable<EntradaDiario> diario,
        IEnumerable<EventoDefender> eventos, DateTime inicio, DateTime fim)
    {
        var lista = diario.Where(e => e.Data >= inicio && e.Data < fim).ToList();
        foreach (var evento in eventos.Where(e => e.Id == 1116 && e.Data >= inicio && e.Data < fim).OrderBy(e => e.Data))
        {
            var nome = NomeAmeaca(evento.Detalhe);
            if (string.IsNullOrWhiteSpace(nome)) continue;
            if (lista.Any(e => e.Tipo == "ameaca"
                && string.Equals(e.NomeAmeaca ?? NomeAmeaca(e.Mensagem), nome, StringComparison.OrdinalIgnoreCase)
                && (e.Data - evento.Data).Duration() <= TimeSpan.FromMinutes(2))) continue;
            lista.Add(new(evento.Data, "ameaca", evento.Titulo + ": " + evento.Detalhe, NomeAmeaca: nome));
        }
        return lista.ToArray();
    }

    internal static string TextoPeriodo(RelatorioSemana r) => r.Inicio.TimeOfDay == TimeSpan.Zero && r.Fim.TimeOfDay == TimeSpan.Zero
        ? $"Período: {r.Inicio:dd/MM/yyyy} a {r.Fim.AddTicks(-1):dd/MM/yyyy}"
        : $"Período: {r.Inicio:dd/MM/yyyy HH:mm} a {r.Fim:dd/MM/yyyy HH:mm}";

    internal static string Texto(RelatorioSemana r) =>
        TextoPeriodo(r) + "\n\n" +
        string.Join("\n\n", r.Secoes.Select(s => s.Titulo + "\n" + (s.Linhas.Count == 0 ? "Nenhum registro no período." : string.Join("\n", s.Linhas))));

    internal static string GerarHtml(RelatorioSemana r)
    {
        static string E(string texto) => System.Text.RegularExpressions.Regex.Replace(WebUtility.HtmlEncode(texto),
            "http", m => "&#" + (int)m.Value[0] + ";" + m.Value[1..], System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Replace(":", "&#58;", StringComparison.Ordinal).Replace("=", "&#61;", StringComparison.Ordinal);
        var html = new StringBuilder("<!doctype html><html lang=\"pt-BR\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>Relatório do Sentinela</title><style>body{font:16px sans-serif;max-width:960px;margin:32px auto;padding:0 20px;line-height:1.5}li{overflow-wrap:anywhere;margin-bottom:8px}section{margin-top:28px}h1,h2{line-height:1.2}@media print{body{margin:0;max-width:none;font-size:11pt}h2{break-after:avoid}li{break-inside:avoid}}</style><body><h1>Relatório do Sentinela</h1>");
        html.Append("<p>").Append(E(TextoPeriodo(r))).Append("</p><p>").Append(E(r.Resumo)).Append("</p>");
        foreach (var secao in r.Secoes)
        {
            html.Append("<section><h2>").Append(E(secao.Titulo)).Append("</h2><ul>");
            foreach (var linha in secao.Linhas.DefaultIfEmpty("Nenhum registro no período."))
                html.Append("<li>").Append(E(linha)).Append("</li>");
            html.Append("</ul></section>");
        }
        return html.Append("</body></html>").ToString();
    }

    internal static RelatorioSemana Ler(DateTime agora, bool semanaPassada)
    {
        StatusDefender? status = null;
        try { status = Defender.LerStatus(); } catch { }
        var periodo = Periodo(agora, semanaPassada);
        return Montar(ConciliarAmeacas(Diario.Ler(30), VigiaDefender.LerRecentes(30), periodo.Inicio, periodo.Fim), periodo.Inicio, periodo.Fim, status,
            Checkup.Checkup.Ultimo, Seguranca.Seguranca.Ultimo);
    }
}
