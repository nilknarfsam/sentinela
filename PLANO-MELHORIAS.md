# Sentinela: plano de melhorias

Escrito em 09/11/2026. Os itens marcados ✅ já existem na versão 1.0.

## A ideia em uma frase
O Sentinela é a **janela do Franklin para a segurança e a saúde do PC**. O motor
é o Windows Defender, e o Sentinela mostra, avisa na hora e facilita as ações.
Ele cresce em três direções: **avisar sozinho**, **mostrar a saúde inteira do PC** e
**manter o PC leve**, sem nunca virar um programa pesado como o Norton.

## Regras que valem para todas as fases
- **Não mexer nas configurações do Defender, do firewall ou do UAC.** Quando algo
  estiver desligado, o Sentinela mostra e abre a página certa da Segurança do
  Windows. Quem liga é o Franklin.
- **Leve:** menos de 60 MB de RAM parado na bandeja, abrir em menos de 1 segundo e
  quase 0% de CPU quando a janela estiver fechada.
- **Nada de "milagre":** nada de limpador de RAM, limpador de registro ou "turbo".
  Isso não ajuda ou até atrapalha.
- **Sem pacote novo** sem perguntar. Se uma fase precisar, o pedido vai antes.
- Cada fase termina com: compilar, captura de cada aba mexida, revisão do Sonnet,
  commit numa branch e merge na `main`.

## O que existe hoje (v1.0) e o que melhorar já

| Área | Hoje | Limitação |
| :--- | :--- | :--- |
| Status do Defender | ✅ Lido a cada 20 s | Atualiza devagar e não avisa sozinho quando algo muda |
| Varredura rápida e completa | ✅ Com cancelar e aviso no fim | Barra sem porcentagem e sem varredura de pasta escolhida |
| Ameaças e quarentena | ✅ Histórico e quarentena | Quarentena em texto cru, sem restaurar nem excluir |
| Otimização | ✅ 5 itens com confirmação | Não pega as pastas de desenvolvimento, que são as maiores |
| Inicialização | ✅ Lista do registro e da pasta Inicializar | Só leitura, sem tarefas agendadas e sem aviso de item novo |
| CPU, memória e discos | ✅ Ao vivo | Sem histórico, processos ou saúde do disco |
| Bandeja | ✅ Escudo com menu | O ícone não muda de cor quando há problema |
| Timers | ✅ | **Continuam rodando com a janela fechada**, gastando CPU à toa |

---

## Fase 0: Fundação (antes de crescer)
**✅ Feita em 09/11/2026 (v1.1.0).** Inclui os testes automáticos (79, com xUnit).

Deixa a base pronta para as próximas fases não virarem bagunça.

| # | Item | Motor | Tamanho |
| :--- | :--- | :--- | :--- |
| 0.1 | **Pausar os timers com a janela fechada.** Na bandeja, ler o Defender a cada 60 s e não ler CPU nem RAM | Haiku | P |
| 0.2 | **Ícone da bandeja com cor:** verde (ok), âmbar (atenção) ou vermelho (proteção desligada). Três `.ico` gerados como o atual | Codex | P |
| 0.3 | **Configurações** em `%APPDATA%\Sentinela\config.json`: iniciar com o Windows, avisos, dia da varredura semanal. Serviço `Configuracoes.cs` e aba **Ajustes** | Codex | M |
| 0.4 | **Diário do Sentinela** em `%LOCALAPPDATA%\Sentinela\diario.jsonl`, uma linha por ação: varredura, limpeza, ameaça ou alerta. Base para relatórios | Codex | P |
| 0.5 | **Projeto de testes** (xUnit) para o que tem lógica pura: leitura da quarentena, `StartupApproved`, formatar tamanhos, regras de limpeza. *Precisa de pacote: pedir antes* | Codex | M |
| 0.6 | **GitHub Actions:** compilar e testar a cada push, e gerar o `Sentinela.exe` como artefato | Codex | P |
| 0.7 | **Versão na tela** (rodapé do menu) e `CHANGELOG.md` | Haiku | P |
| 0.8 | Separar o `MainWindow.xaml.cs` (400 linhas) em **uma página por aba** (`Paginas/Inicio.xaml` ...), sem mudar o visual | Codex | G |

**Aceite:** com a janela fechada, o Sentinela fica perto de 0% de CPU no Gerenciador de
Tarefas. O ícone da bandeja muda de cor. As configurações persistem depois de
reiniciar. O GitHub Actions passa.

