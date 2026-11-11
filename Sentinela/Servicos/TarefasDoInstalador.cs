using System.Diagnostics;
using System.Security.AccessControl;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Sentinela.Servicos;

/// <summary>Cria e remove as tarefas agendadas usadas pelo instalador MSI.</summary>
internal static partial class TarefasDoInstalador
{
    private const string TarefaBandeja = "Sentinela";
    private const string TarefaAbrir = "Sentinela-Abrir";
    private static readonly TimeSpan TempoLimite = TimeSpan.FromSeconds(20);

    internal enum AcaoInstalador { Nenhuma, InstalarTarefas, RemoverTarefas }

    internal readonly record struct Comando(AcaoInstalador Acao, string? Sid);

    internal static bool ContemComandoInstalador(IReadOnlyList<string> argumentos)
        => argumentos.Any(a => string.Equals(a, "--instalar-tarefas", StringComparison.OrdinalIgnoreCase)
            || string.Equals(a, "--remover-tarefas", StringComparison.OrdinalIgnoreCase));

    internal static bool InterpretarArgumentos(IReadOnlyList<string> argumentos, out Comando comando)
    {
        comando = default;
        if (argumentos.Count == 3
            && string.Equals(argumentos[0], "--instalar-tarefas", StringComparison.Ordinal)
            && string.Equals(argumentos[1], "--sid", StringComparison.Ordinal)
            && SidValido(argumentos[2]))
        {
            comando = new(AcaoInstalador.InstalarTarefas, argumentos[2]);
            return true;
        }

        if (argumentos.Count == 1
            && string.Equals(argumentos[0], "--remover-tarefas", StringComparison.Ordinal))
        {
            comando = new(AcaoInstalador.RemoverTarefas, null);
            return true;
        }

        return false;
    }

    internal static bool SidValido(string? sid)
        => sid is not null && SidRegex().IsMatch(sid);

    // S-1-5-21: conta local ou de domínio. S-1-12-1: conta Microsoft Entra (Azure AD).
    [GeneratedRegex(@"\AS-1-(?:5-21|12-1)(?:-[0-9]+){4}\z", RegexOptions.CultureInvariant)]
    private static partial Regex SidRegex();

    /// <summary>SYSTEM e Administradores com controle total; o usuário só lê e executa (para o atalho funcionar sem elevar).</summary>
    internal static string MontarDescritorSeguranca(string sid)
    {
        if (!SidValido(sid)) throw new ArgumentException("O SID do usuário é inválido.", nameof(sid));
        return $"D:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;FRFX;;;{sid})";
    }

    internal static string MontarXmlTarefa(string sid, string executavel, bool aoEntrar, bool ligada = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executavel);
        if (!SidValido(sid)) throw new ArgumentException("O SID do usuário é inválido.", nameof(sid));

