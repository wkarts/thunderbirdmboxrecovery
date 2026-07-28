# Thunderbird Recovery Suite 2.1

Aplicação Windows portátil para explorar, diagnosticar, reparar, extrair, indexar, fazer backup e restaurar caixas MBOX e perfis do Mozilla Thunderbird.

## Desenvolvedor

- **WWSoftware's Sistemas e Tecnologias**
- **Wallace Kleiton**
- GitHub: **@wkarts**
- WhatsApp: **+55 75 98844-9231**
- E-mail: **wkarts@gmail.com**

A logomarca incorporada ao splash screen e à janela Sobre identifica o desenvolvedor.

## Principais módulos

### Explorar MBOX

- Abre `Inbox`, `Sent`, `Drafts`, `Trash`, `Archives`, pastas personalizadas e arquivos `.mbox`.
- Aceita arquivos descompactados sem extensão ou entradas dentro de arquivos compactados.
- Lista número, data, remetente, destinatário, assunto, status, anexo, tamanho, Message-ID e offset.
- Permite seleção múltipla na grade.
- Extrai uma mensagem selecionada, várias selecionadas ou todas as mensagens para EML.
- Exporta inventário em CSV e JSON.

### Diagnosticar e reparar

- Processamento em fluxo para caixas grandes.
- Detecção de estrutura MBOX, cabeçalhos, separadores, status Mozilla e mensagens excluídas.
- Reconstrução em arquivo único por padrão.
- Fracionamento opcional somente entre mensagens completas.
- Recuperação opcional de mensagens `Expunged` e `IMAPDeleted` ainda presentes fisicamente.
- Normalização de `X-Mozilla-Status` e `X-Mozilla-Status2`.
- Logs, manifestos e SHA-256.

### Indexar MSF

- Detecta Thunderbird instalado, versão e arquitetura.
- Cria perfil temporário isolado.
- Solicita ao próprio Thunderbird a geração do índice `.msf`.
- Valida estrutura Mork e acompanha a contagem de mensagens.
- Mantém fallback prospectivo para Panorama/SQLite.

### Backup

- Completo, somente mensagens ou seletivo.
- Formatos:
  - ZIP, para maior compatibilidade;
  - 7Z/LZMA2, para maior compressão.
- SHA-256 por arquivo e do pacote final.
- Manifesto interno.
- Bloqueio de perfil aberto por padrão.

### Restauração

- Completa, somente mensagens ou seletiva.
- Suporta ZIP, 7Z e demais formatos de leitura aceitos pela biblioteca.
- Validação de hashes e proteção contra path traversal.
- Restauração sobre perfil vazio ou existente.
- Sobre perfil existente exige:
  - confirmação de compreensão dos riscos;
  - digitação de `RESTAURAR`;
  - confirmação crítica adicional;
  - confirmação extra quando o backup de segurança estiver desativado.
- Backup de segurança opcional em ZIP ou 7Z antes da alteração.
- Sobrescrita opcional; arquivos fora do backup não são apagados automaticamente.

## Interface institucional

- Splash screen com versão, arquitetura e identificação do desenvolvedor.
- Janela Sobre com logomarca, versão e contatos.
- Botões para GitHub, WhatsApp, e-mail e cópia dos contatos.

## Artefatos de release

Para cada arquitetura `win-x86` e `win-x64`, o workflow gera:

- executável portátil self-contained;
- pacote ZIP;
- pacote 7Z;
- executável menor `runtime-required`, que exige o .NET 8 Desktop Runtime;
- pacotes ZIP e 7Z da variante `runtime-required`;
- hashes SHA-256.

A compactação UPX existe como opção no script, mas permanece desativada por padrão porque executáveis .NET self-contained podem apresentar incompatibilidades ou alertas de antivírus após esse tipo de empacotamento.

## Compilação local

Requisitos:

- Windows 10 ou superior;
- PowerShell 7;
- SDK .NET 8;
- 7-Zip para gerar pacotes `.7z`.

```powershell
pwsh ./scripts/Publish-Portable.ps1 -Version 2.1.0
```

Para tentar gerar uma variante UPX quando o `upx.exe` já estiver instalado:

```powershell
pwsh ./scripts/Publish-Portable.ps1 -Version 2.1.0 -EnableUpx $true
```

## CI e releases

- Pull Requests: sintaxe PowerShell, restore, build, smoke tests e validação de grafos x86/x64.
- Merge em `main` ou `master`: cria release nova e imutável `v2.1.<GITHUB_RUN_NUMBER>`.
- Não utiliza release `continuous`.
- Não move tags antigas.
- Não substitui assets anteriores.
