using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Xml.Linq;

namespace Sentinela.Servicos;

public enum TipoEvento { Ameaca, AmeacaTratada, Varredura, Assinaturas, Protecao, Configuracao, Outro }

public sealed record EventoDefender(DateTime Data, int Id, TipoEvento Tipo, string Titulo, string Detalhe);

public sealed class VigiaDefender : IDisposable
{
    private const string Log = "Microsoft-Windows-Windows Defender/Operational";
    private EventLogWatcher? _vigia;
    private bool _descartado;
    public event Action<EventoDefender>? Chegou;
    public string? Erro { get; private set; }

    public void Iniciar()
    {
        if (_descartado || _vigia != null) return;
        try
        {
            Erro = null;
            _vigia = new EventLogWatcher(new EventLogQuery(Log, PathType.LogName));
            _vigia.EventRecordWritten += Receber;
            _vigia.Enabled = true;
        }
        catch (Exception ex)
        {
            Erro = ex.Message;
            Liberar();
        }
    }

    private void Receber(object? remetente, EventRecordWrittenEventArgs args)
    {
        using var registro = args.EventRecord;
        try
        {
            if (args.EventException != null) { Erro = args.EventException.Message; return; }
            if (registro != null && Ler(registro) is { } evento) Chegou?.Invoke(evento);
        }
        catch (Exception ex) { Erro = ex.Message; }
    }

