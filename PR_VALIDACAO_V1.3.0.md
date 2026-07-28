# Branch

```text
fix/v1.3-validation-netsdk1151
```

# Título

```text
fix: corrige validação NETSDK1151 da versão 1.3
```

# Descrição

## Problema

A validação da versão 1.3 falhava após o restore, ao compilar o projeto de smoke tests:

```text
NETSDK1151: A self-contained executable cannot be referenced by a non self-contained executable.
```

O aplicativo principal declarava `SelfContained=true` e `PublishSingleFile=true` no `.csproj`, fazendo essas propriedades afetarem também o build normal da solução e o `ProjectReference` dos testes.

O log também mostrou seleção do SDK `10.0.301`, embora o projeto tenha como alvo o .NET 8.

## Correções

- remove `SelfContained` e `PublishSingleFile` da configuração global do aplicativo;
- mantém build e smoke tests framework-dependent;
- aplica self-contained e single-file somente no publish portátil;
- passa `SelfContained=true` também no restore dos RIDs, mantendo restore e publish coerentes;
- adiciona `global.json` para restringir o SDK à família .NET 8;
- valida `dotnet --version` no CI e no workflow de release;
- mantém geração portátil para `win-x86` e `win-x64`;
- mantém releases automáticas, novas e imutáveis;
- corrige a documentação da 1.3 para deixar claro que ela não cria `.msf` vazio.

## Critérios de aceite

- sintaxe PowerShell aprovada;
- SDK selecionado inicia com `8.`;
- restore da solução aprovado;
- build da solução sem `NETSDK1151`;
- smoke tests aprovados;
- publish self-contained x86 aprovado;
- publish self-contained x64 aprovado;
- nenhuma release anterior sobrescrita.

# Commit

```text
fix: separa build de testes do publish self-contained
```
