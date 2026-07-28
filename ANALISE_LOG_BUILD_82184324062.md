# Análise do build 82184324062

## Resultado das etapas

- SDK .NET 8: confirmado.
- Sintaxe PowerShell: aprovada.
- Restore da solução: concluído.
- Referências de Windows UI Automation: corrigidas na revisão anterior.
- Build: interrompido por 17 erros CS0246 relacionados exclusivamente a tipos de `System.IO`.

## Erros encontrados

O projeto Windows Desktop não disponibilizou `System.IO` no conjunto de usings implícitos efetivamente gerado. Por isso, tipos usados em assinaturas e campos não foram resolvidos:

- `Stream`
- `FileStream`
- `MemoryStream`
- `StreamWriter`

Arquivos atingidos:

- `Core/MboxStreamParser.cs`
- `Core/ArchiveService.cs`
- `Core/ChunkWriter.cs`
- `Core/MboxAnalyzer.cs`
- `Core/MboxExtractor.cs`
- `Core/MessageRepairWriter.cs`
- `Core/ThunderbirdIndexService.cs`
- `Core/ProfileBackupService.cs`
- `Core/RecoveryLogger.cs`
- `Core/ProfileRestoreService.cs`

## Correção

Foi adicionado o arquivo:

```text
src/ThunderbirdMboxRecovery/GlobalUsings.cs
```

Conteúdo:

```csharp
global using System.IO;
```

A correção centraliza o namespace necessário para toda a aplicação e cobre também os usos de `File`, `Directory`, `Path`, `FileInfo`, `DriveInfo`, `FileMode`, `FileAccess`, `FileShare`, `FileOptions` e `InvalidDataException`.

## Escopo

Nenhuma lógica de recuperação, indexação, backup, restauração ou publicação foi alterada. A correção é exclusivamente de compilação.
