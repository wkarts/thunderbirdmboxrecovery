# Análise do log de validação 82167529235

## Etapas aprovadas

- checkout do Pull Request;
- instalação/configuração do .NET;
- validação sintática de `Publish-Portable.ps1`;
- validação sintática de `Test-PowerShellSyntax.ps1`;
- restore dos dois projetos.

## Etapa que falhou

O build compilou o aplicativo principal, mas falhou ao compilar o projeto de smoke tests:

```text
error NETSDK1151: The referenced project ThunderbirdMboxRecovery.csproj is a self-contained executable.
A self-contained executable cannot be referenced by a non self-contained executable.
```

## Causa

`SelfContained=true` e `PublishSingleFile=true` estavam definidos globalmente no projeto WinForms. Assim, qualquer build, inclusive o build usado pelo `ProjectReference` dos smoke tests, tratava o aplicativo como self-contained.

## Ajuste

O build normal da solução passou a ser framework-dependent. As opções self-contained e single-file são aplicadas somente no restore/publish específico de `win-x86` e `win-x64`.

Também foi adicionado `global.json`, pois o log mostrou que o runner selecionou o SDK `10.0.301`. A solução agora exige a família .NET 8 e o workflow interrompe imediatamente caso outro major seja selecionado.
