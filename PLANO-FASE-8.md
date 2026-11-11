# Plano da Fase 8 — Sentinela para a comunidade

Plano para o **Codex** seguir, escrito pelo Claude em 11/11/2026 a pedido do
Franklin. Antes de começar, leia o `AGENTS.md`, que tem as regras de segurança,
de Git e de entrega. Cada etapa abaixo é uma branch, um PR e uma conferência do
Claude antes do merge.

**Objetivo:** qualquer pessoa entra no site do Franklin, baixa o Sentinela de
graça, instala com um instalador normal do Windows e recebe atualizações como
num programa profissional.

---

> **Mudança de 11/11/2026, à tarde:** o Franklin deixou o repositório
> `nilknarfsam/sentinela` **público** e escolheu a licença **GPL-3.0**. Com isso,
> **não existe mais o repositório separado de releases**. Os releases saem no
> próprio `nilknarfsam/sentinela`, sem token pessoal. As etapas abaixo já estão
> atualizadas. Comece pela etapa **8.0** e pelas correções **C1**.

## Decisões já tomadas pelo Franklin (11/11/2026)

| Assunto | Decisão |
| :--- | :--- |
| Código-fonte | **Público** em `nilknarfsam/sentinela` (desde 11/11/2026) |
| Licença | **GPL-3.0 ou posterior** (`GPL-3.0-or-later`). Quem copiar ou modificar precisa manter o código aberto, com a mesma licença e os créditos |
| Downloads e atualizações | **Releases do próprio `nilknarfsam/sentinela`**: o instalador, o `SHA256SUMS` e as notas de versão |
| Assinatura digital | **Depois.** A primeira versão pública sai sem assinatura, e o aviso do SmartScreen é explicado na página. O passo a passo do certificado está na etapa 8.10 |
| Preço | **Grátis** para baixar e usar |
| Anúncios | **Só no futuro**, numa decisão separada (etapa 8.11). Nada de anúncio, rastreio ou coleta de dados agora |
| Ajuda no app | Nome do Franklin, GitHub, site, Instagram e LinkedIn (etapa 8.2) |
| Formato do instalador | MSI com WiX. **Confirmar com o Franklin** antes de adicionar o pacote `WixToolset.Sdk`, porque a regra do projeto é não adicionar pacote sem perguntar |
| .NET | Embutido no instalador (*self-contained*), para funcionar num PC sem .NET |
| Idioma | Português do Brasil |

**Versão:** a primeira versão pública é a **2.0.0**. Ela junta a nova interface
(Fase 7), o instalador e a atualização. Até lá, as etapas sobem a versão
1.6.x normalmente.

---

## Ordem das etapas

| # | Etapa | Quem | Depende de |
| :--- | :--- | :--- | :--- |
| 8.0 | Licença GPL-3.0 e arrumação para o repositório público | Codex | — |
| **C1** | **Correções da conferência do Claude** (`CONFERENCIAS.md`) | Codex | antes de qualquer merge |
| 8.1 | Generalizar o que é do PC do Franklin ✅ (v1.6.0) | Codex | — |
| 8.2 | Tela "Sobre e ajuda" | Codex | 8.1 |
| 8.3 | Instalador MSI (WiX) | Codex | sim do Franklin para o WiX |
| 8.4 | Publicação automática de releases (no próprio repositório) | Codex | 8.3 |
| 8.5 | Sistema de atualização dentro do app | Codex | 8.4 |
| 8.6 | Privacidade e LEIAME público | Codex | 8.5 |
| 8.7 | Página do Sentinela no site | Codex, no repositório do site | 8.4 |
| 8.8 | Texto "Por que o Sentinela existe" | Claude escreve, Codex põe no site | 8.7 |
| 8.9 | Post de divulgação no Instagram | ChatGPT | 8.7 no ar |
| 8.10 | Certificado digital | Franklin decide e compra; Codex integra | depois da 2.0.0 |
| 8.11 | Anúncios (futuro, só planejamento) | Franklin decide | bem depois |
| 8.12 | winget (opcional) | Codex | 8.4 |

---