        var textoXml = new StringBuilder();
        var configuracao = new XmlWriterSettings { Encoding = Encoding.Unicode, Indent = true, OmitXmlDeclaration = false };
        using (var escritor = XmlWriter.Create(textoXml, configuracao))
        {
            escritor.WriteStartDocument();
            escritor.WriteStartElement("Task", "http://schemas.microsoft.com/windows/2004/02/mit/task");
            escritor.WriteAttributeString("version", "1.2");
            escritor.WriteStartElement("RegistrationInfo");
            escritor.WriteElementString("Description", aoEntrar
                ? "Abre o Sentinela na bandeja ao entrar no Windows."
                : "Usada pelo atalho Sentinela do menu Iniciar.");
            escritor.WriteElementString("SecurityDescriptor", MontarDescritorSeguranca(sid));
            escritor.WriteEndElement();

            if (aoEntrar)
            {
                escritor.WriteStartElement("Triggers");
                escritor.WriteStartElement("LogonTrigger");
                escritor.WriteElementString("Enabled", "true");
                escritor.WriteElementString("UserId", sid);
                escritor.WriteElementString("Delay", "PT20S");
                escritor.WriteEndElement();
                escritor.WriteEndElement();
            }

            escritor.WriteStartElement("Principals");
            escritor.WriteStartElement("Principal");
            escritor.WriteAttributeString("id", "Criador");
            escritor.WriteElementString("UserId", sid);
            escritor.WriteElementString("LogonType", "InteractiveToken");
            escritor.WriteElementString("RunLevel", "HighestAvailable");
            escritor.WriteEndElement();
            escritor.WriteEndElement();

            escritor.WriteStartElement("Settings");
            escritor.WriteElementString("MultipleInstancesPolicy", "Parallel");
            escritor.WriteElementString("DisallowStartIfOnBatteries", "false");
            escritor.WriteElementString("StopIfGoingOnBatteries", "false");
            escritor.WriteElementString("ExecutionTimeLimit", "PT0S");
            escritor.WriteElementString("StartWhenAvailable", "true");
            escritor.WriteElementString("Enabled", ligada ? "true" : "false");
            escritor.WriteEndElement();

            escritor.WriteStartElement("Actions");
            escritor.WriteAttributeString("Context", "Criador");
            escritor.WriteStartElement("Exec");
            escritor.WriteElementString("Command", executavel);
            if (aoEntrar) escritor.WriteElementString("Arguments", "--bandeja");
            escritor.WriteEndElement();
            escritor.WriteEndElement();
            escritor.WriteEndElement();
            escritor.WriteEndDocument();
        }
        return textoXml.ToString();
    }

    internal static bool Criar(string? sid)
    {
        string? arquivoBandeja = null;
        string? arquivoAbrir = null;
        string? pastaTemporaria = null;
        try
        {
            if (!ExecutaComoSistema()) return false;
            if (!SidValido(sid)) return false;
            var executavel = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executavel)) return false;
            pastaTemporaria = CriarPastaTemporariaSegura();
            arquivoBandeja = Path.Combine(pastaTemporaria, "Sentinela.xml");
            arquivoAbrir = Path.Combine(pastaTemporaria, "Sentinela-Abrir.xml");
            // Num upgrade, mantém a escolha dos Ajustes: se o usuário desligou o início com o Windows, continua desligado.
            var ligada = TarefaInicializacao.EstaLigada() ?? true;
            CriarXmlTemporario(arquivoBandeja, MontarXmlTarefa(sid!, executavel, true, ligada));
            CriarXmlTemporario(arquivoAbrir, MontarXmlTarefa(sid!, executavel, false));

            var primeira = Executar("/create", "/tn", TarefaBandeja, "/xml", arquivoBandeja, "/f");
            if (!primeira.Ok) return false;
            return Executar("/create", "/tn", TarefaAbrir, "/xml", arquivoAbrir, "/f").Ok;
        }
        catch { return false; }
        finally
        {
            ApagarTemporario(arquivoBandeja);
            ApagarTemporario(arquivoAbrir);
            ApagarPastaTemporaria(pastaTemporaria);
        }
    }

    internal static bool Remover()
    {
        try
        {
            if (!ExecutaComoSistema()) return false;
            var bandeja = Executar("/delete", "/tn", TarefaBandeja, "/f");
            if (!bandeja.Ok && !TarefaAusente(TarefaBandeja)) return false;
            var abrir = Executar("/delete", "/tn", TarefaAbrir, "/f");
            return abrir.Ok || TarefaAusente(TarefaAbrir);
        }
        catch { return false; }
    }

    private static bool ExecutaComoSistema()
    {
        try
        {
            using var identidade = WindowsIdentity.GetCurrent();
            return identidade.User?.IsWellKnown(WellKnownSidType.LocalSystemSid) == true;
        }
        catch { return false; }
    }

    private static string CriarPastaTemporariaSegura()
    {
        var temporario = Path.GetFullPath(Path.GetTempPath());
        if ((File.GetAttributes(temporario) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("A pasta temporária não é segura.");

        var caminho = Path.Combine(temporario, $"Sentinela-Instalador-{Guid.NewGuid():N}");
        var sistema = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var administradores = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var seguranca = new DirectorySecurity();
        seguranca.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        seguranca.SetOwner(sistema);
        var heranca = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        seguranca.AddAccessRule(new FileSystemAccessRule(sistema, FileSystemRights.FullControl, heranca,
            PropagationFlags.None, AccessControlType.Allow));
        seguranca.AddAccessRule(new FileSystemAccessRule(administradores, FileSystemRights.FullControl, heranca,
            PropagationFlags.None, AccessControlType.Allow));

        var pasta = new DirectoryInfo(caminho);
        pasta.Create(seguranca);
        try
        {
            if ((pasta.Attributes & FileAttributes.ReparsePoint) != 0 || !PermissoesTemporariasSeguras(pasta))
                throw new IOException("Não foi possível proteger a pasta temporária.");
            return pasta.FullName;
        }
        catch
        {
            try
            {
                if ((pasta.Attributes & FileAttributes.ReparsePoint) == 0 && !pasta.EnumerateFileSystemInfos().Any())
                    pasta.Delete();
            }
            catch { }
            throw;
        }
    }

    private static bool PermissoesTemporariasSeguras(DirectoryInfo pasta)
    {
        var seguranca = pasta.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
        var sistema = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var administradores = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        if (!seguranca.AreAccessRulesProtected || !sistema.Equals(seguranca.GetOwner(typeof(SecurityIdentifier)))) return false;
        var regras = seguranca.GetAccessRules(includeExplicit: true, includeInherited: true, targetType: typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>().ToArray();
        return regras.Length == 2 && regras.All(r => r.AccessControlType == AccessControlType.Allow
            && r.FileSystemRights == FileSystemRights.FullControl
            && r.InheritanceFlags == (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit)
            && r.PropagationFlags == PropagationFlags.None)
            && regras.Any(r => sistema.Equals(r.IdentityReference))
            && regras.Any(r => administradores.Equals(r.IdentityReference));
    }

    private static void CriarXmlTemporario(string caminho, string xml)
    {
        using var fluxo = new FileStream(caminho, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var escritor = new StreamWriter(fluxo, Encoding.Unicode, 1024, leaveOpen: true);
        escritor.Write(xml);
        escritor.Flush();
    }

    private static (bool Ok, int Codigo, string Texto) Executar(params string[] argumentos)
    {
        try
        {
            var inicio = new ProcessStartInfo(CaminhosSistema.SchTasks) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var argumento in argumentos) inicio.ArgumentList.Add(argumento);
            inicio.RedirectStandardOutput = true;
            inicio.RedirectStandardError = true;
            using var processo = Process.Start(inicio);
            if (processo is null) return (false, -1, "");
            var saida = processo.StandardOutput.ReadToEndAsync();
            var erro = processo.StandardError.ReadToEndAsync();
            if (!processo.WaitForExit((int)TempoLimite.TotalMilliseconds))
            {
                try { processo.Kill(true); } catch { }
                return (false, -1, "");
            }
            var texto = saida.GetAwaiter().GetResult() + erro.GetAwaiter().GetResult();
            return (processo.ExitCode == 0, processo.ExitCode, texto);
        }
        catch { return (false, -1, ""); }
    }

    private static bool TarefaAusente(string nome)
    {
        object? servico = null;
        object? pasta = null;
        object? tarefa = null;
        try
        {
            var tipo = Type.GetTypeFromProgID("Schedule.Service", throwOnError: true);
            servico = Activator.CreateInstance(tipo!);
            if (servico is null) return false;
            ((dynamic)servico).Connect();
            pasta = ((dynamic)servico).GetFolder("\\");
            try
            {
                tarefa = ((dynamic)pasta).GetTask(nome);
                return false;
            }
            catch (COMException erro)
            {
                // ERROR_FILE_NOT_FOUND retornado por GetTask quando a tarefa não existe.
                return unchecked((uint)erro.ErrorCode) == 0x80070002;
            }
        }
        catch { return false; }
        finally
        {
            LiberarCom(tarefa);
            LiberarCom(pasta);
            LiberarCom(servico);
        }
    }

    private static void LiberarCom(object? objeto)
    {
        try { if (objeto is not null && Marshal.IsComObject(objeto)) Marshal.FinalReleaseComObject(objeto); }
        catch { }
    }

    private static void ApagarTemporario(string? caminho)
    {
        if (caminho is null) return;
        try { if (File.Exists(caminho)) File.Delete(caminho); } catch { }
    }

    private static void ApagarPastaTemporaria(string? caminho)
    {
        if (caminho is null) return;
        try
        {
            var pasta = new DirectoryInfo(caminho);
            if (pasta.Exists && (pasta.Attributes & FileAttributes.ReparsePoint) == 0)
                pasta.Delete(recursive: false);
        }
        catch { }
    }
}