    private static EventoDefender? Ler(EventRecord registro)
    {
        var xml = XDocument.Parse(registro.ToXml());
        XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
        var campos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var campo in xml.Descendants(ns + "EventData").Elements(ns + "Data"))
            if (campo.Attribute("Name") is { } nome) campos[nome.Value] = campo.Value;
        return Interpretar(registro.Id, registro.TimeCreated ?? DateTime.Now, campos);
    }

    public static List<EventoDefender> LerRecentes(int dias, int maximo = 500)
    {
        var lista = new List<EventoDefender>();
        try
        {
            if (dias <= 0 || maximo <= 0) return [];
            var milissegundos = (long)TimeSpan.FromDays(dias).TotalMilliseconds;
            var periodo = $"TimeCreated[timediff(@SystemTime) <= {milissegundos.ToString(CultureInfo.InvariantCulture)}]";
            List<EventoDefender> Consultar(string xpath)
            {
                var consulta = new EventLogQuery(Log, PathType.LogName, xpath) { ReverseDirection = true };
                using var leitor = new EventLogReader(consulta);
                var eventos = new List<EventoDefender>();
                for (var lidos = 0; lidos < 5000 && eventos.Count < maximo; lidos++)
                {
                    using var registro = leitor.ReadEvent();
                    if (registro == null) break;
                    if (Ler(registro) is { } evento) eventos.Add(evento);
                }
                return eventos;
            }

            lista.AddRange(Consultar($"*[System[(EventID=1000 or EventID=1001 or EventID=1002 or EventID=1005 or EventID=1116 or EventID=1117 or EventID=1118 or EventID=1119 or EventID=2000 or EventID=2001 or EventID=5000 or EventID=5001 or EventID=5004 or EventID=5010 or EventID=5012) and {periodo}]]"));
            // O 5007 é muito frequente: tem consulta e teto próprios, para não
            // empurrar eventos raros para fora da lista. O XPath do Log de Eventos
            // não aceita contains(), então o filtro por conteúdo fica no Interpretar.
            lista.AddRange(Consultar($"*[System[EventID=5007 and {periodo}]]"));
        }
        catch { /* O histórico disponível continua utilizável se uma consulta falhar. */ }
        return lista.OrderByDescending(e => e.Data).Take(maximo).ToList();
    }

    internal static EventoDefender? Interpretar(int id, DateTime data, IReadOnlyDictionary<string, string> campos)
    {
        string Campo(string nome) => campos.TryGetValue(nome, out var valor) ? valor ?? "" : "";
        string Juntar(params string[] valores) => string.Join(" · ", valores.Where(v => !string.IsNullOrWhiteSpace(v)));
        EventoDefender Evento(TipoEvento tipo, string titulo, string detalhe = "") => new(data, id, tipo, titulo, detalhe);
        switch (id)
        {
            case 1116: return Evento(TipoEvento.Ameaca, "Ameaça detectada", Juntar(Campo("Threat Name"), Campo("Path")));
            case 1117: return Evento(TipoEvento.AmeacaTratada, "Ameaça tratada", Juntar(Campo("Threat Name"), Campo("Action Name")));
            case 1118 or 1119: return Evento(TipoEvento.Ameaca, "Falha ao tratar ameaça", Juntar(Campo("Threat Name"), Campo("Path")));
            case 1000: return Evento(TipoEvento.Varredura, "Varredura iniciada", Campo("Scan Parameters"));
            case 1001:
                double Numero(string nome) => double.TryParse(Campo(nome), NumberStyles.Number, CultureInfo.InvariantCulture, out var n)
                    && double.IsFinite(n) && n >= 0 ? Math.Min(n, 1000000) : 0;
                var duracao = TimeSpan.FromHours(Numero("Scan Time Hours")) + TimeSpan.FromMinutes(Numero("Scan Time Minutes"))
                    + TimeSpan.FromSeconds(Numero("Scan Time Seconds"));
                return Evento(TipoEvento.Varredura, "Varredura terminada", Juntar(Campo("Scan Parameters"), Formatos.Duracao(duracao)));
            case 1002: return Evento(TipoEvento.Varredura, "Varredura cancelada", Campo("Scan Parameters"));
            case 1005: return Evento(TipoEvento.Varredura, "Varredura falhou", Campo("Scan Parameters"));
            case 2000: return Evento(TipoEvento.Assinaturas, "Assinaturas atualizadas");
            case 2001: return Evento(TipoEvento.Assinaturas, "Falha ao atualizar assinaturas");
            case 5001: return Evento(TipoEvento.Protecao, "Proteção em tempo real DESLIGADA");
            case 5000: return Evento(TipoEvento.Protecao, "Proteção em tempo real ligada");
            case 5010 or 5012: return Evento(TipoEvento.Protecao, "Proteção contra spyware/vírus desligada");
            case 5004: return Evento(TipoEvento.Protecao, "Configuração da proteção em tempo real mudou");
            case 5007:
                var antigo = Campo("Old Value");
                var novo = Campo("New Value");
                if (string.Equals(antigo, novo, StringComparison.OrdinalIgnoreCase)) return null;
                string[] relevantes = [@"\Exclusions\", "DisableRealtimeMonitoring", "DisableAntiSpyware", "DisableAntiVirus",
                    "DisableBehaviorMonitoring", "DisableIOAVProtection", "TamperProtection"];
                if (!relevantes.Any(c => antigo.Contains(c, StringComparison.OrdinalIgnoreCase) || novo.Contains(c, StringComparison.OrdinalIgnoreCase))) return null;
                var exclusao = antigo.Contains(@"\Exclusions\", StringComparison.OrdinalIgnoreCase)
                    || novo.Contains(@"\Exclusions\", StringComparison.OrdinalIgnoreCase);
                if (!exclusao)
                {
                    var adulteracao = antigo.Contains("TamperProtection", StringComparison.OrdinalIgnoreCase)
                        || novo.Contains("TamperProtection", StringComparison.OrdinalIgnoreCase);
                    if (adulteracao
                        ? !novo.EndsWith("0x0", StringComparison.OrdinalIgnoreCase) && !novo.EndsWith("0x4", StringComparison.OrdinalIgnoreCase)
                        : !novo.EndsWith("0x1", StringComparison.OrdinalIgnoreCase)) return null;
                }
                var detalhe = Juntar(antigo, novo);
                var valor = string.IsNullOrWhiteSpace(novo) ? antigo : novo;
                foreach (var grupo in new[] { "Paths", "Processes", "Extensions" })
                {
                    var prefixo = @"\Exclusions\" + grupo + @"\";
                    var inicio = valor.IndexOf(prefixo, StringComparison.OrdinalIgnoreCase);
                    if (inicio < 0) continue;
                    var caminho = valor[(inicio + prefixo.Length)..];
                    var fim = caminho.LastIndexOf(" = ", StringComparison.Ordinal);
                    if (fim >= 0) caminho = caminho[..fim];
                    if (string.IsNullOrWhiteSpace(antigo)) detalhe = "Exclusão adicionada: " + caminho;
                    else if (string.IsNullOrWhiteSpace(novo)) detalhe = "Exclusão removida: " + caminho;
                    break;
                }
                return Evento(TipoEvento.Configuracao, "Configuração do Defender mudou", detalhe);
            default: return null;
        }
    }

    private void Liberar()
    {
        var vigia = _vigia;
        _vigia = null;
        if (vigia == null) return;
        vigia.EventRecordWritten -= Receber;
        try { vigia.Dispose(); } catch { /* A saída e a falha de assinatura não podem interromper o app. */ }
    }

    public void Dispose()
    {
        _descartado = true;
        Liberar();
    }
}