## 8.0 Licença GPL-3.0 e arrumação para o repositório público
- `LICENSE` na raiz com o **texto oficial e completo da GPL-3.0**, sem alterar
  nada. Pegue de `https://www.gnu.org/licenses/gpl-3.0.txt`, ou com
  `gh api licenses/gpl-3.0 --jq .body`.
- Seção "Licença" no `LEIAME.md`: "Sentinela. Copyright (C) 2026 Franklin
  Carvalho. Este programa é software livre: você pode redistribuí-lo e/ou
  modificá-lo sob os termos da GNU General Public License, versão 3 ou
  posterior. Ele é distribuído SEM NENHUMA GARANTIA. Veja o arquivo LICENSE."
- No `.csproj`: `<Copyright>Copyright (C) 2026 Franklin Carvalho</Copyright>`.
  Na tela "Sobre e ajuda": "Software livre sob a licença GPL-3.0. Sem garantia."
- `LEIAME.md` para quem chega pelo GitHub:
  - o que é o Sentinela e as capturas da interface nova;
  - como baixar (link do último release, quando existir) e como compilar;
  - a privacidade em 3 linhas e o aviso do SmartScreen explicado.

  Os bastidores do PC do Franklin (diagnóstico, Norton, Driver Booster) ficam no
  `PLANO-MELHORIAS.md`, não no LEIAME.
- `CONTRIBUTING.md` curto em português: como relatar problema e como propor
  mudança (PR com testes). Crie também um modelo de *issue* "Relatar um
  problema" em `.github/ISSUE_TEMPLATE/`.
- **Confira** que nada sensível ficou visível. O Claude já conferiu o histórico
  em 11/11: não achou senhas nem tokens, e os commits usam o e-mail `noreply`.
  Repita a busca no estado atual (`git grep` por `token`, `senha`, `password`,
  `@gmail`, `ghp_`, `github_pat_`). Se achar algo, **pare e avise o Franklin**
  antes de qualquer outro passo.
- **Aceite:** o GitHub mostra "GPL-3.0" na página do repositório, e o LEIAME
  explica o projeto para quem nunca ouviu falar dele.

## 8.1 Generalizar o que é do PC do Franklin
- Procure no código e nos testes: `C:\PROJETOS`, `nilkn`, `Franklin`, e-mail,
  `D:\` e caminhos absolutos. Nada pessoal fica fixo no código. A exceção é a
  tela "Sobre e ajuda" (8.2).
- A pasta de projetos da limpeza de desenvolvedor já é configurável nos Ajustes.
  O padrão `C:\PROJETOS` só entra se a pasta existir. Mantenha assim e confira.
- Primeira execução num PC novo: sem pastas configuradas, sem erro e com textos
  que façam sentido para quem nunca viu o app.
- **Aceite:** busca sem nada pessoal, testes verdes e capturas de todas as abas
  sem dados do Franklin.

**Acompanhamento (11/11/2026):** implementação 8.1 feita na branch `fase-8/generalizacao`. Build Release sem avisos/erros, 738 testes aprovados e 10 capturas revisadas em `../REFERENCIAS-INTERFACE/fase-8-1/capturas/` (fora do repositório). Os testes simulam presença e ausência da pasta padrão com verificadores injetados. A etapa aguarda a conferência do Claude antes do merge; não foi testada em PC novo físico, Sandbox ou instalação em unidade diferente de C:.

## 8.2 Tela "Sobre e ajuda"
Nova aba ou seção nos Ajustes (escolha a mais coerente com a interface nova).
Conteúdo:
- **Sentinela**, versão, "Grátis. Usa o Windows Defender como motor."
- **Criado por Franklin Carvalho.** Grafia confirmada pelo Franklin em 11/11/2026.
- Links, abrindo no navegador padrão:
  - GitHub: `https://github.com/nilknarfsam`
  - Site: `https://samfieldsoak.com.br`
  - Instagram: `https://www.instagram.com/samfieldsoak`
  - LinkedIn: **[PREENCHER — pedir o endereço ao Franklin]**
  - Página do Sentinela no site (quando a 8.7 estiver no ar)
  - "Relatar um problema": `https://github.com/nilknarfsam/sentinela/issues/new/choose`
  - "Privacidade" (8.6)