## Fase 1: Proteção que avisa na hora
**✅ Feita em 09/11/2026 (v1.2.0).** Mudou em relação ao plano:
- A quarentena não tem "Excluir" por item, porque o Windows não oferece esse comando. O botão abre a Segurança do Windows.
- O andamento da varredura é uma estimativa pelas últimas varreduras, porque o Defender não informa a porcentagem.

O teste com o EICAR passou: o arquivo foi detectado, posto em quarentena e apareceu no Histórico.

É o que mais aproxima o Sentinela do Norton, só que leve. Hoje ele só olha
quando alguém abre a janela.

| # | Item | Motor | Tamanho |
| :--- | :--- | :--- | :--- |
| 1.1 | **Vigia de eventos do Defender.** Escutar o log `Microsoft-Windows-Windows Defender/Operational` (`EventLogWatcher`, sem polling). Aviso **na hora** para: ameaça detectada (1116), ameaça tratada (1117), proteção em tempo real desligada (5001), varredura terminada (1001), falha de atualização (2001) | Codex | M |
| 1.2 | **Linha do tempo:** página com os últimos 30 dias de eventos do Defender e do diário, filtrável por tipo | Codex | M |
| 1.3 | **Varredura personalizada:** escolher pasta ou arquivo, ou arrastar para a janela (`MpCmdRun -Scan -ScanType 3 -File`) | Codex | P |
| 1.4 | **"Verificar com o Sentinela"** no clique direito do Explorer. O instalador cria a entrada em `HKCR\*\shell` e `HKCR\Directory\shell`, e o desinstalador remove | Codex | P |
| 1.5 | **Quarentena de verdade:** lista com nome, arquivo e data. Botões **Restaurar** (aviso forte, dupla confirmação) e **Excluir de vez** | Codex | M |
| 1.6 | **Varredura offline** (`Start-MpWDOScan`) para vírus que se escondem do Windows. Botão com aviso de que o PC reinicia e leva uns 15 minutos | Haiku | P |
| 1.7 | **Andamento da varredura:** mostrar a pasta sendo verificada e a contagem, se o log de eventos permitir. Senão, manter a barra animada | Codex | M |

**Aceite:** com o arquivo de teste EICAR (inofensivo, feito para testar
antivírus), o aviso aparece em menos de 5 segundos, mesmo com a janela fechada.
Ele entra na linha do tempo e na quarentena e pode ser excluído por ali.

## Fase 2: Saúde da segurança (nota de 0 a 100)
**✅ Feita em 11/11/2026 (v1.4.0).** Oito verificações, nota, referência de exclusões com confirmação, cartão no Início e comando na bandeja.

Mudou em relação ao plano original:
- Firewall usa o estado efetivo por COM, com registro como alternativa.
- Inicialização segura usa o registro; TPM e BitLocker usam WMI, sem PowerShell.
- O serviço se chama `Servicos/Seguranca/Seguranca.cs` e compartilha o motor do Checkup.

Uma página que responde: "meu PC está bem configurado?". Tudo **só leitura**, com
o botão "Como resolver" abrindo a página certa do Windows.

| # | Verificação | Como ler |
| :--- | :--- | :--- |
| 2.1 | Firewall ligado nos 3 perfis | WMI `MSFT_NetFirewallProfile` |
| 2.2 | Windows Update: última instalação, se há reinício pendente, se está pausado | API do Windows Update e registro |
| 2.3 | UAC ligado | Registro `EnableLUA` |
| 2.4 | SmartScreen e bloqueio de apps potencialmente indesejados (PUA) | `MSFT_MpPreference` e registro |
| 2.5 | Acesso controlado a pastas (proteção contra ransomware) | `MSFT_MpPreference` |
| 2.6 | Secure Boot, TPM e BitLocker do disco C: | `Confirm-SecureBootUEFI`, WMI TPM e BitLocker |
| 2.7 | **Exclusões do Defender.** Vírus costumam adicionar uma para se esconder. Alerta se aparecer alguma nova | `MSFT_MpPreference.ExclusionPath` |
| 2.8 | Conta de administrador sem senha, e a conta Convidado | WMI `Win32_UserAccount` |

- Nota de 0 a 100 com peso por item, no card do Início.
- **Motor:** Codex escreve o serviço `Saude.cs` e a página; o Sonnet confere se nada escreve no sistema.

**Aceite:** a página carrega em menos de 2 segundos e cada item tem o botão de
solução. Com o firewall desligado num perfil, a nota cai e o item fica vermelho.

## Fase 3: Checkup de desempenho
**✅ Feita em 10/11/2026 (v1.3.0).** As seis etapas, 3A a 3F, foram entregues e revisadas.

