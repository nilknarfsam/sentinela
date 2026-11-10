using System.Text.Json;

namespace Sentinela.Servicos;

public sealed class Configuracoes
{
    public bool FecharParaBandeja { get; set; } = true;
    public bool AvisosDeRotina { get; set; } = true;
    public DayOfWeek DiaVarreduraSemanal { get; set; } = DayOfWeek.Sunday;
    public int HoraVarreduraSemanal { get; set; } = 12;

    public static Configuracoes Atual { get; } = Carregar();

    private static string Caminho => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sentinela", "config.json");

    private static Configuracoes Carregar()
    {
        try
        {
            var configuracoes = JsonSerializer.Deserialize<Configuracoes>(ArquivosSeguros.LerTexto(Caminho, 64 * 1024) ?? "");
            if (configuracoes != null && Enum.IsDefined(configuracoes.DiaVarreduraSemanal)
                && configuracoes.HoraVarreduraSemanal is >= 0 and <= 23)
                return configuracoes;
        }
        catch { /* Arquivo ausente, inválido ou indisponível: usa os padrões. */ }
        return new Configuracoes();
    }

    public void Salvar()
    {
        if (App.CapturaArquivo != null) return;
        try
        {
            var caminho = Caminho;
            ArquivosSeguros.GravarTexto(caminho, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* As configurações não podem interromper o aplicativo. */ }
    }
}
