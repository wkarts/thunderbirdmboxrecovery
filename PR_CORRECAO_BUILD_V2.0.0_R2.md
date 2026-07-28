# Pull Request — correção de build da suíte 2.0

## Branch

`feature/thunderbird-recovery-suite-2.0`

## Título

`fix: corrige referências de UI Automation e validação do build 2.0`

## Commit

`fix: habilita referências WPF para Windows UI Automation`

## Descrição

### Problema

O build do PR falhava ao resolver `UIAutomationClient` e `UIAutomationTypes`. Como consequência, `System.Windows.Automation`, `AutomationElement` e `Condition` não eram encontrados em `ThunderbirdUiAutomation.cs`.

### Correção

- habilita `UseWPF` no projeto híbrido Windows Forms/WPF;
- remove referências manuais não resolvíveis às DLLs de UI Automation;
- mantém a interface principal em Windows Forms;
- preserva a automação assistida do Thunderbird por `System.Windows.Automation`;
- mantém suporte a `win-x86` e `win-x64`;
- impede geração de executáveis e artifacts no workflow de Pull Request;
- deixa os publishes portáteis apenas no workflow pós-merge de release.

### Critérios de aceitação

- selecionar SDK .NET 8;
- validar sintaxe PowerShell;
- restaurar e compilar a solução com warnings tratados como erro;
- executar smoke tests integrados;
- validar restore dos grafos `win-x86` e `win-x64` sem publicar binários no PR;
- após merge, publicar executáveis self-contained/single-file x86 e x64 em uma release nova e imutável.