Mudou em relação ao plano:
- A revisão de segurança levou a três proteções novas:
  - `ArquivosSeguros`: o app, rodando como admin, nunca segue links nas pastas do usuário;
  - `CaminhosSistema`: executáveis do Windows sempre pelo caminho completo;
  - uma trava contra apagar pastas importantes.
- A limpeza de temporários deixou de usar a variável TEMP, um risco que existia desde a 1.0.
- O checkup completo leva cerca de 2 segundos.

Pedido do Franklin em 10/11/2026: um "modo deixar o PC mais rápido", no lugar de
programas como Driver Booster e CCleaner, só com o que funciona de verdade.
Esta fase junta as antigas Fases 3 (vigia de inicialização), 4 (otimização) e
5 (desempenho).

**O que fica de fora**, por ser marketing ou arriscado:
- atualizador automático de drivers;
- limpador de registro;
- "turbo de RAM";
- desfragmentar SSD;
- desligar serviços do Windows sem saber o que fazem.

### Como fica para o Franklin
Uma aba nova, **Desempenho**, com o botão **Verificar o PC**. Em até 1 minuto ela mostra:
- uma **nota de 0 a 100**;
- uma lista de cartões, um para cada verificação. Cada cartão traz o estado (ok,
  atenção ou problema) e uma explicação em português simples.

Quando dá para resolver, o cartão tem o botão **Resolver**, que sempre pede
confirmação. Nada muda sozinho.

### Arquitetura
- **`Servicos/Checkup/`:**
  - a interface `IVerificacao` (Nome, Peso, `Verificar(CancellationToken)`);
  - o record `ResultadoVerificacao` (Nome, Nivel Ok/Atencao/Problema, Resumo,
    Detalhe e uma ação opcional de resolver).
- **`Checkup.Executar()`:**
  - roda as verificações em paralelo, com tempo limite por item, para uma lenta
    não travar as outras;
  - calcula a nota;
  - grava no diário (tipo `checkup`).
- **Regras:**
  - as regras de cada verificação são funções puras e testáveis;
  - a leitura do sistema fica separada e nunca lança exceção;
  - as ações de "Resolver" ficam à parte e sempre confirmam antes.

### Etapas
| Etapa | O que entra | Motor | Aceite |
| :--- | :--- | :--- | :--- |
| **3A Base** | Aba Desempenho, `IVerificacao`, nota e cartões. Verificações leves: **reinício pendente** e dias ligado, **espaço livre** nos discos, **drivers com problema** (dispositivo com erro no Gerenciador de Dispositivos), **plano de energia** ativo (só leitura), **quantos programas** abrem com o Windows | Codex | Checkup completo em menos de 10 s. Testes das regras e da nota |
| **3B Inicialização** | **Ligar e desligar** pelo `StartupApproved`, **tarefas agendadas** e **serviços** de terceiros, **editora e assinatura** de cada programa, **aviso de item novo** (retrato da inicialização). Cartão: "N programas abrem com o Windows" → Resolver abre a lista | Codex | Desligar pelo Sentinela = desligar pelo Gerenciador de Tarefas. Aviso de item novo em até 1 minuto |
| **3C Espaço** | **Limpeza de desenvolvedor** por projeto em `C:\PROJETOS` (`build`, `.dart_tool`, `node_modules`, `bin/obj`), caches (Gradle, npm, NuGet, Pub), cache de navegadores fechados, **maiores pastas** do disco, histórico do espaço liberado. Cartão: "Dá para liberar X GB" | Codex | Análise de `C:\PROJETOS` em menos de 30 s. `flutter build` continua funcionando depois de limpar. Nunca segue junctions |
| **3D Processos** | **Top 10 processos** por CPU e memória (amostra de 2 s), **Finalizar** com confirmação e nunca em processo do sistema. Gráfico da última hora só com a janela aberta. Cartão: "Programa X está usando 40% da CPU" | Codex | Lista em menos de 3 s. Finalizar recusa processos protegidos |
| **3E Disco** | **Saúde do SSD/HD**: estado, desgaste, temperatura e erros (precisa de admin). Limpeza de componentes do Windows (`DISM /StartComponentCleanup`) como ação opcional, com aviso de que demora | Codex | Mostra desgaste e temperatura do SSD deste PC. Sem admin, explica o que falta |
| **3F Entrega** | Nota do Checkup no Início, "Verificar desempenho" no menu da bandeja, filtro **Desempenho** no Histórico, lembrete de reinício, CHANGELOG, LEIAME, versão **1.3.0** | Haiku + Codex | Build, testes, capturas, GitHub Actions verde, merge na `main` |

