using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Sentinela.Servicos.Checkup;
using Motor = Sentinela.Servicos.Checkup.Checkup;

namespace Sentinela.Servicos.Seguranca;

internal sealed record PreferenciasDefender(int? Pua, int? Pastas, string[]? Exclusoes, bool ExclusoesIgnoradas = false);
public sealed record ContaWindows(string Sid, string Nome, bool Desativada, bool? SenhaObrigatoria);

internal static class Leituras
{
    internal static object? Registro(string chave, string valor)
    {
        try { using var aberta = Registry.LocalMachine.OpenSubKey(chave); return aberta?.GetValue(valor); }
        catch { return null; }
    }
    internal static int? Numero(object? valor)
    {
        try { return valor == null ? null : Convert.ToInt32(valor, CultureInfo.InvariantCulture); }
        catch { return null; }
    }
    internal static bool? Ligado(object? valor) => Numero(valor) switch { 1 => true, 0 => false, _ => null };
    internal static bool SemAdministrador(string texto) => texto.Contains("Must be an administrator", StringComparison.OrdinalIgnoreCase)
        || texto.TrimStart().StartsWith("N/A", StringComparison.OrdinalIgnoreCase)
        || texto.Contains(": N/A", StringComparison.OrdinalIgnoreCase);
    internal static bool TemQuebraDeLinha(string texto) => texto.Contains('\r') || texto.Contains('\n');
    internal static bool ExclusaoValida(string? texto) => !string.IsNullOrWhiteSpace(texto)
        && !SemAdministrador(texto) && !TemQuebraDeLinha(texto);
    internal static T? Consultar<T>(string escopo, string consulta, Func<ManagementObjectCollection, T> ler) where T : class
    {
        try
        {
            using var busca = new ManagementObjectSearcher(escopo, consulta);
            busca.Options.Timeout = TimeSpan.FromSeconds(5);
            using var itens = busca.Get();
            return ler(itens);
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }
    internal static PreferenciasDefender? LerPreferencias(CancellationToken ct = default) => Consultar(@"root\Microsoft\Windows\Defender",
        "SELECT PUAProtection, EnableControlledFolderAccess, ExclusionPath, ExclusionProcess, ExclusionExtension FROM MSFT_MpPreference", itens =>
        {
            foreach (ManagementObject item in itens)
            {
                using (item)
                {
                    ct.ThrowIfCancellationRequested();
                    var exclusoes = new List<string>();
                    var ignoradas = false;
                    foreach (var (campo, tipo) in new[] { ("ExclusionPath", "Pasta"), ("ExclusionProcess", "Processo"), ("ExclusionExtension", "Extensão") })
                    {
                        var valor = item[campo];
                        var valores = valor is string[] lista ? lista : valor is string texto ? new[] { texto } : Array.Empty<string>();
                        if (valores.Any(SemAdministrador)) return new PreferenciasDefender(Numero(item["PUAProtection"]), Numero(item["EnableControlledFolderAccess"]), null);
                        ignoradas |= valores.Any(TemQuebraDeLinha);
                        exclusoes.AddRange(valores.Where(ExclusaoValida).Select(v => $"{tipo}: {v}"));
                    }
                    return new PreferenciasDefender(Numero(item["PUAProtection"]), Numero(item["EnableControlledFolderAccess"]), exclusoes.ToArray(), ignoradas);
                }
            }
            return null!;
        });
    internal static ResultadoVerificacao Resultado(string nome, string icone, NivelVerificacao nivel, string resumo, string detalhe, string alvo)
        => new(nome, icone, nivel, resumo, detalhe, new("Como resolver", "abrir", alvo));
}

public sealed class VerificacaoFirewall : Verificacao
{
    public override string Nome => "Firewall";
    public override int Peso => 3;
    protected override Task<ResultadoVerificacao> Ler(CancellationToken ct)
    {
        bool? dominio = null, particular = null, publico = null;
        object? politica = null;
        try
        {
            politica = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2", true)!);
            dynamic efetiva = politica!;
            dominio = (bool)efetiva.FirewallEnabled[1];
            particular = (bool)efetiva.FirewallEnabled[2];
            publico = (bool)efetiva.FirewallEnabled[4];
        }
        catch
        {
            const string raiz = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\";
            dominio = Leituras.Ligado(Leituras.Registro(raiz + "DomainProfile", "EnableFirewall"));
            particular = Leituras.Ligado(Leituras.Registro(raiz + "StandardProfile", "EnableFirewall"));
            publico = Leituras.Ligado(Leituras.Registro(raiz + "PublicProfile", "EnableFirewall"));
        }
        finally { if (politica != null && Marshal.IsComObject(politica)) Marshal.FinalReleaseComObject(politica); }
        return Task.FromResult(Avaliar(dominio, particular, publico));
    }
    internal static ResultadoVerificacao Avaliar(bool? dominio, bool? particular, bool? publico)
    {
        var perfis = new[] { ("Domínio", dominio), ("Particular", particular), ("Pública", publico) };
        var desligados = perfis.Where(p => p.Item2 == false).Select(p => p.Item1).ToArray();
        var incompleto = perfis.Any(p => p.Item2 == null);
        return Leituras.Resultado("Firewall", "\uE968", desligados.Length > 0 ? NivelVerificacao.Problema : incompleto ? NivelVerificacao.Atencao : NivelVerificacao.Ok,
            desligados.Length > 0 ? "Firewall desligado na rede " + string.Join(", ", desligados) : incompleto ? "Não consegui ler todos os perfis" : "Firewall ligado nos três perfis",
            string.Join("\n", perfis.Select(p => $"{p.Item1}: {(p.Item2 == true ? "ligado" : p.Item2 == false ? "desligado" : "não lido")}")), "windowsdefender://network");
    }
}

public sealed class VerificacaoWindowsUpdate : Verificacao
{
    public override string Nome => "Windows Update";
    public override int Peso => 3;
    protected override Task<ResultadoVerificacao> Ler(CancellationToken ct)
    {
        DateTime? ultima = null;
        object? atualizador = null, resultados = null;
        try
        {
            atualizador = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.AutoUpdate", true)!);
            resultados = ((dynamic)atualizador!).Results;
            DateTime data = ((dynamic)resultados!).LastInstallationSuccessDate;
            ultima = NormalizarUltimaInstalacao(data);
        }
        catch { /* A pausa ainda pode ser lida quando a API não responde. */ }
        finally
        {
            if (resultados != null && Marshal.IsComObject(resultados)) Marshal.FinalReleaseComObject(resultados);
            if (atualizador != null && Marshal.IsComObject(atualizador)) Marshal.FinalReleaseComObject(atualizador);
        }
        const string chave = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
        var pausa = LerFimPausa(new[] { "PauseUpdatesExpiryTime", "PauseFeatureUpdatesEndTime", "PauseQualityUpdatesEndTime" }
            .Select(nome => Leituras.Registro(chave, nome)?.ToString()).ToArray());
        return Task.FromResult(Avaliar(ultima, pausa, DateTime.Now));
    }
    internal static DateTime? NormalizarUltimaInstalacao(DateTime? data) => data?.Year >= 2000 ? data : null;
    internal static DateTime? LerFimPausa(params string?[] textos) => textos.Select(texto =>
        DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var fim)
            ? (DateTime?)fim.ToLocalTime() : null).Max();
    internal static ResultadoVerificacao Avaliar(DateTime? ultima, DateTime? pausaAte, DateTime agora)
    {
        var dias = ultima.HasValue ? (agora - ultima.Value).TotalDays : (double?)null;
        var pausado = pausaAte > agora;
        var nivel = dias > 60 ? NivelVerificacao.Problema : pausado || dias == null || dias > 30 ? NivelVerificacao.Atencao : NivelVerificacao.Ok;
        return Leituras.Resultado("Windows Update", "\uE895", nivel,
            pausado ? "Atualizações pausadas" : dias == null ? "Não consegui ler a data" : $"Última instalação há {Math.Max(0, (int)dias.Value)} dias",
            (ultima.HasValue ? $"Última instalação bem-sucedida: {ultima:dd/MM/yyyy HH:mm}." : "O Windows não informou a última instalação.")
                + (pausado ? $" Pausadas até {pausaAte:dd/MM/yyyy HH:mm}." : " Confira as atualizações no Windows."), "ms-settings:windowsupdate");
    }
}

