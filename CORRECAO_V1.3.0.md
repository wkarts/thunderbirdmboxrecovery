# Correção da versão 1.3.0

## Diagnóstico do log `82162774302`

O workflow definiu a versão `1.3.1` e chegou à etapa de empacotamento, porém o PowerShell interrompeu a execução antes de `dotnet restore` e `dotnet publish`.

Erro original:

```text
ParserError: scripts/Publish-Portable.ps1:118
Write-Host "Arquivos finais da versão $Version:"
Variable reference is not valid. ':' was not followed by a valid variable name character.
```

Correção:

```powershell
Write-Host "Arquivos finais da versão ${Version}:"
```

## Barreiras adicionadas

- parser PowerShell executado no CI antes do build;
- build da solução com `-warnaserror`;
- smoke tests com MBOX sintético em saída única e fracionada;
- validação portátil `win-x86` e `win-x64` antes da release;
- proteção contra diretório de saída igual à raiz do repositório;
- validação de espaço livre e FAT32;
- release imutável: uma tag nova por execução, sem `continuous` e sem sobrescrever assets.

## Estratégia MSF corrigida

A 1.3 não cria `.msf` artificial. O Thunderbird reconstrói o índice a partir do MBOX importado. A observação feita com a 1.2.0 — reconstrução espontânea do `.msf` após algum tempo de processamento — foi registrada no README e no guia operacional.
