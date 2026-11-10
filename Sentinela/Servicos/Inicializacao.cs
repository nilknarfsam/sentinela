using System.Diagnostics;
using Microsoft.Win32;
using System.Buffers.Binary;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace Sentinela.Servicos;

public sealed record ProgramaInicial(string Nome, string Comando, string Origem, bool Ativo)
{
    public string Estado => Ativo ? "Ativo" : "Desativado";
    public RegistryHive Raiz { get; init; }
    public string Subchave { get; init; } = "";
    public string NomeValor { get; init; } = "";
    public bool TodosOsUsuarios => Raiz == RegistryHive.LocalMachine;
    public string Editora { get; init; } = "—";
    public EstadoAssinatura Assinatura { get; init; }
    public string TextoAssinatura => Assinaturas.Texto(Assinatura);
}

/// <summary>
/// Lê Run e Inicializar; altera apenas o estado reversível em StartupApproved.
/// </summary>
public static class Inicializacao
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Aprovado = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    public static List<ProgramaInicial> Listar(bool assinaturas = true) => Listar(assinaturas, out _);

    internal static List<ProgramaInicial> Listar(bool assinaturas, out bool completo)
    {
        var falhou = false;
        void Falha() => falhou = true;
        var lista = new List<ProgramaInicial>();
        LerRun(lista, Registry.CurrentUser, Run, "Usuário", Aprovado + "Run", Falha);
        LerRun(lista, Registry.LocalMachine, Run, "Todos os usuários", Aprovado + "Run", Falha);
        LerRun(lista, Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Todos (32 bits)", Aprovado + "Run32", Falha);
        LerPasta(lista, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Pasta Inicializar", Registry.CurrentUser, Falha);
        LerPasta(lista, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Pasta Inicializar (todos)", Registry.LocalMachine, Falha);
        if (assinaturas)
            lista = lista.Select(p =>
            {
                var a = Assinaturas.LerComando(p.Comando);
                return p with { Editora = a.Editora, Assinatura = a.Estado };
            }).ToList();
        completo = !falhou;
        return lista.OrderByDescending(p => p.Ativo).ThenBy(p => p.Nome).ToList();
    }

    public static void AbrirGerenciadorDeTarefas()
    {
        try { Process.Start(new ProcessStartInfo(CaminhosSistema.TaskMgr, "/0 /startup") { UseShellExecute = true }); }
        catch { /* O sistema pode não disponibilizar o gerenciador. */ }
    }

    internal static (RegistryHive Raiz, string Subchave) ChaveStartupApproved(string origem) => origem switch
    {
        "Usuário" => (RegistryHive.CurrentUser, Aprovado + "Run"),
        "Todos os usuários" => (RegistryHive.LocalMachine, Aprovado + "Run"),
        "Todos (32 bits)" => (RegistryHive.LocalMachine, Aprovado + "Run32"),
        "Pasta Inicializar" => (RegistryHive.CurrentUser, Aprovado + "StartupFolder"),
        "Pasta Inicializar (todos)" => (RegistryHive.LocalMachine, Aprovado + "StartupFolder"),
        _ => (RegistryHive.CurrentUser, "")
    };

    internal static byte[] ValorStartupApproved(bool ligar, DateTime agoraUtc)
    {
        var valor = new byte[12];
        valor[0] = ligar ? (byte)2 : (byte)3;
        if (!ligar) BinaryPrimitives.WriteInt64LittleEndian(valor.AsSpan(4), agoraUtc.ToFileTimeUtc());
        return valor;
    }

    public static (bool ok, string mensagem) Definir(ProgramaInicial p, bool ligar)
    {
        try
        {
            var chave = ChaveStartupApproved(p.Origem);
            if (p.Subchave != chave.Subchave || p.Raiz != chave.Raiz || string.IsNullOrEmpty(p.Subchave) || string.IsNullOrEmpty(p.NomeValor))
                return (false, "Origem de inicialização não reconhecida.");
            using var identidade = WindowsIdentity.GetCurrent();
            if (p.TodosOsUsuarios && !new WindowsPrincipal(identidade).IsInRole(WindowsBuiltInRole.Administrator))
                return (false, "Abra o Sentinela como administrador para alterar itens de todos os usuários.");
            using var raiz = RegistryKey.OpenBaseKey(p.Raiz, RegistryView.Registry64);
            using var destino = raiz.CreateSubKey(p.Subchave);
            destino.SetValue(p.NomeValor, ValorStartupApproved(ligar, DateTime.UtcNow), RegistryValueKind.Binary);
            var mensagem = $"{(ligar ? "Ligado" : "Desligado")}: {p.Nome}";
            Diario.Registrar("inicializacao", mensagem);
            return (true, mensagem);
        }
        catch (Exception ex) { return (false, "Não foi possível alterar a inicialização: " + ex.Message); }
    }

    // Só interpretação de texto: nenhum acesso ao disco, registro ou COM.
    internal static string? ExtrairExecutavel(string comando)
    {
        if (string.IsNullOrWhiteSpace(comando)) return null;
        var texto = Environment.ExpandEnvironmentVariables(comando.Trim());
        var host = Regex.Match(texto, "^(?:\"[^\"]*rundll32(?:\\.exe)?\"|\\S*rundll32(?:\\.exe)?)\\s+", RegexOptions.IgnoreCase);
        if (host.Success) texto = texto[host.Length..].TrimStart();
        string? caminho;
        if (texto.StartsWith('"'))
        {
            var fim = texto.IndexOf('"', 1);
            caminho = fim > 1 ? texto[1..fim] : null;
        }
        else
        {
            var achado = Regex.Match(texto, @"^(.+?\.(?:exe|dll|lnk|com|cmd|bat|ps1))(?=\s|,|$)", RegexOptions.IgnoreCase);
            caminho = achado.Success ? achado.Groups[1].Value : null;
        }
        return caminho != null && Path.IsPathFullyQualified(caminho) ? caminho : null;
    }

    private static void LerRun(List<ProgramaInicial> lista, RegistryKey raiz, string caminho, string origem, string aprovado, Action falha)
    {
        try
        {
            using var k = raiz.OpenSubKey(caminho);
            if (k == null) return;
            foreach (var nome in k.GetValueNames())
            {
                if (string.IsNullOrEmpty(nome)) continue;
                var chave = ChaveStartupApproved(origem);
                lista.Add(new(nome, k.GetValue(nome)?.ToString() ?? "", origem, EstaAtivo(raiz, aprovado, nome))
                    { Raiz = chave.Raiz, Subchave = chave.Subchave, NomeValor = nome });
            }
        }
        catch { falha(); /* Preserva as outras origens quando uma chave não puder ser lida. */ }
    }

    private static void LerPasta(List<ProgramaInicial> lista, string pasta, string origem, RegistryKey raiz, Action falha)
    {
        try
        {
            if (!Directory.Exists(pasta)) return;
            foreach (var f in Directory.EnumerateFiles(pasta).Where(f => !f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)))
            {
                var nome = Path.GetFileName(f);
                var chave = ChaveStartupApproved(origem);
                lista.Add(new(Path.GetFileNameWithoutExtension(f), f, origem, EstaAtivo(raiz, Aprovado + "StartupFolder", nome))
                    { Raiz = chave.Raiz, Subchave = chave.Subchave, NomeValor = nome });
            }
        }
        catch { falha(); /* Uma pasta indisponível não impede a leitura das demais. */ }
    }

    /// <summary>O Gerenciador de Tarefas guarda o liga/desliga em StartupApproved (1º byte par = ativo).</summary>
    private static bool EstaAtivo(RegistryKey raiz, string caminho, string nome)
    {
        using var k = raiz.OpenSubKey(caminho);
        return AtivoPeloValor(k?.GetValue(nome));
    }

    internal static bool AtivoPeloValor(object? valor)
        => valor is not byte[] { Length: > 0 } b || (b[0] & 1) == 0;
}