public sealed class VerificacaoUac : Verificacao
{
    public override string Nome => "Controle de Conta de Usuário (UAC)";
    public override int Peso => 3;
    protected override Task<ResultadoVerificacao> Ler(CancellationToken ct)
    {
        const string chave = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
        return Task.FromResult(Avaliar(Leituras.Numero(Leituras.Registro(chave, "EnableLUA")), Leituras.Numero(Leituras.Registro(chave, "ConsentPromptBehaviorAdmin"))));
    }
    internal static ResultadoVerificacao Avaliar(int? habilitado, int? consentimento) => Leituras.Resultado("Controle de Conta de Usuário (UAC)", "\uE7EF",
        habilitado == 0 ? NivelVerificacao.Problema : habilitado is not (null or 1) || consentimento == 0 ? NivelVerificacao.Atencao : NivelVerificacao.Ok,
        habilitado == 0 ? "UAC desligado" : habilitado is not (null or 1) ? "Não consegui ler a configuração do UAC" : consentimento == 0 ? "Elevação sem perguntar" : "UAC ligado e pedindo confirmação",
        "O UAC pede sua confirmação antes de um programa fazer mudanças como administrador.", "uac");
}

public sealed class VerificacaoSmartScreen : Verificacao
{
    private readonly Lazy<Task<PreferenciasDefender?>>? _preferencias;
    public VerificacaoSmartScreen() { }
    internal VerificacaoSmartScreen(Lazy<Task<PreferenciasDefender?>> preferencias) => _preferencias = preferencias;
    public override string Nome => "SmartScreen e apps indesejados";
    public override int Peso => 2;
    protected override async Task<ResultadoVerificacao> Ler(CancellationToken ct)
    {
        var preferencias = await (_preferencias?.Value ?? Task.Run(() => Leituras.LerPreferencias(ct))).WaitAsync(ct).ConfigureAwait(false);
        return Avaliar(Leituras.Numero(Leituras.Registro(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableSmartScreen")),
            Leituras.Registro(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled")?.ToString(), preferencias?.Pua);
    }
    internal static ResultadoVerificacao Avaliar(int? politica, string? smartScreen, int? pua)
    {
        var desligado = politica == 0 || string.Equals(smartScreen, "Off", StringComparison.OrdinalIgnoreCase);
        return Leituras.Resultado("SmartScreen e apps indesejados", "\uE8A1", desligado ? NivelVerificacao.Problema : pua != 1 ? NivelVerificacao.Atencao : NivelVerificacao.Ok,
            desligado ? "SmartScreen desligado" : pua == 0 ? "Bloqueio de apps indesejados desligado" : pua == 2 ? "Apps indesejados: só auditoria" : pua == 1 ? "SmartScreen e bloqueio de apps ligados" : "Não consegui ler o bloqueio de apps indesejados",
            "O SmartScreen verifica a reputação dos apps. O bloqueio de apps indesejados evita programas inconvenientes.", "windowsdefender://appbrowser");
    }
}

public sealed class VerificacaoRansomware : Verificacao
{
    private readonly Lazy<Task<PreferenciasDefender?>>? _preferencias;
    public VerificacaoRansomware() { }
    internal VerificacaoRansomware(Lazy<Task<PreferenciasDefender?>> preferencias) => _preferencias = preferencias;
    public override string Nome => "Proteção contra ransomware";
    public override int Peso => 1;
    protected override async Task<ResultadoVerificacao> Ler(CancellationToken ct) => Avaliar((await (_preferencias?.Value ?? Task.Run(() => Leituras.LerPreferencias(ct))).WaitAsync(ct).ConfigureAwait(false))?.Pastas);
    internal static ResultadoVerificacao Avaliar(int? estado) => Leituras.Resultado("Proteção contra ransomware", "\uE8B7",
        estado == 1 ? NivelVerificacao.Ok : NivelVerificacao.Atencao,
        estado == 1 ? "Acesso controlado a pastas ligado" : estado == 0 ? "Acesso controlado a pastas desligado" : estado == 2 ? "Acesso controlado a pastas: só auditoria" : "Não consegui ler a proteção contra ransomware",
        "Opcional. Protege Documentos e Imagens, mas pode bloquear programas legítimos; ligue se aceitar liberar programas um a um.", "windowsdefender://ransomwareprotection");
}

public sealed class VerificacaoDispositivo : Verificacao
{
    public override string Nome => "Inicialização segura, TPM e BitLocker";
    public override int Peso => 2;
    protected override async Task<ResultadoVerificacao> Ler(CancellationToken ct)
    {
        const string chave = @"SYSTEM\CurrentControlSet\Control\SecureBoot\State";
        bool chaveAusente;
        using (var estado = Registry.LocalMachine.OpenSubKey(chave)) chaveAusente = estado == null;
        var seguro = Leituras.Ligado(Leituras.Registro(chave, "UEFISecureBootEnabled"));
        // Esses dois provedores exigem elevação; sem ela podem esperar até o limite do WMI.
        // A falta de permissão é informativa e não reduz a nota.
        var admin = Sistema.EhAdmin();
        if (!admin) return Avaliar(seguro, null, null, false, chaveAusente);
        var tpm = Task.Run(() => Leituras.Consultar(@"root\CIMV2\Security\MicrosoftTpm", "SELECT IsEnabled_InitialValue, IsActivated_InitialValue FROM Win32_Tpm", itens =>
        {
            foreach (ManagementObject item in itens)
                using (item)
                {
                    ct.ThrowIfCancellationRequested();
                    if (item["IsEnabled_InitialValue"] == null || item["IsActivated_InitialValue"] == null) throw new InvalidDataException();
                    return new[] { Convert.ToBoolean(item["IsEnabled_InitialValue"]), Convert.ToBoolean(item["IsActivated_InitialValue"]) };
                }
            return Array.Empty<bool>();
        }));
        var bitlocker = Task.Run(() => Leituras.Consultar(@"root\CIMV2\Security\MicrosoftVolumeEncryption", "SELECT ProtectionStatus FROM Win32_EncryptableVolume WHERE DriveLetter='C:'", itens =>
        {
            foreach (ManagementObject item in itens)
                using (item)
                {
                    ct.ThrowIfCancellationRequested();
                    return new int?[] { Leituras.Numero(item["ProtectionStatus"]) };
                }
            return Array.Empty<int?>();
        }));
        var dadosTpm = await tpm.WaitAsync(ct).ConfigureAwait(false);
        var dadosBitlocker = await bitlocker.WaitAsync(ct).ConfigureAwait(false);
        return Avaliar(seguro, dadosTpm == null ? null : dadosTpm.Length > 0 && dadosTpm.All(v => v),
            dadosBitlocker?.FirstOrDefault() is int protecao ? protecao == 1 : null, admin, chaveAusente, dadosTpm?.Length > 0);
    }
    internal static ResultadoVerificacao Avaliar(bool? seguro, bool? tpm, bool? bitlocker, bool admin, bool chaveSecureBootAusente = false, bool tpmPresente = false)
    {
        var faltam = seguro == null || tpm == null || bitlocker == null;
        var nivel = seguro == false || chaveSecureBootAusente ? NivelVerificacao.Atencao : NivelVerificacao.Ok;
        string Estado(bool? valor) => valor == true ? "ligado" : valor == false ? "desligado ou ausente" : admin ? "não consegui ler" : "precisa de administrador";
        return Leituras.Resultado("Inicialização segura, TPM e BitLocker", "\uE950", nivel,
            chaveSecureBootAusente ? "Inicialização segura não suportada (BIOS antigo)" : seguro == false ? "Inicialização segura desligada" : tpm == false ? tpmPresente ? "TPM desativado" : "TPM ausente (limite da placa-mãe)" : faltam ? admin ? "Leitura parcial da segurança do dispositivo" : "Alguns dados precisam de administrador" : "Proteções do dispositivo verificadas",
            $"Inicialização segura: {Estado(seguro)}. {(tpm == false ? tpmPresente ? "TPM presente, mas desativado" : "Este PC não tem TPM (limite da placa-mãe)" : "TPM: " + Estado(tpm))}. BitLocker do C:: {(bitlocker == null && admin ? "BitLocker não disponível" : Estado(bitlocker))}. Criptografar o disco é opcional para um PC de mesa em casa.", "windowsdefender://devicesecurity");
    }
}

public sealed class VerificacaoExclusoes : Verificacao
{
    private readonly Lazy<Task<PreferenciasDefender?>>? _preferencias;
    public VerificacaoExclusoes() { }
    internal VerificacaoExclusoes(Lazy<Task<PreferenciasDefender?>> preferencias) => _preferencias = preferencias;
    public override string Nome => "Exclusões do Defender";
    public override int Peso => 3;
    protected override async Task<ResultadoVerificacao> Ler(CancellationToken ct)
    {
        var preferencias = await (_preferencias?.Value ?? Task.Run(() => Leituras.LerPreferencias(ct))).WaitAsync(ct).ConfigureAwait(false);
        return ProcessarLeitura(preferencias?.Exclusoes, Seguranca.CaminhoExclusoes, App.CapturaArquivo != null,
            preferencias?.ExclusoesIgnoradas == true);
    }
    internal static ResultadoVerificacao ProcessarLeitura(string[]? atuais, string caminho, bool captura = false, bool ignoradas = false)
    {
        var referencia = Seguranca.ObterReferencia(caminho);
        if (referencia.Ausente && atuais is { Length: 0 } && !captura && !ignoradas)
            Seguranca.GravarReferencia(caminho, []);
        return Avaliar(atuais, referencia.Itens, referencia.Invalida, ignoradas);
    }
    internal static ResultadoVerificacao Avaliar(IEnumerable<string>? atuais, IEnumerable<string>? referencia, bool referenciaInvalida = false, bool ignoradas = false)
    {
        var lista = atuais?.ToArray();
        if (lista == null || lista.Any(Leituras.SemAdministrador))
            return Leituras.Resultado("Exclusões do Defender", "\uE71C", NivelVerificacao.Atencao, "Abra como administrador", "Não consegui ler as exclusões do Defender.", "windowsdefender://threatsettings");
        ignoradas |= lista.Any(Leituras.TemQuebraDeLinha);
        lista = lista.Where(Leituras.ExclusaoValida).ToArray();
        var detalhe = string.Join("\n", lista) + (ignoradas ? "\n\nAviso: exclusões com quebra de linha foram ignoradas." : "");
        referenciaInvalida |= referencia?.Any(i => !Leituras.ExclusaoValida(i)) == true;
        if (referenciaInvalida)
            return Leituras.Resultado("Exclusões do Defender", "\uE71C", NivelVerificacao.Atencao,
                "A lista de referência das exclusões está ilegível", detalhe, "windowsdefender://threatsettings");
        var conhecidas = referencia?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var novas = lista.Where(i => conhecidas != null && !conhecidas.Contains(i)).ToArray();
        return Leituras.Resultado("Exclusões do Defender", "\uE71C", novas.Length > 0 ? NivelVerificacao.Problema : lista.Length > 0 && conhecidas == null ? NivelVerificacao.Atencao : ignoradas ? NivelVerificacao.Atencao : NivelVerificacao.Ok,
            lista.Length == 0 ? ignoradas ? "Algumas exclusões não puderam ser lidas" : "Nenhuma exclusão no Defender" : novas.Length > 0 ? $"Exclusão nova no Defender: {novas[0]}. Vírus costumam fazer isso para se esconder." : conhecidas == null ? "Confira se você reconhece estas exclusões" : "Todas as exclusões já são conhecidas",
            detalhe, "windowsdefender://threatsettings");
    }
    // A lista exibida é a própria lista avaliada, para a confirmação nunca aceitar dados que o usuário não viu.
    internal static string[]? ListaExibida(ResultadoVerificacao item)
    {
        if (item.Nome != "Exclusões do Defender" || item.Acao is not { Tipo: "abrir", Alvo: "windowsdefender://threatsettings" }
            || item.Resumo == "Abra como administrador") return null;
        var lista = item.Detalhe.Split("\n\n", StringSplitOptions.None)[0];
        if (lista.Length == 0) return item.Resumo == "A lista de referência das exclusões está ilegível" ? [] : null;
        var itens = lista.Split('\n');
        return itens.All(Leituras.ExclusaoValida) ? itens : null;
    }
}

public sealed class VerificacaoContas : Verificacao
{
    public override string Nome => "Contas do Windows";
    public override int Peso => 2;
    protected override Task<ResultadoVerificacao> Ler(CancellationToken ct) => Task.FromResult(Avaliar(Leituras.Consultar(@"root\CIMV2",
        "SELECT SID, Name, Disabled, PasswordRequired FROM Win32_UserAccount WHERE LocalAccount=True", itens =>
        {
            var lista = new List<ContaWindows>();
            foreach (ManagementObject item in itens)
            {
                using (item)
                {
                    ct.ThrowIfCancellationRequested();
                    if (item["SID"] == null || item["Disabled"] == null) throw new InvalidDataException();
                    lista.Add(new(item["SID"].ToString()!, item["Name"]?.ToString() ?? "Conta sem nome", Convert.ToBoolean(item["Disabled"]),
                        item["PasswordRequired"] == null ? null : Convert.ToBoolean(item["PasswordRequired"])));
                }
            }
            return lista;
        })));
    internal static ResultadoVerificacao Avaliar(IEnumerable<ContaWindows>? contas)
    {
        if (contas == null) return Motor.Indisponivel("Contas do Windows");
        var ativas = contas.Where(c => !c.Desativada).ToArray();
        var convidado = ativas.Any(c => c.Sid.EndsWith("-501", StringComparison.Ordinal));
        var avisos = ativas.SelectMany(c => new[] {
            c.Sid.EndsWith("-501", StringComparison.Ordinal) ? $"Convidado ativo: {c.Nome}" : null,
            c.Sid.EndsWith("-500", StringComparison.Ordinal) ? $"Administrador embutido ativo: {c.Nome}" : null,
            c.SenhaObrigatoria == false ? $"A conta {c.Nome} pode entrar sem senha" : c.SenhaObrigatoria == null ? $"Não consegui ler a exigência de senha da conta {c.Nome}" : null
        }).Where(t => t != null).ToArray();
        return Leituras.Resultado("Contas do Windows", "\uE716", convidado ? NivelVerificacao.Problema : avisos.Length > 0 ? NivelVerificacao.Atencao : NivelVerificacao.Ok,
            convidado ? "Conta Convidado ativa" : avisos.FirstOrDefault() ?? "Contas locais sem alertas",
            avisos.Length == 0 ? "Contas desativadas foram ignoradas." : string.Join("\n", avisos), "ms-settings:otherusers");
    }
}
