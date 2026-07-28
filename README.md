# Thunderbird Recovery Suite 2.2

Aplicação Windows portátil para explorar, diagnosticar, reparar, extrair, indexar, fazer backup e restaurar caixas MBOX e perfis do Mozilla Thunderbird.

## Desenvolvedor

- **WWSoftware's Sistemas e Tecnologias**
- **Wallace Kleiton**
- GitHub: **@wkarts**
- WhatsApp: **+55 75 98844-9231**
- E-mail: **wkarts@gmail.com**

A logomarca incorporada ao splash screen e à janela Sobre identifica o desenvolvedor.

## Principais módulos

### Explorar e extrair MBOX

- Abre `Inbox`, `Sent`, `Drafts`, `Trash`, `Archives`, pastas personalizadas e arquivos `.mbox`.
- Aceita arquivos sem extensão ou entradas dentro de ZIP, 7Z, RAR, TAR, GZip, BZip2 e XZ.
- Lista número, data, remetente, destinatário, assunto, status, anexos, tamanho, Message-ID e offsets.
- Extrai uma mensagem, várias mensagens selecionadas, todas ou somente as filtradas para EML.
- Exporta inventário em CSV e JSON.

### Diagnosticar e reparar

- Processamento em fluxo para caixas grandes.
- Reconstrução em arquivo único por padrão e fracionamento opcional entre mensagens completas.
- Recuperação de mensagens `Expunged` e `IMAPDeleted` ainda presentes fisicamente.
- Normalização de `X-Mozilla-Status` e `X-Mozilla-Status2`.
- Logs, manifestos e SHA-256.

### Indexar MSF

- Detecta Thunderbird instalado, versão e arquitetura.
- Cria perfil temporário isolado.
- Solicita ao próprio Thunderbird a geração do índice `.msf`.
- Valida Mork/MSF e acompanha a contagem de mensagens.
- Mantém fallback prospectivo para Panorama/SQLite.

## Backup 2.2

A tela detecta automaticamente os diretórios de dados e perfis disponíveis.

### Escopos

- **Perfil selecionado**: completo, somente mensagens ou seletivo.
- **Thunderbird completo**: inclui o diretório Roaming do Thunderbird, `profiles.ini`, `installs.ini`, todos os perfis armazenados nessa raiz e configurações.

### Locais detectados

- Instalação tradicional: `%APPDATA%\Thunderbird`.
- Microsoft Store: `%LOCALAPPDATA%\Packages\*Thunderbird*\LocalCache\Roaming\Thunderbird`.
- Diretório personalizado indicado pelo operador.

O `%LOCALAPPDATA%\Thunderbird` tradicional contém principalmente caches e é opcional. Quando solicitado, o cache local é armazenado separadamente dentro do backup.

Perfis registrados com `IsRelative=0` e armazenados fora da raiz selecionada aparecem na listagem automática, mas devem ser copiados pelo modo **Perfil selecionado**, evitando que um backup completo omita dados mantidos em outro disco ou diretório.

### Formatos e segurança

- ZIP para maior compatibilidade.
- 7Z/LZMA2 para maior compressão.
- SHA-256 por arquivo e do pacote final.
- Manifesto interno com escopo, origem, perfis e presença de cache local.
- Bloqueio de backup com Thunderbird aberto por padrão.

## Restauração 2.2

A suíte identifica o tipo do backup, seleciona a raiz de dados compatível e calcula automaticamente o destino. O caminho sugerido permanece estável enquanto o nome do novo perfil é editado.

### Modos disponíveis

1. **Criar um novo perfil** — padrão recomendado.
   - Cria uma pasta isolada em `Profiles`.
   - Registra automaticamente no `profiles.ini`.
   - Cria uma estrutura mínima válida quando o backup é apenas de mensagens ou seletivo.
   - Não altera o perfil atual.
   - Permite definir o novo perfil como padrão.

2. **Substituir um perfil existente**.
   - Lista automaticamente os perfis detectados.
   - Exige Thunderbird fechado.
   - Exige backup de segurança em ZIP ou 7Z.
   - Exige a confirmação `SUBSTITUIR PERFIL`.

3. **Restaurar somente mensagens em um perfil existente**.
   - Não sobrescreve Inbox, Sent ou outras caixas atuais.
   - Cria uma pasta separada em `Mail\Local Folders`.
   - Mantém as mensagens importadas isoladas para conferência.
   - Não importa índices `.msf` antigos nem metadados de controle POP/IMAP.

4. **Restaurar o Thunderbird completo**.
   - Disponível para backups do diretório completo.
   - Restaura Roaming, todos os perfis armazenados na raiz, `profiles.ini` e `installs.ini`.
   - Cache local permanece opcional e desativado por padrão.
   - Exige backup de segurança e a confirmação `SUBSTITUIR THUNDERBIRD`.

5. **Pasta manual** — modo avançado.

A restauração valida hashes, bloqueia path traversal e grava cada arquivo por meio de `.restore-partial`. Substituições de perfil ou da raiz completa são extraídas em diretório de estágio; o destino original só é trocado depois da validação integral, com reversão automática se a troca falhar.

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
- hashes SHA-256.

UPX permanece opcional e desativado por padrão, pois pode causar incompatibilidades ou alertas de antivírus em executáveis .NET self-contained.

## Compilação local

Requisitos:

- Windows 10 ou superior;
- PowerShell 7;
- SDK .NET 8;
- 7-Zip para gerar pacotes `.7z`.

```powershell
pwsh ./scripts/Publish-Portable.ps1 -Version 2.2.0
```

Para tentar gerar uma variante UPX:

```powershell
pwsh ./scripts/Publish-Portable.ps1 -Version 2.2.0 -EnableUpx $true
```

## CI e releases

- Pull Requests: sintaxe PowerShell, restore, build, smoke tests e validação dos grafos x86/x64.
- Merge em `main` ou `master`: cria release nova e imutável `v2.2.<GITHUB_RUN_NUMBER>`.
- Não utiliza release `continuous`.
- Não move tags antigas nem substitui assets anteriores.
