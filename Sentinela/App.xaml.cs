using System.Windows;
using WinForms = System.Windows.Forms;

namespace Sentinela;

public partial class App : Application
{
    private const string NomeUnico = "Sentinela_Franklin_Instancia";
    private Mutex? _mutex;
    private EventWaitHandle? _sinalMostrar;
    private WinForms.NotifyIcon? _bandeja;
    private MainWindow? _janela;

    /// <summary>Iniciado pelo Windows: fica só na bandeja, sem abrir a janela.</summary>
    public static bool IniciarNaBandeja { get; private set; }
    /// <summary>Modo de teste: salva uma imagem da janela e fecha.</summary>
    public static string? CapturaArquivo { get; private set; }
    public static string? CapturaAba { get; private set; }
    public bool Saindo { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        for (int i = 0; i < e.Args.Length; i++)
        {
            switch (e.Args[i].ToLowerInvariant())
            {
                case "--bandeja": IniciarNaBandeja = true; break;
                case "--captura" when i + 1 < e.Args.Length: CapturaArquivo = e.Args[++i]; break;
                case "--aba" when i + 1 < e.Args.Length: CapturaAba = e.Args[++i]; break;
            }
        }

        if (CapturaArquivo == null)
        {
            // Uma instância só. Se já estiver aberto, pede para a outra mostrar a janela.
            bool primeira;
            try
            {
                _mutex = new Mutex(true, NomeUnico, out primeira);
                _sinalMostrar = new EventWaitHandle(false, EventResetMode.AutoReset, NomeUnico + "_Mostrar");
            }
            catch (UnauthorizedAccessException)
            {
                // A outra instância roda como administrador e o Windows não deixa
                // um programa comum falar com ela.
                MessageBox.Show("O Sentinela já está aberto como administrador.\n\n" +
                                "Clique no escudo perto do relógio, ou use o atalho \"Sentinela\" do menu Iniciar.",
                                "Sentinela", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
            if (!primeira && e.Args.Contains("--elevado"))
            {
                // Reaberto como admin: espera a instância antiga terminar de fechar.
                try { primeira = _mutex.WaitOne(8000); }
                catch (AbandonedMutexException) { primeira = true; }
            }
            if (!primeira)
            {
                _sinalMostrar.Set();
                Shutdown();
                return;
            }
            var t = new Thread(() =>
            {
                while (_sinalMostrar.WaitOne())
                    Dispatcher.Invoke(MostrarJanela);
            }) { IsBackground = true };
            t.Start();
            CriarBandeja();
        }

        _janela = new MainWindow();
        if (!IniciarNaBandeja || CapturaArquivo != null) _janela.Show();
    }

    private void CriarBandeja()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Abrir o Sentinela", null, (_, _) => MostrarJanela());
        menu.Items.Add("Varredura rápida", null, (_, _) => { MostrarJanela(); _janela?.IniciarVarredura(Servicos.TipoVarredura.Rapida); });
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => Sair());

        _bandeja = new WinForms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Shield,
            Text = "Sentinela",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _bandeja.MouseClick += (_, a) => { if (a.Button == WinForms.MouseButtons.Left) MostrarJanela(); };
    }

    public void MostrarJanela()
    {
        _janela ??= new MainWindow();
        _janela.Show();
        if (_janela.WindowState == WindowState.Minimized) _janela.WindowState = WindowState.Normal;
        _janela.Activate();
    }

    public void Avisar(string titulo, string texto, bool erro = false)
    {
        if (_bandeja == null) return;
        _bandeja.ShowBalloonTip(5000, titulo, texto, erro ? WinForms.ToolTipIcon.Warning : WinForms.ToolTipIcon.Info);
    }

    public void AtualizarIconeTexto(string texto)
    {
        if (_bandeja != null) _bandeja.Text = texto.Length > 63 ? texto[..63] : texto;
    }

    public void Sair()
    {
        Saindo = true;
        if (_bandeja != null) { _bandeja.Visible = false; _bandeja.Dispose(); }
        _janela?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _bandeja?.Dispose();
        try { _mutex?.ReleaseMutex(); } catch { }
        base.OnExit(e);
    }
}
