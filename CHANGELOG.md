# Histórico de mudanças

## [1.3.0] - 2026-11-10
### Novo
- Aba **Desempenho** com **Checkup**: nota de 0 a 100 e cartões com o que deixa o PC lento, cada um com um botão para resolver (sempre com confirmação). Verifica reinício pendente, espaço livre, drivers com problema, plano de energia, programas que abrem com o Windows, espaço que dá para liberar, o que está pesando agora e a saúde dos discos.
- **O que está pesando:** os 10 programas que mais usam CPU e memória, com **Finalizar** (processos do Windows ficam protegidos) e gráfico da última hora.
- **Saúde dos discos:** estado de cada SSD/HD e, como administrador, desgaste, temperatura e erros.
- **Inicialização completa:** ligar e desligar programas direto no Sentinela (igual ao Gerenciador de Tarefas), editora e assinatura digital de cada programa, tarefas agendadas e serviços de terceiros, e **aviso quando um programa novo passa a abrir com o Windows**.
- **Otimização 2.0:** limpeza das sobras de build dos seus projetos (Flutter, Node, .NET, Gradle), caches de desenvolvimento, cache dos navegadores (fechados), maiores pastas do disco e limpeza de componentes antigos do Windows.
- Nota do último Checkup no Início, "Verificar desempenho" no menu da bandeja, filtro Desempenho no Histórico e lembrete de reinício quando há atualização esperando.
### Segurança
- Executáveis do Windows sempre chamados pelo caminho completo.
- Arquivos do Sentinela nas pastas do usuário lidos e gravados sem seguir atalhos de pasta (links).
- Trava contra apagar pastas importantes; a limpeza de temporários não depende mais da variável TEMP (risco que existia desde a 1.0).
### Fora de propósito
- O Sentinela **não** atualiza drivers sozinho, não "limpa registro" nem "turbina a RAM": isso é marketing e às vezes atrapalha.

## [1.2.0] - 2026-11-09
### Novo
- **Aviso na hora:** o Sentinela escuta o próprio Windows Defender e avisa em segundos quando uma ameaça é encontrada, quando a proteção em tempo real é desligada ou quando alguém adiciona uma exclusão ou desliga uma proteção (mesmo com a janela fechada).
- Página **Histórico**: eventos do Defender e do Sentinela dos últimos 30 dias, com filtros.
- **Verificar pasta ou arquivo** escolhido (e arrastar para a janela quando não está como administrador).
- **"Verificar com o Sentinela"** no clique direito do Explorer (no Windows 11, em "Mostrar mais opções"). Requer rodar o instalador de novo.
- **Quarentena em lista**, com **Restaurar** (dupla confirmação) e atalho para excluir na Segurança do Windows.
- **Varredura offline** (avançado): reinicia o PC e verifica antes do Windows carregar.
- Varreduras rápida e completa mostram uma **estimativa de tempo** baseada nas últimas.
### Melhorado
- Abrir o Sentinela quando ele já está rodando como administrador agora mostra a janela, em vez de uma mensagem.
- Avisos agrupados (no máximo um a cada 5 segundos), sem esconder o mais grave.

## [1.1.0] - 2026-11-09
### Novo
- Aba **Ajustes**: iniciar com o Windows, manter na bandeja ao fechar, avisos de rotina, diário e versão.
- Ícone da bandeja muda de cor: verde (protegido), âmbar (atenção) e vermelho (proteção desligada), com aviso quando piora.
- Diário do Sentinela em `%LOCALAPPDATA%\Sentinela\diario.jsonl` (varreduras, limpezas, ameaças, alertas).
- Versão no rodapé do menu.
### Melhorado
- Com a janela fechada, o Sentinela não lê CPU e memória e confere o Defender só a cada 60 s.
- Cada aba virou uma página separada (código mais fácil de manter).

## [1.0.0] - 2026-11-09
### Novo
- Primeira versão (antes chamada Painel do PC): status do Windows Defender, varredura rápida e completa, ameaças e quarentena, otimização, programas de inicialização, ícone na bandeja e instalador.
