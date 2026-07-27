# Thunderbird MBOX Recovery

Utilitário Windows portátil para recuperar caixas MBOX grandes do Mozilla Thunderbird, inclusive quando estão armazenadas dentro de backup `.7z`.

## Objetivo

O operador leva somente o executável correspondente à arquitetura do computador do cliente:

- `ThunderbirdMboxRecovery-win-x64.exe`
- `ThunderbirdMboxRecovery-win-x86.exe`

O programa é autossuficiente, não exige instalação do .NET, Python, Thunderbird ou 7-Zip para executar a recuperação.

## Recursos

- Interface gráfica em português.
- Leitura direta de `Inbox`/MBOX sem extensão.
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

## Operação no cliente

1. Preserve o backup `.7z` original.
2. Execute preferencialmente a versão `win-x64`.
3. Selecione o backup ou o arquivo `Inbox`.
4. Informe a senha do `.7z`, quando houver.
5. Clique em **Analisar backup** e confirme a `Inbox` correta.
6. Selecione uma unidade com espaço livre suficiente.
7. Mantenha o tamanho padrão de 1,50 GiB.
8. Inicie a recuperação.
9. Confira `manifesto_recuperacao.json`, `recuperacao.log` e as partes geradas.
10. Importe as partes em um perfil separado do Thunderbird antes de alterar o perfil original.

## Segurança operacional

- Nunca execute a recuperação sobre a única cópia disponível.
- Não compacte a Inbox original de 28 GB.
- Não importe arquivos terminados em `.partial`.
- Não apague o `.7z` nem a Inbox original antes de conferir todas as partes.
- A pasta de destino deve ter, no mínimo, o tamanho descompactado da Inbox mais 10% de folga.

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
- `build.yml`: gera pacotes portáteis x86/x64 em push para `main` ou execução manual.
- `release.yml`: publica Release estável somente para tags `v*`.