Cada etapa segue o fluxo do projeto:
1. A branch `fase-3` recebe o pedido detalhado.
2. O **Codex** escreve o código.
3. O **Sonnet** revisa.
4. O **Opus** compila, roda os testes, confere as capturas e faz o commit.

O lançamento da 1.3.0 acontece no fim da 3F.

## Fase 6: Rotinas e relatórios
**✅ Feita em 11/11/2026 (v1.5.0).** Rotina semanal nos Ajustes, relatório no Histórico, aviso semanal e exportação em HTML. Ausência de TPM não reduz a nota.

Mudou em relação ao plano original:
- A rotina roda dentro do Sentinela, inclusive na bandeja, a cada 5 minutos. Não cria tarefa agendada; recupera o horário perdido após 10 minutos de ociosidade. Na primeira execução e ao ligar/mudar o horário, usa o momento atual como referência.
- Relatório combina o diário com detecções do Defender no período, deduplicadas por nome em até dois minutos, sem gravar os eventos do vigia no diário. Resultados novos de varreduras têm campos estruturados, com leitura compatível do diário antigo.
- Exportação aceita somente .html/.htm em unidade local, fora da raiz e das pastas protegidas. Identifica tags de nuvem e recusa links/junções.
- Exporta HTML autocontido para imprimir ou salvar como PDF pelo navegador. PDF nativo fica fora para não adicionar pacote NuGet.

| # | Item | Motor | Tamanho |
| :--- | :--- | :--- | :--- |
| 6.1 | **Varredura completa semanal** dentro do Sentinela, no dia e hora dos Ajustes, com andamento e cancelamento na aba Varredura | Codex | P |
| 6.2 | **Relatório semanal:** aviso com o resumo da semana (varreduras, ameaças, espaço liberado, nota de saúde) e uma página com o detalhe | Codex | M |
| 6.3 | Exportar o relatório em HTML; impressão e PDF pelo navegador | Codex | P |

## Fase 7: Acabamento e distribuição
| # | Item | Motor | Tamanho |
| :--- | :--- | :--- | :--- |
| 7.1 | **Tema claro**, seguindo o tema do Windows | Codex | M |
| 7.2 | Navegação por teclado e leitor de tela (nomes acessíveis) | Codex | P |
| 7.3 | *(movido para a Fase 8)* **Releases no GitHub** com o `.exe` pronto, e o Sentinela avisando quando sai versão nova. *O repositório é privado: precisaria de token ou de torná-lo público. Decidir antes* | Codex | M |
| 7.4 | *(movido para a Fase 8)* Instalador num arquivo só, com desinstalação em Aplicativos do Windows | Codex | M |

**Acompanhamento do acabamento (11/11/2026):** a nova interface foi implementada localmente nas nove áreas, com base nas nove propostas finais. Inclui estilos compartilhados, indicação de resultados parciais, Ajustes em abas e captura visual com dimensões selecionáveis. O build Release passou sem avisos ou erros e os 720 testes passaram; `TEMP` e `TMP` foram definidos apenas no processo de teste, para uma pasta gravável do workspace.

**Pendente:** validar interativamente teclado, foco e seleção, além das escalas nativas de DPI de 125% e 150%. A Fase 7 continua em andamento; esta entrega não conclui todos os itens da fase. Não foram feitos instalação, publicação ou merge.

## Fase 8: Produto e distribuição (para a comunidade)
Ideia do Franklin em 10/11/2026: quando o Sentinela estiver maduro, distribuí-lo
de graça pelo GitHub e pelo site dele. Junto, explicar o raciocínio: por que o
Defender basta e o que é marketing nos "otimizadores" de PC.

**Quando começar:** depois das Fases 3 e 2, com o Sentinela usado por algumas semanas
no PC do Franklin sem problemas.

### Decisões antes de começar (do Franklin)
| Decisão | Opções | Recomendação |
| :--- | :--- | :--- |
| Repositório | continuar privado e só publicar o instalador, ou abrir o código | **Abrir o código** (licença MIT). Gera confiança: qualquer um pode conferir que o Sentinela não espiona nem muda o Defender |
| Assinatura digital do instalador | sem assinatura (o SmartScreen avisa "aplicativo não reconhecido"), certificado comum (~R$ 1–2 mil/ano) ou assinatura via Azure Trusted Signing (mais barato, exige verificação de identidade) | Começar **sem assinatura**, explicando o aviso na página, e assinar quando houver usuários |
| Formato do instalador | **MSI** (WiX Toolset), **Inno Setup** (.exe) ou **MSIX** | **MSI com WiX**: padrão das empresas, aparece em "Aplicativos" do Windows, desinstala limpo e o `winget` aceita |
| .NET | exigir o .NET 9 Desktop Runtime instalado ou embutir no instalador | **Embutir** (self-contained). Fica maior (~70 MB) mas funciona em qualquer PC |
| Idioma | só português ou português + inglês | Português primeiro e inglês depois (os textos já ficam centralizados para isso) |