- **Sem e-mail** na tela.
- Os links abrem só por uma **lista branca fechada** de URLs exatas (o mesmo
  padrão do `AlvoPermitido`), com `UseShellExecute = true`. Teste a lista.
- Um parágrafo curto de ajuda: o que cada aba faz, em linguagem de leigo.
- **Aceite:** captura da tela, teste da lista branca e todos os links abrindo
  certo.

## 8.3 Instalador MSI (WiX)
Siga a seção 6 do `AGENTS.md`. Em resumo:
- Projeto novo `Sentinela.Instalador/` (WiX), dentro da solução.
- `dotnet publish` *self-contained* win-x64 → o MSI empacota o resultado.
- Instala por máquina em `C:\Program Files\Sentinela`. Cria:
  - as tarefas `Sentinela` (logon, `--bandeja`, privilégio mais alto) e `Sentinela-Abrir`;
  - o atalho no menu Iniciar;
  - o clique direito "Verificar com o Sentinela".

  É o mesmo que o `instalar-sentinela.ps1` faz hoje: use-o como referência exata.
- `UpgradeCode` fixo. Atualizar por cima (MajorUpgrade) mantém as configurações.
  Instalar uma versão mais velha por cima é bloqueado com mensagem clara.
- Desinstala pelo "Aplicativos" do Windows sem sobra. Os dados em
  `%LOCALAPPDATA%\Sentinela` ficam, e isso é dito na desinstalação.
- Ícone, nome do editor ("Franklin Carvalho") e link de ajuda (8.2) no
  "Aplicativos".
- O `instalar-sentinela.bat` continua por enquanto, marcado no LEIAME como
  "modo antigo".
- **Teste num PC limpo:** use a **Área Restrita do Windows** (Windows Sandbox),
  que vem no Windows 11 Pro. Se não estiver ligada, peça ao Franklin para ligar
  em "Ativar ou desativar recursos do Windows". Teste instalar, abrir, atualizar
  por cima e desinstalar, conferindo as sobras.
- **Aceite:** MSI gerado pelo build; instalação, atualização e desinstalação
  testadas no Sandbox, com capturas.

## 8.4 Publicação automática de releases (no próprio repositório)
Com o repositório público, **não precisa de token pessoal**. O workflow usa o
`GITHUB_TOKEN` do próprio Actions, com `permissions: contents: write`.

Workflow novo `.github/workflows/publicar.yml`:
- Dispara com uma **tag** `v*.*.*`. Quem cria a tag é o Franklin, ou o Codex
  com o "pode" dele naquela vez.
- Confere que a tag bate com a `<Version>` do `.csproj`. Se não bater, falha.
- Compila, roda os testes, gera o MSI (8.3) e gera o `SHA256SUMS`.
- Cria o **Release** em `nilknarfsam/sentinela` com `gh release create`. Anexa o
  MSI e o `SHA256SUMS`, e usa como notas a seção da versão no `CHANGELOG.md`.
- Tag com hífen (por exemplo `v1.7.0-teste`) vira *pre-release*.
- Permissões mínimas: `contents: write` só neste workflow. O `compilar.yml`
  continua só de leitura.
- **Aceite:** um release de teste (`v1.7.0-teste`, *pre-release*) aparece com o
  MSI e o `SHA256SUMS`. Depois o Franklin apaga o release e a tag de teste.

## 8.5 Sistema de atualização dentro do app
Siga **todas** as regras da seção 6 do `AGENTS.md`. Resumo técnico:
- Consulta: `GET https://api.github.com/repos/nilknarfsam/sentinela/releases/latest`.
  Cabeçalhos `User-Agent: Sentinela/<versão>` e `Accept: application/vnd.github+json`.
  Nenhum dado do usuário. Uma vez por dia no máximo, mais o botão "Procurar
  atualizações". Opção nos Ajustes para desligar a consulta automática.
- Ignore *pre-release* e *draft*. Compare a versão (`System.Version`) e só
  ofereça se for **maior**.
- Os downloads só aceitam HTTPS e os hosts do GitHub (`github.com`,
  `objects.githubusercontent.com`, `release-assets.githubusercontent.com`).
  Qualquer outro host é recusado. Tamanho máximo do MSI: 300 MB.
