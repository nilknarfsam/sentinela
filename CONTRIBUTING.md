# Como contribuir com o Sentinela

Obrigado pelo interesse! O Sentinela é software livre (GPL-3.0 ou posterior).
Tudo aqui é em **português do Brasil**: código, mensagens e documentação.

## Relatar um problema
Abra uma [issue](https://github.com/nilknarfsam/sentinela/issues/new/choose) com:
- a versão do Sentinela (em **Ajustes → Sobre e ajuda**) e do Windows;
- o que você fez, o que esperava e o que aconteceu;
- se possível, uma captura de tela.

**Não cole** dados pessoais, senhas nem o conteúdo inteiro do diário. Se for uma
**falha de segurança**, não descreva a falha numa issue pública: use a aba
[Security → Report a vulnerability](https://github.com/nilknarfsam/sentinela/security/advisories/new).
Se a opção não aparecer, abra uma issue dizendo só "encontrei um problema de
segurança", sem detalhes, e o criador entra em contato.

## Propor uma mudança
1. Leia o [AGENTS.md](AGENTS.md). Ele tem as regras de segurança e de código,
   que valem para pessoas e para IAs.
2. Crie uma branch a partir da `main` e faça mudanças pequenas e focadas.
3. Antes de abrir o Pull Request, confira:
   ```
   cd Sentinela
   dotnet build -c Release      # zero avisos
   cd ..
   dotnet test                  # todos verdes
   ```
4. No PR, descreva o que mudou, como testou e o que não testou.

## O que não aceitamos
- Qualquer coisa que desligue, enfraqueça ou mude o Defender, o firewall ou o
  Controle de Conta de Usuário.
- Telemetria, coleta de dados ou qualquer conexão de rede sem pedido explícito do usuário.
- "Limpador de registro", "turbo de RAM" e atualizador automático de drivers.
- Pacotes ou código de terceiros com licença incompatível com a GPL-3.0.

Ao contribuir, você concorda que a sua contribuição será distribuída sob a
GPL-3.0 ou posterior.
