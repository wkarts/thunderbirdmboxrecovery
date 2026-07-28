# Análise do workflow 82101614969

## Resultado real

A compilação foi concluída corretamente para as duas arquiteturas:

- `win-x64`: `dotnet publish` concluiu e gravou a saída em `artifacts\win-x64`;
- `win-x86`: `dotnet publish` concluiu e gravou a saída em `artifacts\win-x86`.

A falha ocorreu somente na etapa `actions/upload-artifact@v4`:

```text
Failed to CreateArtifact: Artifact storage quota has been hit.
Unable to upload any new artifacts.
Usage is recalculated every 6-12 hours.
```

Portanto, não houve erro de código C#, restore ou publicação self-contained. O job ficou vermelho porque não conseguiu armazenar os arquivos compilados como artifacts do GitHub Actions.

## Correção aplicada

- removido `actions/upload-artifact` do build contínuo;
- removidos `actions/upload-artifact` e `actions/download-artifact` da release por tag;
- x86 e x64 agora são construídos no mesmo job para publicação atômica;
- binários são enviados diretamente para GitHub Releases;
- `continuous` recebe builds da branch principal;
- tags `v*` geram releases estáveis;
- ações atualizadas para `actions/checkout@v6` e `actions/setup-dotnet@v5`;
- PR continua apenas validando build e publish de x86/x64, sem guardar artifacts.