- Mostra "Versão X disponível" com as notas, em texto simples, nunca HTML
  interpretado, e o botão **"Atualizar agora"**. Só instala depois do clique.
- Baixa para `%ProgramData%\Sentinela\Atualizacoes`, criada com permissão **só
  para Administradores e SYSTEM** (ACL explícita, sem herdar). Confere o
  SHA-256 com o `SHA256SUMS` do mesmo release. Quando houver certificado,
  confere também a assinatura (8.10). Se falhar, apaga o arquivo e avisa.
- Instala com `msiexec` (caminho completo em `CaminhosSistema`), usando
  `ArgumentList`: `/i <msi> /passive /norestart /l*v <log>`. O próprio MSI fecha
  e reabre o Sentinela. Tudo é registrado no diário.
- Um ícone ou cartão discreto no Início quando houver versão nova. Aviso na
  bandeja uma vez por versão.
- **Testes:** versão maior, igual e menor; *pre-release* ignorado; JSON
  malformado; host fora da lista; SHA-256 certo e errado; arquivo grande demais;
  sem internet (o app segue normal).
- **Aceite:** com o release de teste da 8.4, a versão anterior instalada no
  Sandbox detecta, baixa, confere e atualiza. Com o `SHA256SUMS` adulterado, ela
  recusa.

## 8.6 Privacidade e LEIAME público
- Texto curto, na tela "Sobre e ajuda", no LEIAME e na
  página do site:
  - o que o Sentinela lê (estado do Defender, discos, programas que abrem com o
    Windows, processos);
  - o que guarda (configurações, diário e notas, **só no PC**, em
    `%LOCALAPPDATA%\Sentinela`);
  - o que envia: **nada**. A única conexão é a consulta de versão nova no GitHub,
    que pode ser desligada.
- Seção "O Sentinela não muda o seu antivírus": ele só lê e pede ações ao
  Defender.

## 8.7 Página do Sentinela no site
- Repositório do site: `nilknarfsam/site-samfieldsoak`. Tem regras próprias:
  leia o `AGENTS.md`/`CLAUDE.md`/docs dele antes. O site é HTML, CSS e JS puro,
  SPA por hash.
