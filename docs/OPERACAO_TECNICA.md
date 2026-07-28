# Operação técnica — versão 1.4.0 corrigida

## Antes de recuperar

1. Preserve a origem original e o backup `.7z`.
2. Extraia ou selecione qualquer MBOX do Thunderbird: `Inbox`, `Sent`, `Drafts`, `Trash`, `Archives` ou pasta personalizada.
3. Não selecione `.msf` ou banco auxiliar.
4. Use destino NTFS/exFAT com espaço livre superior ao tamanho esperado da saída.

## Modos

### Reconstrução conservadora

Desmarque a recuperação de excluídas. A aplicação reconstrói o MBOX sem remover as flags lógicas de exclusão.

### Recuperação de excluídas/expurgadas

Marque a opção correspondente. A aplicação remove somente:

- `0x0008` (`Expunged`) de `X-Mozilla-Status`;
- `0x00200000` (`IMAPDeleted`) de `X-Mozilla-Status2`.

As demais flags são preservadas.

## Importação

1. Feche o Thunderbird.
2. Use preferencialmente um perfil separado de recuperação.
3. Copie somente o MBOX reparado para `Mail\\Local Folders\\`.
4. Não copie `.msf` antigo e não crie `.msf` vazio.
5. Abra o Thunderbird e aguarde a indexação.
6. Enquanto a pasta estiver ocupada, não clique repetidamente em **Reparar pasta**.
7. Valide assuntos, remetentes, datas, corpos e anexos antes de substituir qualquer caixa.

## Observação validada em campo

Com a versão 1.2.0, o Thunderbird reconstruiu sozinho o `.msf` de uma caixa de aproximadamente 27,4 GiB após ficar aberto e processando o arquivo por algum tempo. A ausência inicial da lista e o bloqueio por “outra operação” não significaram necessariamente perda definitiva.
