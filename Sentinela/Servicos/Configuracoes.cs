using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sentinela.Servicos;

public sealed class Configuracoes
{
    public sealed record PastaProjeto(string Caminho, string? Motivo)
    {
        public override string ToString() => Motivo == null ? Caminho : $"{Caminho} — Ignorada: {Motivo}";
    }

    [JsonIgnore]
    public List<PastaProjeto> PastasIgnoradas { get; } = [];

    internal static bool ValidarPastaProjeto(string? pasta, out string normalizada, out string motivo)
    {
        normalizada = "";
        motivo = "Caminho inválido";
        try
        {
            if (string.IsNullOrWhiteSpace(pasta) || !Path.IsPathFullyQualified(pasta)) { motivo = "Use um caminho totalmente qualificado"; return false; }
            if (ArvoreDescartavel.PastaProibidaParaApagar(pasta)) { motivo = "Pasta protegida"; return false; }
            normalizada = ArvoreDescartavel.Normalizar(pasta);
            if (!Directory.Exists(normalizada)) { motivo = "Pasta inexistente ou inacessível"; return false; }
            return true;
        }
        catch { return false; }
    }

    private readonly object _travaPastas = new();

    public string[] PastasProjetosValidas()
    {
        lock (_travaPastas)
        {
            var validas = new List<string>();
            PastasIgnoradas.Clear();
            foreach (var pasta in PastasProjetos)
            {
                if (ValidarPastaProjeto(pasta, out var normalizada, out var motivo)) validas.Add(normalizada);
                else if (!PastasIgnoradas.Any(p => p.Caminho == pasta)) PastasIgnoradas.Add(new(pasta, motivo));
            }
            return validas.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    public PastaProjeto[] ListarPastasProjetos()
    {
        lock (_travaPastas)
            return PastasProjetosValidas().Select(p => new PastaProjeto(p, null)).Concat(PastasIgnoradas).ToArray();
    }

    public void AdicionarPastaProjeto(string pasta)
    {
        lock (_travaPastas)
            if (!PastasProjetos.Contains(pasta, StringComparer.OrdinalIgnoreCase)) PastasProjetos.Add(pasta);
    }

    public void RemoverPastaProjeto(PastaProjeto pasta)
    {
        lock (_travaPastas)
        {
            PastasProjetos.Remove(pasta.Caminho);
            PastasIgnoradas.Remove(pasta);
        }
    }

    public List<string> PastasProjetos { get; set; } = Directory.Exists(@"C:\PROJETOS") ? [@"C:\PROJETOS"] : [];
    public bool FecharParaBandeja { get; set; } = true;
    public DateTime? UltimoLembreteReinicio { get; set; }
    public bool AvisosDeRotina { get; set; } = true;
    public DayOfWeek DiaVarreduraSemanal { get; set; } = DayOfWeek.Sunday;
    public int HoraVarreduraSemanal { get; set; } = 12;
    public bool VarreduraSemanalLigada { get; set; } = true;
    public DateTime? UltimaTentativaVarreduraSemanal { get; set; }
    public DateTime? UltimoRelatorioAvisado { get; set; }

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
            {
                configuracoes.PastasProjetos ??= [];
                configuracoes.PastasProjetosValidas();
                return configuracoes;
            }
        }
        catch { /* Arquivo ausente, inválido ou indisponível: usa os padrões. */ }
        return new Configuracoes();
    }

    // Uma única gravação por vez, incluindo a serialização.
    private static readonly object TravaGravacao = new();

    internal bool PrepararRotinaSemanal(DateTime agora)
    {
        if (UltimaTentativaVarreduraSemanal != null) return false;
        UltimaTentativaVarreduraSemanal = agora;
        return true;
    }

    internal void AlterarRotinaSemanal(bool ligada, DayOfWeek dia, int hora, DateTime agora)
    {
        if ((!VarreduraSemanalLigada && ligada) || dia != DiaVarreduraSemanal || hora != HoraVarreduraSemanal)
            UltimaTentativaVarreduraSemanal = agora;
        VarreduraSemanalLigada = ligada;
        DiaVarreduraSemanal = dia;
        HoraVarreduraSemanal = hora;
    }

    internal bool MarcarRelatorioAvisado(DateTime semana, bool temBandeja, Func<bool> salvar)
    {
        if (!temBandeja) return false;
        var anterior = UltimoRelatorioAvisado;
        UltimoRelatorioAvisado = semana;
        if (salvar()) return true;
        UltimoRelatorioAvisado = anterior;
        return false;
    }

    public void Salvar() => TentarSalvar();

    internal bool TentarSalvar()
    {
        lock (TravaGravacao)
        lock (_travaPastas)
        {
            if (App.CapturaArquivo != null) return false;
            try
            {
                var caminho = Caminho;
                return ArquivosSeguros.GravarTexto(caminho, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { return false; /* As configurações não podem interromper o aplicativo. */ }
        }
    }
}
