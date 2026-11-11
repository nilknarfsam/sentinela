# Histórico de mudanças

## [1.6.2] — Em preparação
### Segurança
- Os links da tela **Sobre e ajuda** passam a ser abertos pelo Explorer da sessão do usuário (`NavegadorSeguro`), para o navegador **não** herdar o privilégio de administrador do Sentinela. No padrão do Windows isso evita a elevação. Com "Iniciar janelas de pasta em processo separado" ligado, UAC desligado ou a conta Administrador embutida, o navegador ainda pode abrir elevado, como antes. Só endereços `https://` são aceitos, e continua valendo a lista fechada de links.

## [1.6.1] — Em preparação
### Novo
- Subaba **Sobre e ajuda** em Ajustes, com versão, apresentação do Sentinela, links exatos para os quatro perfis autorizados e uma explicação curta das nove áreas do app.
- A tela mantém as informações do diário e do executável. Página do Sentinela, relato de problema e informações detalhadas de privacidade aparecem como recursos em breve.

## [1.6.0] — Em preparação
### Melhorado
- O Sentinela usa um identificador genérico de instância única. Antes de abrir esta versão, feche a versão anterior que já esteja em execução; a migração entre versões será tratada pelo instalador da etapa 8.3.
- A pasta de projetos só entra como padrão quando `C:\PROJETOS` existe. Em uma primeira execução sem pastas configuradas, **Otimização → Projetos** orienta a adicioná-las em Ajustes.
- A limpeza do temporário do Windows e a leitura do BitLocker usam caminhos derivados da instalação atual do Windows.

## Em desenvolvimento
### Melhorado
- Nova paleta azul-ardósia, controles com foco de teclado visível, textos auxiliares maiores e navegação lateral rolável.
- Estados de Segurança e Desempenho identificam resultados parciais quando verificações não ficaram disponíveis, sem alterar as notas calculadas.
- Início separa falha na consulta do Defender de proteção desligada; Histórico e Ameaças explicam quando uma lista vazia não confirma ausência de ocorrências.
- Ajustes organizados em abas; Varredura mantém o resultado acessível por rolagem.
- Captura visual pode usar `--captura-largura` e `--captura-altura` para registrar o tamanho padrão ou mínimo sem alongar o painel.

## [1.5.0] - 2026-11-11
### Novo
- Varredura completa semanal, com dia, hora e liga/desliga nos Ajustes. Se perder o horário, espera o PC ficar parado por 10 minutos. Usa o mesmo andamento e botão Cancelar da aba Varredura.
- Relatório da semana no Histórico: varreduras, ameaças e tratamentos, espaço liberado, notas de desempenho e segurança e outros eventos. Escolha a semana passada ou os últimos 7 dias.
- Aviso semanal na bandeja a partir de segunda-feira às 9h, respeitando a opção de avisos de rotina.
- Exportação do relatório em HTML para abrir, imprimir ou salvar como PDF pelo navegador.
### Melhorado
- A rotina semanal começa a contar na instalação ou ao ligar/mudar o horário, evitando uma varredura inesperada.
- Exportação restrita a HTML em pastas locais permitidas, com mensagens específicas e suporte a pastas de nuvem sem aceitar links ou junções.
- Relatório concilia ameaças do diário e do Defender, distingue varreduras concluídas, canceladas e com falha, e inclui tratamentos e restaurações no resumo. O vigia continua sem copiar eventos para o diário.
- A ausência de TPM, que depende da placa-mãe, não reduz mais a nota de segurança.

## [1.4.0] - 2026-11-11
### Novo
- Aba **Segurança** com oito verificações e nota de 0 a 100: firewall, atualizações do Windows, UAC, SmartScreen e apps indesejados, ransomware, segurança do dispositivo, exclusões do Defender e contas locais.
- Botões **Como resolver** abrem a página certa do Windows. O Sentinela só lê as configurações de segurança.
- Exclusões novas do Defender recebem um alerta. Você pode reconhecer a lista após confirmar na tela; isso salva apenas uma referência do Sentinela.
- Cartão **Saúde da segurança** no Início, último resultado salvo e **Verificar segurança** na bandeja.

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
