using Sentinela.Servicos.Checkup;

namespace Sentinela.Apresentacao;

/// <summary>Resume a confiança do resultado sem mudar os resultados nem a nota do checkup.</summary>
internal static class EstadoApresentacao
{
    internal enum Estado { Completo, Parcial, Indisponivel }

    private static readonly string[] MarcadoresIndisponiveis =
    [
        "não consegui", "não foi possível", "indisponível", "indisponivel",
        "abra como administrador", "requer administrador", "precisa de administrador",
        "precisam de administrador", "precisa de permissão", "precisa de permissao",
        "leitura parcial", "medição parcial", "medicao parcial", "não foi possível ler",
        "não lido", "nao lido", "não disponível", "nao disponivel", "puderam ser lidas",
        "puderam ser lidos", "ilegível", "ilegivel"
    ];

    internal static Estado Avaliar(ResultadoCheckup? resultado)
    {
        if (resultado == null || resultado.Itens.Count == 0) return Estado.Indisponivel;
        // O formato salvo atual omite detalhes, então não permite confirmar que um resultado antigo era completo.
        if (resultado.Itens.Any(i => i.Detalhe == "Resultado da última verificação salva.")) return Estado.Parcial;
        var indisponiveis = resultado.Itens.Count(TemIndisponibilidade);
        if (indisponiveis == resultado.Itens.Count) return Estado.Indisponivel;
        return indisponiveis > 0 ? Estado.Parcial : Estado.Completo;
    }

    internal static bool TemIndisponibilidade(ResultadoVerificacao item)
    {
        var texto = $"{item.Resumo} {item.Detalhe}";
        return MarcadoresIndisponiveis.Any(m => texto.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    internal static string Mensagem(ResultadoCheckup? resultado, string assunto)
    {
        if (DadosSalvosSemDetalhes(resultado))
            return $"{(TemProblemaConhecido(resultado) ? "Há problemas registrados neste resultado salvo. " : "")}Detalhes limitados; a nota não confirma {assunto}.";
        return Avaliar(resultado) switch
        {
            Estado.Indisponivel => $"Não foi possível confirmar {assunto}: as verificações não ficaram disponíveis.",
            Estado.Parcial when TemProblemaConhecido(resultado) => $"Há problemas identificados, mas o resultado é parcial. A nota não confirma {assunto}.",
            Estado.Parcial => $"Resultado parcial: há verificações indisponíveis. A nota não confirma {assunto}.",
            _ => string.Empty
        };
    }

    internal static bool TemProblemaConhecido(ResultadoCheckup? resultado)
        => resultado != null && (resultado.Nota < 60 || resultado.Itens.Any(i => i.Nivel == NivelVerificacao.Problema));

    internal static string Cor(ResultadoCheckup? resultado, string corDaNota)
        => TemProblemaConhecido(resultado) ? "Vermelho" :
            Avaliar(resultado) == Estado.Completo ? corDaNota : "Ambar";

    internal static string Rotulo(ResultadoCheckup resultado)
    {
        if (DadosSalvosSemDetalhes(resultado)) return "resultado salvo · detalhes limitados";
        if (Avaliar(resultado) == Estado.Parcial) return "resultado parcial";
        if (Avaliar(resultado) == Estado.Indisponivel)
            return "resultado não confirmado";
        return string.Empty;
    }

    internal static string Titulo(ResultadoCheckup resultado, string tituloCompleto)
    {
        var rotulo = Rotulo(resultado);
        if (string.IsNullOrEmpty(rotulo)) return tituloCompleto;
        if (TemProblemaConhecido(resultado)) return "Há problemas identificados · " + rotulo;
        return rotulo switch
        {
            "resultado salvo · detalhes limitados" => "Resultado salvo · detalhes limitados",
            "resultado não confirmado" => "Resultado não confirmado",
            _ => "Resultado parcial"
        };
    }

    internal static bool DadosSalvosSemDetalhes(ResultadoCheckup? resultado)
        => resultado?.Itens.Any(i => i.Detalhe == "Resultado da última verificação salva.") == true;
}
