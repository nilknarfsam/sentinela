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

## Fase 3: Vigia de inicialização
Programas que se instalam para abrir com o Windows são o jeito mais comum de
vírus e programas inúteis ficarem no PC.

| # | Item | Motor | Tamanho |
| :--- | :--- | :--- | :--- |
| 3.1 | **Ligar e desligar** direto na lista (o mesmo `StartupApproved` que o Gerenciador de Tarefas usa) | Codex | P |
| 3.2 | Mostrar também **tarefas agendadas** e **serviços** que não são da Microsoft | Codex | M |
| 3.3 | **Aviso de item novo:** guardar um retrato da inicialização. Se aparecer algo novo, avisar "Programa X passou a abrir com o Windows" com os botões Manter e Desligar | Codex | M |
| 3.4 | Mostrar quem assinou o programa (editora), e marcar "sem assinatura" em âmbar | Codex | P |

**Aceite:** ao instalar um programa que abre com o Windows, o aviso aparece em até 1
minuto. Desligar pelo Sentinela tem o mesmo efeito que pelo Gerenciador de Tarefas.

## Fase 4: Otimização 2.0 (feita para quem programa)
As maiores sobras no PC do Franklin são de desenvolvimento, não temporários do Windows.

| # | Item | Motor | Tamanho |
| :--- | :--- | :--- | :--- |
| 4.1 | **Limpeza de desenvolvedor:** procurar em `C:\PROJETOS` as pastas `build/`, `.dart_tool/`, `node_modules/` e `bin/obj` de .NET, com tamanho **por projeto**. Marcar o que limpar. Tudo é recriado no próximo build | Codex | M |
| 4.2 | Caches grandes: Gradle (`~\.gradle\caches`), npm, NuGet e Pub, com explicação do custo de apagar (baixa de novo) | Codex | P |
| 4.3 | Cache do Chrome e do Edge, só com o navegador fechado | Codex | P |
| 4.4 | **Maiores pastas do disco:** top 20 de um disco escolhido, para achar o que ocupa espaço | Codex | M |
| 4.5 | Limpeza de componentes do Windows (`DISM /StartComponentCleanup`), com aviso de que demora | Haiku | P |
| 4.6 | Histórico do espaço liberado, vindo do diário | Haiku | P |

**Aceite:** a análise de `C:\PROJETOS` leva menos de 30 segundos. Depois de limpar, o
`flutter build` de um projeto volta a funcionar normalmente.

## Fase 5: Desempenho
| # | Item | Motor | Tamanho |
| :--- | :--- | :--- | :--- |
| 5.1 | Gráfico de CPU e memória da **última hora**, coletado só com a janela aberta ou em baixa frequência | Codex | M |
| 5.2 | **Top 10 processos** por CPU e memória, com o botão Finalizar (confirma, e nunca em processos do sistema) | Codex | M |
| 5.3 | **Saúde dos discos:** estado (`MSFT_PhysicalDisk`), desgaste do SSD e temperatura (`Get-StorageReliabilityCounter`, precisa de admin) | Codex | M |
| 5.4 | Lembrete **"Reinicie o PC"** quando ele estiver ligado há mais de 7 dias com atualização pendente | Haiku | P |

## Fase 6: Rotinas e relatórios
| # | Item | Motor | Tamanho |
| :--- | :--- | :--- | :--- |
| 6.1 | **Varredura completa semanal** por tarefa agendada própria, no dia e hora dos Ajustes. Não usa o agendador do Defender, para não mexer na configuração dele | Codex | P |
| 6.2 | **Relatório semanal:** aviso com o resumo da semana (varreduras, ameaças, espaço liberado, nota de saúde) e uma página com o detalhe | Codex | M |
| 6.3 | Exportar o relatório em HTML ou PDF | Codex | P |

## Fase 7: Acabamento e distribuição
| # | Item | Motor | Tamanho |
| :--- | :--- | :--- | :--- |
| 7.1 | **Tema claro**, seguindo o tema do Windows | Codex | M |
| 7.2 | Navegação por teclado e leitor de tela (nomes acessíveis) | Codex | P |
| 7.3 | **Releases no GitHub** com o `.exe` pronto, e o Sentinela avisando quando sai versão nova. *O repositório é privado: precisaria de token ou de torná-lo público. Decidir antes* | Codex | M |
| 7.4 | Instalador num arquivo só, com desinstalação em Aplicativos do Windows | Codex | M |

---

## Ordem recomendada
1. **Instalar a v1.0 e usar uns dias.** O uso real mostra o que incomoda mais.
2. **Fase 0:** sem ela, as outras ficam frágeis.
3. **Fase 1:** maior ganho de segurança, porque é o aviso na hora.
4. **Fase 3:** segurança e leveza ao mesmo tempo.
5. **Fase 4:** libera gigas de espaço no seu caso.
6. **Fase 2**, depois **5**, **6** e **7**.

Tamanhos: **P** cabe numa tarefa, **M** em 2 ou 3 tarefas com revisão entre elas, **G** pede plano do Codex (`modo="plan"`) antes.

## O que o Sentinela **não** vai ter
- **Motor de antivírus próprio.** O Defender já é um dos melhores nos testes independentes.
- **Botões que mudam a configuração do Defender.** É uma regra de segurança do projeto.
- **VPN, cofre de senhas e "limpador de RAM".** O cofre fica para o Bitwarden, pendente no
  plano geral (`../PLANO-PERFORMANCE-E-IA.md`, Passo 8).

## Pendências fora do Sentinela
- ⏸ **Bitwarden:** avaliar e migrar o cofre do Norton. Pendente, sem data. Passo a passo no
  plano geral, Passo 8.
