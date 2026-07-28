# Correção preventiva e funcional da versão 1.4.0

## Correções de build herdadas

A 1.4.0 recebeu a mesma correção do script 1.3.0:

```powershell
Write-Host "Arquivos finais da versão ${Version}:"
```

Também recebeu análise sintática dos scripts, build com avisos como erro, smoke tests e validação x86/x64 antes da release.

## Reparos de mensagens

- remove `Expunged` sem apagar as demais flags de `X-Mozilla-Status`;
- remove `IMAPDeleted` sem apagar as demais flags de `X-Mozilla-Status2`;
- repara status malformado apenas quando a recuperação de excluídas está habilitada;
- preserva linha excepcionalmente longa em vez de descartá-la;
- adiciona status ausente antes do corpo da mensagem;
- registra quantidade de mensagens e cabeçalhos reparados.

## MSF

Nenhum `.msf` falso é criado. A criação de um índice válido será tratada em evolução específica, utilizando o próprio mecanismo do Thunderbird em perfil isolado.
