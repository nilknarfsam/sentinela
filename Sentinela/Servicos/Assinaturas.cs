using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Sentinela.Servicos;

public enum EstadoAssinatura { Assinado, NaoAssinado, Invalida, ArquivoNaoEncontrado }
public sealed record InfoAssinatura(string Editora, EstadoAssinatura Estado);

/// <summary>Chamado em segundo plano. Cache por caminho, data e tamanho, compartilhado entre as listas.</summary>
public static class Assinaturas
{
    private static readonly ConcurrentDictionary<string, Lazy<InfoAssinatura>> Cache = new(StringComparer.OrdinalIgnoreCase);
    public static string Texto(EstadoAssinatura estado) => estado switch
    {
        EstadoAssinatura.Assinado => "Assinado",
        EstadoAssinatura.NaoAssinado => "Sem assinatura",
        EstadoAssinatura.Invalida => "Assinatura inválida",
        _ => "Arquivo não existe"
    };

    public static InfoAssinatura LerComando(string comando)
    {
        try
        {
            var caminho = Inicializacao.ExtrairExecutavel(comando);
            if (caminho == null) return new("?", EstadoAssinatura.ArquivoNaoEncontrado);
            if (caminho.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) caminho = AlvoAtalho(caminho);
            if (string.IsNullOrWhiteSpace(caminho)) return new("—", EstadoAssinatura.ArquivoNaoEncontrado);
            caminho = Path.GetFullPath(Environment.ExpandEnvironmentVariables(caminho));
            var arquivo = new FileInfo(caminho);
            if (!arquivo.Exists) return new("?", EstadoAssinatura.ArquivoNaoEncontrado);
            var chave = $"{caminho}|{arquivo.LastWriteTimeUtc.Ticks}|{arquivo.Length}";
            return Cache.GetOrAdd(chave, _ => new Lazy<InfoAssinatura>(() => Verificar(caminho))).Value;
        }
        catch { return new("—", EstadoAssinatura.Invalida); }
    }

    private static string? AlvoAtalho(string caminho)
    {
        object? shell = null, atalho = null;
        try
        {
            if (!File.Exists(caminho)) return null;
            var tipo = Type.GetTypeFromProgID("WScript.Shell");
            if (tipo == null) return null;
            shell = Activator.CreateInstance(tipo);
            if (shell == null) return null;
            atalho = ((dynamic)shell).CreateShortcut(caminho);
            return (string)((dynamic)atalho).TargetPath;
        }
        catch { return null; }
        finally { LiberarCom(atalho); LiberarCom(shell); }
    }

    internal static void LiberarCom(object? valor)
    {
        try { if (valor != null && Marshal.IsComObject(valor)) Marshal.FinalReleaseComObject(valor); }
        catch { /* O recurso pode já ter sido liberado. */ }
    }

    private static InfoAssinatura Verificar(string caminho)
    {
        if (!File.Exists(caminho)) return new("—", EstadoAssinatura.ArquivoNaoEncontrado);
        var memoria = IntPtr.Zero;
        var arquivo = new ArquivoTrust { Tamanho = (uint)Marshal.SizeOf<ArquivoTrust>(), Caminho = caminho };
        var dados = new DadosTrust { Tamanho = (uint)Marshal.SizeOf<DadosTrust>(), Interface = 2, Escolha = 1,
            Acao = 1, Flags = 0x1010 }; // Sem revogação; somente cache de URLs (sem rede).
        var acao = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
        try
        {
            memoria = Marshal.AllocHGlobal(Marshal.SizeOf<ArquivoTrust>());
            Marshal.StructureToPtr(arquivo, memoria, false);
            dados.Arquivo = memoria;
            var resultado = WinVerifyTrust(new IntPtr(-1), ref acao, ref dados);
            var estado = resultado == 0 ? EstadoAssinatura.Assinado :
                resultado == unchecked((int)0x800B0100) ? EstadoAssinatura.NaoAssinado : EstadoAssinatura.Invalida;
            var editora = "—";
            try
            {
                var provedor = WTHelperProvDataFromStateData(dados.Estado);
                var assinante = provedor == IntPtr.Zero ? IntPtr.Zero : WTHelperGetProvSignerFromChain(provedor, 0, false, 0);
                if (assinante != IntPtr.Zero)
                {
                    var signatario = Marshal.PtrToStructure<SignatarioTrust>(assinante);
                    if (signatario.Quantidade > 0 && signatario.Cadeia != IntPtr.Zero)
                    {
                        var certificado = Marshal.PtrToStructure<CertificadoTrust>(signatario.Cadeia);
                        var contexto = Marshal.PtrToStructure<ContextoCertificado>(certificado.Certificado);
                        var bytes = new byte[contexto.Tamanho];
                        Marshal.Copy(contexto.Dados, bytes, 0, bytes.Length);
                        using var x509 = X509CertificateLoader.LoadCertificate(bytes);
                        editora = x509.GetNameInfo(X509NameType.SimpleName, false);
                    }
                }
            }
            catch { /* Arquivos sem certificado continuam com o estado retornado pelo WinVerifyTrust. */ }
            return new(editora, estado);
        }
        catch { return new("—", EstadoAssinatura.Invalida); }
        finally
        {
            try { dados.Acao = 2; if (memoria != IntPtr.Zero) WinVerifyTrust(new IntPtr(-1), ref acao, ref dados); }
            catch { /* Fechamento do estado de verificação. */ }
            if (memoria != IntPtr.Zero) { Marshal.DestroyStructure<ArquivoTrust>(memoria); Marshal.FreeHGlobal(memoria); }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ArquivoTrust
    {
        public uint Tamanho;
        [MarshalAs(UnmanagedType.LPWStr)] public string Caminho;
        public IntPtr Arquivo, Assunto;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DadosTrust
    {
        public uint Tamanho;
        public IntPtr Politica, Cliente;
        public uint Interface, Revogacao, Escolha;
        public IntPtr Arquivo;
        public uint Acao;
        public IntPtr Estado, Url;
        public uint Flags, Contexto;
        public IntPtr Assinaturas;
    }

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr janela, ref Guid acao, ref DadosTrust dados);

    [StructLayout(LayoutKind.Sequential)]
    private struct SignatarioTrust
    {
        public uint Tamanho, DataBaixa, DataAlta, Quantidade;
        public IntPtr Cadeia;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct CertificadoTrust
    {
        public uint Tamanho;
        public IntPtr Certificado;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ContextoCertificado
    {
        public uint Codificacao;
        public IntPtr Dados;
        public uint Tamanho;
    }
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperProvDataFromStateData(IntPtr estado);
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr provedor, uint indice,
        [MarshalAs(UnmanagedType.Bool)] bool contraAssinatura, uint indiceContraAssinatura);
}
