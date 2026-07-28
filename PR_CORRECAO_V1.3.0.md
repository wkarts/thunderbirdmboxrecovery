# Branch

```text
fix/v1.3-build-msf-rebuild
```

# Título

```text
fix: corrige build da linha 1.3 e delega o índice MSF ao Thunderbird
```

# Descrição

## Objetivo

Corrigir o bloqueio de build/release da versão 1.3.0 e consolidar uma estratégia segura para o índice `.msf`, sem fabricar um arquivo vazio ou incompatível.

## Correções

- corrige `NETSDK1151` causado por `SelfContained=true` aplicado globalmente;
- fixa a resolução do SDK na família .NET 8 por `global.json`;

- corrige a interpolação PowerShell `${Version}:` no script de publicação;
- valida sintaticamente todos os `.ps1` antes do build;
- adiciona build com avisos tratados como erro;
- adiciona smoke tests para saída única e fracionada;
- valida publicação portátil `win-x86` e `win-x64` antes de criar a release;
- mantém releases imutáveis e versionadas;
- remove a geração artificial de `.msf`;
- documenta que o Thunderbird reconstruiu sozinho o `.msf` em validação real feita com a versão 1.2.0 e uma caixa de aproximadamente 27,4 GiB;
- adiciona validação de espaço livre e bloqueio de arquivo maior que 4 GiB em FAT32.

## Fora do escopo

A linha 1.3 não altera flags de mensagens excluídas/expurgadas. Essa evolução pertence à 1.4.0.

## Critérios de aceite

- análise sintática PowerShell aprovada;
- solução compilada sem avisos e sem `NETSDK1151`;
- smoke tests aprovados;
- publish self-contained x86 e x64 aprovado;
- nenhuma criação de `.msf` vazio;
- release nova sem substituir versões anteriores.

# Commit

```text
fix: corrige pipeline 1.3 e remove MSF artificial
```
