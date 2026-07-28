# Correção da validação da versão 1.3.0

## Log analisado

Execução: `82167529235`

A validação de sintaxe PowerShell foi aprovada. O erro ocorreu depois, durante o build da solução:

```text
error NETSDK1151: The referenced project ThunderbirdMboxRecovery.csproj is a self-contained executable.
A self-contained executable cannot be referenced by a non self-contained executable.
```

## Causa raiz

O projeto principal declarava globalmente no `.csproj`:

```xml
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
```

Essas propriedades devem ser usadas apenas no `dotnet publish` dos executáveis portáteis. Como também eram aplicadas ao build normal da solução, o projeto de smoke tests, que é framework-dependent, não conseguia referenciar o executável self-contained.

O runner também selecionou o SDK `10.0.301`, apesar de o workflow instalar `8.0.x`, porque não havia `global.json` limitando a resolução do SDK. Isso não originou o `NETSDK1151`, mas aumentava o risco de incompatibilidades futuras.

## Correções aplicadas

- removidas `SelfContained` e `PublishSingleFile` da configuração global do projeto principal;
- mantido o build normal framework-dependent para permitir `ProjectReference` dos smoke tests;
- mantido `SelfContained=true` somente no restore/publish dos artefatos portáteis `win-x86` e `win-x64`;
- adicionada `IncludeNativeLibrariesForSelfExtract=true` explicitamente no publish;
- adicionado `global.json` para restringir a solução à família do SDK .NET 8;
- adicionada validação explícita de que `dotnet --version` começa com `8.`;
- adicionadas propriedades defensivas `SelfContained=false` e `PublishSingleFile=false` no restore/build da solução;
- definido `SelfContained=false` explicitamente no projeto de smoke tests.

## Resultado esperado

A sequência deve agora ser:

1. validar sintaxe PowerShell;
2. confirmar SDK .NET 8;
3. restaurar e compilar a solução em modo framework-dependent;
4. executar smoke tests;
5. publicar executáveis self-contained separados para `win-x86` e `win-x64`;
6. criar uma nova release imutável somente após todas as validações.
