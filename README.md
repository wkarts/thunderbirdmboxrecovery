# Thunderbird MBOX Recovery

Utilitário Windows portátil para recuperar caixas MBOX grandes do Mozilla Thunderbird, independentemente do nome da pasta, inclusive quando estão armazenadas dentro de backup `.7z`.

## Objetivo

O operador leva somente o executável correspondente à arquitetura do computador do cliente:

- `ThunderbirdMboxRecovery-v<VERSAO>-win-x64.exe`
- `ThunderbirdMboxRecovery-v<VERSAO>-win-x86.exe`

O programa é autossuficiente, não exige instalação do .NET, Python, Thunderbird ou 7-Zip para executar a recuperação.


## O que a recuperação efetivamente faz

A ferramenta realiza uma **reconstrução estrutural do MBOX**:

- encontra os separadores de mensagens ainda reconhecíveis;
- copia as mensagens encontradas para uma nova caixa MBOX recuperada;
- gera por padrão um único MBOX recuperado, sem fracionamento;
- permite opcionalmente fracionar a saída somente no limite entre mensagens;
- não fabrica arquivo `.msf`; o Thunderbird cria/reconstrói o índice válido após a importação;
- preserva conteúdo anterior à primeira mensagem reconhecida para análise;
- não altera o arquivo original.

Isso normalmente recupera a visualização das mensagens quando o problema é causado por uma `Inbox` excessivamente grande, índice `.msf` inválido ou corrupção localizada entre mensagens. A ferramenta não recria bytes já sobrescritos, removidos ou truncados e não consegue restaurar anexos cujo conteúdo físico tenha sido perdido.

## Recursos

- Interface gráfica em português.
- Leitura direta de qualquer arquivo MBOX do Thunderbird, com ou sem extensão: `Inbox`, `Sent`, `Drafts`, `Archives`, `Trash`, `Templates`, `Junk`, pastas personalizadas e subpastas.
- Leitura de `.7z`, `.zip`, `.rar`, `.tar`, `.gz`, `.bz2` e `.xz`.
- Campo de senha para arquivos compactados protegidos.
- Detecção e seleção da caixa existente dentro do backup.
- Processamento em fluxo, sem carregar a caixa inteira na memória.
- Origem aberta somente para leitura.
- Divisão apenas no início reconhecido de uma nova mensagem MBOX.
- Saída única sem fracionamento por padrão.
- Fracionamento opcional com tamanho configurável, padrão de 1,50 GiB.
- O `.msf` não é criado artificialmente; a reconstrução é delegada ao Thunderbird.
- SHA-256 da entrada descompactada e de cada parte.
- `manifesto_recuperacao.json`.
- `recuperacao.log`.
- Preservação de bytes anteriores à primeira mensagem reconhecida.
- Arquivos incompletos permanecem com extensão `.partial` e não devem ser importados.
- Instruções de importação geradas automaticamente.

## Arquivos aceitos diretamente

O programa não depende do nome `Inbox`. Ele aceita qualquer arquivo de dados MBOX do Thunderbird, normalmente localizado em `Mail`, `ImapMail` ou `Mail\Local Folders`. A recuperação é executada sobre uma caixa por vez. Exemplos:

```text
Inbox
Sent
Drafts
Archives
Trash
Templates
Junk
Clientes
Financeiro
2025
caixa-exportada.mbox
```

Arquivos auxiliares como `*.msf`, `global-messages-db.sqlite`, `prefs.js` e outros metadados não armazenam o corpo completo das mensagens e não devem ser usados como origem.

O nome da caixa é preservado na saída. Exemplos:

```text
Sent            -> Sent_Recuperada
Archives        -> Archives_Recuperada
Clientes        -> Clientes_Recuperada
caixa.mbox      -> caixa_Recuperada
```

## Operação no cliente

1. Preserve o backup `.7z` original.
2. Execute preferencialmente a versão `win-x64`.
3. Selecione o backup ou qualquer arquivo MBOX descompactado do Thunderbird.
4. Informe a senha do `.7z`, quando houver.
5. Em backups compactados, clique em **Analisar backup** e selecione a caixa desejada.
6. Selecione uma unidade com espaço livre suficiente.
7. Mantenha a saída única, ou marque o fracionamento e defina o tamanho das partes.
8. Inicie a recuperação.
9. Confira `manifesto_recuperacao.json`, `recuperacao.log` e os arquivos gerados.
10. Importe o arquivo único ou as partes em um perfil separado do Thunderbird antes de alterar o perfil original.

## Segurança operacional

- Nunca execute a recuperação sobre a única cópia disponível.
- Não compacte nem altere o arquivo MBOX original durante a recuperação.
- Não importe arquivos terminados em `.partial`.
- Não apague o backup nem o arquivo MBOX original antes de conferir todos os arquivos recuperados.
- A pasta de destino deve ter, no mínimo, o tamanho descompactado da caixa MBOX mais 10% de folga.

## Desenvolvimento

Tecnologias:

- .NET 8
- Windows Forms
- SharpCompress

Restaurar e validar:

```bash
dotnet restore ThunderbirdMboxRecovery.sln
dotnet build ThunderbirdMboxRecovery.sln -c Release --no-restore
```

Publicar manualmente:

```bash
dotnet publish src/ThunderbirdMboxRecovery/ThunderbirdMboxRecovery.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish src/ThunderbirdMboxRecovery/ThunderbirdMboxRecovery.csproj -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true
```

## Fluxos do GitHub Actions

- `ci.yml`: valida Pull Requests sem publicar executáveis, releases ou artifacts.
- `release.yml`: após cada push em `main` ou `master`, gera `win-x86` e `win-x64` e cria uma nova release imutável.
- Não existe mais release `continuous`.
- Nenhuma tag anterior é movida, reutilizada ou apagada.
- Nenhum asset existente é sobrescrito com `--clobber`.
- O número da versão é automático no formato `<MAJOR>.<MINOR>.<GITHUB_RUN_NUMBER>`, por exemplo `v1.3.27`.
- Os binários e ZIPs recebem a versão no nome, por exemplo `ThunderbirdMboxRecovery-v1.3.27-win-x64.exe`.
- A publicação é feita diretamente em GitHub Releases, sem consumir a cota de `actions/upload-artifact`.

Cada execução manual do workflow também recebe um novo `GITHUB_RUN_NUMBER` e, consequentemente, uma nova versão. Se uma tag calculada já existir, o workflow falha de propósito em vez de alterar a release anterior.


## Saída única e `.msf`

A versão 1.3.0 gera, por padrão, apenas um arquivo como `Inbox_Recuperada` ou `Sent_Recuperada`. O operador pode marcar o fracionamento quando desejar várias partes.

A versão 1.3.0 não cria `.msf` vazio. Depois que o MBOX recuperado é copiado para `Mail\Local Folders\` com o Thunderbird fechado, o próprio Thunderbird cria ou reconstrói o índice `.msf` válido. Em caixas muito grandes, esse processamento pode demorar e manter a pasta ocupada.


## Build normal e publicação portátil

A solução é compilada normalmente como framework-dependent para permitir os smoke tests. Os executáveis entregues ao cliente continuam autossuficientes: `SelfContained=true` e `PublishSingleFile=true` são aplicados exclusivamente durante o publish `win-x86` e `win-x64`.