### Etapas
| # | Item | Motor |
| :--- | :--- | :--- |
| 8.1 | **Generalizar o que é do PC do Franklin:** a pasta `C:\PROJETOS` da limpeza de desenvolvedor vira configurável nos Ajustes; nada de nome, e-mail ou caminho pessoal no código | Codex |
| 8.2 | **Instalador MSI (WiX):** instala em Program Files, cria as tarefas agendadas, o atalho e o clique direito, e desinstala tudo pelo "Aplicativos" do Windows. Substitui o `instalar-sentinela.bat` | Codex |
| 8.3 | **Releases no GitHub:** o GitHub Actions gera o MSI e o anexa a cada versão (tag `v1.x.y`), com o CHANGELOG como descrição | Codex |
| 8.4 | **Aviso de versão nova** dentro do Sentinela (consulta a página de releases, sem enviar nada do usuário) | Codex |
| 8.5 | **Privacidade:** página curta dizendo o que o Sentinela lê, o que guarda (só no PC) e que não envia nada para a internet | Opus |
| 8.6 | **Licença e contribuição:** LICENSE (MIT), CONTRIBUTING, modelo de issue para relatar problema | Haiku |
| 8.7 | **Página do Sentinela no site do Franklin:** o que é, download, capturas de tela e o aviso do SmartScreen explicado | Opus + Codex |
| 8.8 | **Texto "Por que o Sentinela existe":** o Defender é um dos melhores antivírus nos testes independentes; o que é marketing (atualizador de drivers, limpador de registro, turbo de RAM) e o que funciona de verdade. Vira página do site, seção do LEIAME e base dos posts do Instagram (Passo 10 do plano geral) | Opus escreve, ChatGPT adapta para os posts |
| 8.9 | **winget** (opcional): publicar no repositório do `winget` para instalar com `winget install Sentinela` | Codex |

**Aceite:**
- Um PC limpo, sem .NET e sem nada do Franklin, instala pelo MSI baixado do GitHub, usa e desinstala sem deixar sobra.
- A página do site e o LEIAME explicam o aviso do SmartScreen e a privacidade.

---

## Ordem recomendada
1. ✅ **Fase 0:** fundação (v1.1.0).
2. ✅ **Fase 1:** aviso na hora (v1.2.0).
3. ✅ **Fase 3:** Checkup de desempenho (v1.3.0).
4. **Fase 2:** saúde da segurança.
5. **Fases 6 e 7:** rotinas, relatórios e acabamento.
6. **Fase 8:** produto e distribuição para a comunidade (MSI, GitHub, site).

Tamanhos: **P** cabe numa tarefa, **M** em 2 ou 3 tarefas com revisão entre elas, **G** pede plano do Codex (`modo="plan"`) antes.

## O que o Sentinela **não** vai ter
- **Motor de antivírus próprio.** O Defender já é um dos melhores nos testes independentes.
- **Botões que mudam a configuração do Defender.** É uma regra de segurança do projeto.
- **VPN, cofre de senhas e "limpador de RAM".** O cofre fica para o Bitwarden, pendente no
  plano geral (`../PLANO-PERFORMANCE-E-IA.md`, Passo 8).

## Pendências fora do Sentinela
Os detalhes de cada uma estão no plano geral, `../PLANO-PERFORMANCE-E-IA.md`.
- ⏸ **Bitwarden:** avaliar e migrar o cofre do Norton (Passo 8).
- ⏸ **Driver Booster (IObit):** está instalado e não é necessário, porque nenhum
  dispositivo tem problema de driver. Recomendação: o Franklin desinstalar
  (Passo 6).
- ⏸ **NVIDIA:** rever o driver e as configurações para jogos e IA local, depois
  da Fase 3 (Passo 9).
- ⏸ **Post para Instagram** (IA sem Lábia ou perfil de tecnologia) sobre "o que é
  marketing nos otimizadores de PC", para o ChatGPT fazer depois da Fase 3
  (Passo 10).
