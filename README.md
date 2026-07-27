# Thunderbird MBOX Recovery

Utilitário Windows portátil para recuperar caixas MBOX grandes do Mozilla Thunderbird, independentemente do nome da pasta, inclusive quando estão armazenadas dentro de backup `.7z`.

## Objetivo

O operador leva somente o executável correspondente à arquitetura do computador do cliente:

- `ThunderbirdMboxRecovery-win-x64.exe`
- `ThunderbirdMboxRecovery-win-x86.exe`

O programa é autossuficiente, não exige instalação do .NET, Python, Thunderbird ou 7-Zip para executar a recuperação.


## O que a recuperação efetivamente faz

A ferramenta realiza uma **reconstrução estrutural do MBOX**:

- encontra os separadores de mensagens ainda reconhecíveis;
- copia as mensagens encontradas para novas caixas MBOX menores;
- separa as partes somente no limite entre mensagens;
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
- Partes com tamanho configurável, padrão de 1,50 GiB.
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
Sent            -> Sent_Recuperada_001
Archives        -> Archives_Recuperada_001
Clientes        -> Clientes_Recuperada_001
caixa.mbox      -> caixa_Recuperada_001
```

## Operação no cliente

1. Preserve o backup `.7z` original.
2. Execute preferencialmente a versão `win-x64`.
3. Selecione o backup ou qualquer arquivo MBOX descompactado do Thunderbird.
4. Informe a senha do `.7z`, quando houver.
5. Em backups compactados, clique em **Analisar backup** e selecione a caixa desejada.
6. Selecione uma unidade com espaço livre suficiente.
7. Mantenha o tamanho padrão de 1,50 GiB.
8. Inicie a recuperação.
9. Confira `manifesto_recuperacao.json`, `recuperacao.log` e as partes geradas.
10. Importe as partes em um perfil separado do Thunderbird antes de alterar o perfil original.

## Segurança operacional

- Nunca execute a recuperação sobre a única cópia disponível.
- Não compacte nem altere o arquivo MBOX original durante a recuperação.
- Não importe arquivos terminados em `.partial`.
- Não apague o backup nem o arquivo MBOX original antes de conferir todas as partes.
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

- `ci.yml`: valida Pull Requests sem publicar executáveis ou artefatos.
- `build.yml`: gera `win-x86` e `win-x64` e atualiza diretamente a Release de pré-lançamento `continuous`, sem usar armazenamento de artifacts.
- `release.yml`: gera as duas arquiteturas em um único job e publica diretamente a Release estável para tags `v*`, sem `upload-artifact`/`download-artifact`.
