using System.Windows;
using WinForms = System.Windows.Forms;
using Sentinela.Servicos;
using System.Windows.Threading;

namespace Sentinela;

public partial class App : Application
{
    private const string NomeUnico = "Sentinela_Franklin_Instancia";
    private Mutex? _mutex;
    private EventWaitHandle? _sinalMostrar;
    private WinForms.NotifyIcon? _bandeja;
    private MainWindow? _janela;
    private Dictionary<NivelStatus, System.Drawing.Icon> _icones = new();
    private NivelStatus? _nivelStatus;
    private VigiaDefender? _vigia;
    private readonly DispatcherTimer _timerAvisos = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly System.Diagnostics.Stopwatch _desdeAviso = new();
    private readonly List<(string Texto, bool Erro)> _avisosPendentes = [];

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

        Diario.Desligado = CapturaArquivo != null;

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
        if (CapturaArquivo == null)
        {
            _timerAvisos.Tick += (_, _) => MostrarAvisosPendentes();
            _vigia = new VigiaDefender();
            _vigia.Chegou += evento =>
            {
                if (!Dispatcher.HasShutdownStarted)
                    Dispatcher.BeginInvoke(new Action(() => ReceberEventoDefender(evento)));
            };
            _vigia.Iniciar();
        }
        Diario.Registrar("inicio", IniciarNaBandeja ? "Sentinela iniciado na bandeja" : "Sentinela iniciado");
        _ = _janela.Iniciar();
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
        if (_janela == null)
        {
            _janela = new MainWindow();
            _ = _janela.Iniciar();
        }
        _janela.Show();
        if (_janela.WindowState == WindowState.Minimized) _janela.WindowState = WindowState.Normal;
        _janela.Activate();
    }

    public void Avisar(string titulo, string texto, bool erro = false, bool rotina = false)
    {
        if (_bandeja == null || (rotina && !Configuracoes.Atual.AvisosDeRotina)) return;
        if (_desdeAviso.IsRunning && _desdeAviso.Elapsed < TimeSpan.FromSeconds(5))
        {
            _avisosPendentes.Add((texto, erro));
            if (!_timerAvisos.IsEnabled)
            {
                _timerAvisos.Interval = TimeSpan.FromSeconds(5) - _desdeAviso.Elapsed;
                _timerAvisos.Start();
            }
            return;
        }
        if (_avisosPendentes.Count > 0)
        {
            _avisosPendentes.Add((texto, erro));
            MostrarAvisosPendentes();
            return;
        }
        _bandeja.ShowBalloonTip(5000, titulo, texto, erro ? WinForms.ToolTipIcon.Warning : WinForms.ToolTipIcon.Info);
        _desdeAviso.Restart();
    }

    private void MostrarAvisosPendentes()
    {
        _timerAvisos.Stop();
        if (_avisosPendentes.Count == 0 || _bandeja == null) return;
        var restante = TimeSpan.FromSeconds(5) - _desdeAviso.Elapsed;
        if (restante > TimeSpan.Zero)
        {
            _timerAvisos.Interval = restante;
            _timerAvisos.Start();
            return;
        }
        var erro = _avisosPendentes.Any(a => a.Erro);
        var texto = erro ? _avisosPendentes.First(a => a.Erro).Texto : _avisosPendentes[^1].Texto;
        if (texto.Length > 150) texto = texto[..149] + "…";
        var outros = _avisosPendentes.Count - 1;
        if (outros > 0)
            texto += $" (e mais {outros} {(outros == 1 ? "aviso" : "avisos")}. Abra o Histórico.)";
        _bandeja.ShowBalloonTip(5000, "Sentinela", texto,
            erro ? WinForms.ToolTipIcon.Warning : WinForms.ToolTipIcon.Info);
        _avisosPendentes.Clear();
        _desdeAviso.Restart();
    }

    private void ReceberEventoDefender(EventoDefender evento)
    {
        if (Saindo) return;
        if (evento.Id is 1000 or 1001 or 1002 && _janela?.IgnorarEventoDeVarredura == true) return;
        var relerStatus = false;
        var relerAmeacas = false;
        switch (evento.Tipo)
        {
            case TipoEvento.Ameaca:
                Avisar("Sentinela", "Ameaça detectada: " + evento.Detalhe, erro: true);
                relerStatus = relerAmeacas = true;
                break;
            case TipoEvento.AmeacaTratada:
                Avisar("Sentinela", "O Defender tratou: " + evento.Detalhe, rotina: false);
                relerStatus = relerAmeacas = true;
                break;
            case TipoEvento.Protecao:
                relerStatus = true;
                if (evento.Id is 5001 or 5010 or 5012)
                {
                    Avisar("Sentinela", evento.Titulo, erro: true);
                }
                break;
            case TipoEvento.Configuracao:
                Avisar("Sentinela", "Configuração do Defender mudou: " + evento.Detalhe + ". Se não foi você, abra a Segurança do Windows.", erro: true);
                break;
            case TipoEvento.Varredura when evento.Id == 1001:
                Avisar("Sentinela", evento.Titulo + ": " + evento.Detalhe, rotina: true);
                relerStatus = true;
                break;
            case TipoEvento.Assinaturas:
                relerStatus = true;
                if (evento.Id == 2001) Avisar("Sentinela", evento.Titulo, erro: true);
                break;
        }
        _janela?.ReceberEventoDefender(relerStatus, relerAmeacas);
    }

    public void AtualizarIcone(NivelStatus nivel, string texto, string? aviso = null)
    {
        try
        {
            var anterior = _nivelStatus;
            var mudou = anterior != nivel;
            _nivelStatus = nivel;

            if (mudou)
            {
                if (anterior is { } previo ? nivel > previo : nivel != NivelStatus.Ok)
                    Diario.Registrar("alerta", $"Status mudou de {anterior?.ToString() ?? "desconhecido"} para {nivel}: {texto}");
                else if (anterior != null)
                    Diario.Registrar("status", $"Status voltou para {nivel}");
            }

            if (_bandeja == null) return;
            _bandeja.Text = texto.Length > 63 ? texto[..63] : texto;
            if (!mudou) return;

            try
            {
                if (_icones.Count == 0)
                {
                    var icones = new Dictionary<NivelStatus, System.Drawing.Icon>();
                    try
                    {
                        foreach (var (status, nome) in new[] { (NivelStatus.Ok, "ok"), (NivelStatus.Atencao, "atencao"), (NivelStatus.Alerta, "alerta") })
                        {
                            using var stream = GetResourceStream(new Uri($"pack://application:,,,/Icones/status-{nome}.ico")).Stream;
                            using var icone = new System.Drawing.Icon(stream);
                            icones.Add(status, (System.Drawing.Icon)icone.Clone());
                        }
                    }
                    catch
                    {
                        foreach (var icone in icones.Values) icone.Dispose();
                        throw;
                    }
                    _icones = icones;
                }
                _bandeja.Icon = _icones[nivel];
            }
            catch { /* Mantém o ícone atual se os recursos não puderem ser carregados. */ }

            if (nivel == NivelStatus.Alerta)
                Avisar("Sentinela", aviso ?? "Atenção: uma proteção do Windows Defender está desligada. Abra o Sentinela para ver.", erro: true);
        }
        catch { /* A atualização da bandeja não pode interromper o aplicativo. */ }
    }

    public void Sair()
    {
        Saindo = true;
        if (_bandeja != null) { _bandeja.Visible = false; _bandeja.Dispose(); }
        _janela?.Close();
        Shutdown();
    }

    public void AtualizarAndamentoVarredura(string texto)
    {
        if (_bandeja != null) _bandeja.Text = texto.Length > 63 ? texto[..63] : texto;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _vigia?.Dispose();
        _timerAvisos.Stop();
        _bandeja?.Dispose();
        foreach (var icone in _icones.Values) icone.Dispose();
        try { _mutex?.ReleaseMutex(); } catch { }
        base.OnExit(e);
    }
}