- Rota nova `#/sentinela`, com:
  - o que é, em 3 frases;
  - capturas de tela da interface nova;
  - botão **"Baixar grátis"**, apontando para
    `https://github.com/nilknarfsam/sentinela/releases/latest`;
  - o que ele faz e o que ele **não** faz (sem limpador de registro, turbo de RAM
    ou atualizador de drivers);
  - o aviso do SmartScreen ("O Windows protegeu o computador" → "Mais
    informações" → "Executar assim mesmo"), explicando por que aparece;
  - a privacidade (8.6);
  - links: GitHub, Instagram, LinkedIn;
  - requisitos: Windows 10 ou 11, 64 bits.
- Link para a página no menu ou na home do site.
- **Aceite:** página em 390 px e 1280 px, botão de download levando ao release
  certo, sem erro no console.

## 8.8 Texto "Por que o Sentinela existe"
- O Claude escreve e entrega para o Codex colocar no site (seção da página 8.7) e
  no LEIAME.
- Pontos:
  - O antivírus do Windows (Defender) é um dos melhores nos testes
    independentes. Para quem usa o PC em casa, não precisa pagar antivírus.
  - Muito "otimizador de PC" é marketing: limpador de registro, turbo de RAM,
    atualizador de drivers.
  - O que funciona: menos programas abrindo com o Windows, espaço livre, reiniciar
    com atualização pendente, saber o que está pesando, disco saudável.
  - O Sentinela junta só o que funciona, de graça, usando o próprio Defender.

## 8.9 Post de divulgação (Instagram) — para o ChatGPT
- **Pauta no Drive:** documento "PAUTA PARA O CHATGPT – Otimizador de PC e
  antivírus" (pasta Mensagens do IA sem Lábia), **Pauta 3**.
- **Ordem:** sai **depois** dos posts das pautas 1 (otimizadores) e 2 (precisa
  pagar antivírus?), e só quando a página 8.7 estiver no ar.
- Mensagem: "o antivírus do Windows já é muito bom; muito app de 'melhorar o PC'
  é marketing; fizemos um app grátis que junta só o que funciona — baixe no
  site".
- O Codex não faz o post. Ele só confere que o link da página está no ar e avisa
  o Franklin.

## 8.10 Certificado digital (depois da 2.0.0)
Sem certificado, o Windows mostra "O Windows protegeu o computador" (SmartScreen)
para quem baixa. Com certificado, aparece o nome do editor e o aviso some aos
poucos, conforme o programa ganha reputação. Opções verificadas em 11/11/2026
(**confira preços e regras antes de comprar**):

| Opção | Custo aproximado | Serve para o Franklin? |
| :--- | :--- | :--- |
| **SignPath Foundation** (assinatura para código aberto) | **grátis** | **Sim, agora que o código é aberto.** Exige licença aprovada pela OSI (a GPL-3.0 é), projeto ativo e build pelo GitHub Actions. O certificado sai no nome da SignPath Foundation, com o produto "Sentinela". Ver `signpath.org` |
| **Certum Open Source Code Signing** | ~€49/ano (nuvem) a €69 (com cartão e leitor) | **Sim, agora que o código é aberto.** Sai no nome do Franklin ("Open Source Developer"). Exige validar a identidade |
| **Microsoft Artifact Signing** (ex-Trusted Signing) | ~US$ 10/mês | **Hoje não.** Para pessoa física, só nos EUA e no Canadá |
| **Certificado comum de pessoa física** | ~€100 a US$ 300/ano | Sim, mas é o mais caro e não compensa com o código aberto |
| **Sem certificado** | grátis | É o começo: explicar o aviso na página |

**Passos quando o Franklin decidir:**
1. Escolher a opção. Recomendação: depois da 2.0.0, **pedir o SignPath
   Foundation**, que é grátis. Se for recusado ou demorar, usar o **Certum Open
   Source** em nuvem (~€49/ano), que funciona no GitHub Actions.
2. Validar a identidade com o fornecedor (documento com foto, comprovante de
   endereço e às vezes videochamada). Leva de alguns dias a 2 semanas.
3. Guardar as credenciais **só** nos *secrets* do GitHub, nunca em arquivo.
4. O Codex inclui a assinatura do `Sentinela.exe` e do MSI no `publicar.yml`.
   Use `signtool` com carimbo de tempo (*timestamp*).
5. O atualizador (8.5) passa a exigir assinatura válida e do editor esperado,
   além do SHA-256.
6. Atenção: desde 27/02/2026 o certificado vale no máximo **459 dias**. Mesmo
   comprando 2 ou 3 anos, é preciso reemitir.

## 8.11 Anúncios (futuro — não fazer agora)
Fica registrado para uma decisão separada do Franklin. Se um dia entrar:
- Nunca na tela de ameaça, de alerta de proteção ou no meio de uma varredura.
- Sem rastreio e sem enviar dados do PC. Anúncio estático, sem perfil do usuário.
- Opção clara de desligar, ou uma versão sem anúncios.
- Atualizar a página de privacidade **antes**.
- Exige mudar a regra "sem anúncios" do `AGENTS.md`, só com o Franklin.

## 8.12 winget (opcional)
Depois da 2.0.0 estável: manifesto no `microsoft/winget-pkgs` apontando para o
MSI dos releases do `nilknarfsam/sentinela`, para instalar com `winget install Sentinela`.

---

## Aceite final da Fase 8
- Um PC limpo (Sandbox), sem .NET e sem nada do Franklin:
  1. baixa o MSI pela página do site;
  2. instala;
  3. usa;
  4. recebe uma atualização;
  5. desinstala sem sobra.
- A página do site e o LEIAME público explicam o SmartScreen e a privacidade.
- O repositório público tem a licença GPL-3.0, o LEIAME, o CONTRIBUTING e o
  modelo de *issue*, e nenhum segredo.
- O `CHANGELOG`, o `LEIAME`, o `PLANO-MELHORIAS` e este plano estão atualizados.
