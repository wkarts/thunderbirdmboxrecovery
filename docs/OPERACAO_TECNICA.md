# Operação técnica — versão 1.3.0 corrigida

## Origem aceita

- arquivos MBOX sem extensão: `Inbox`, `Sent`, `Drafts`, `Trash`, `Archives` e pastas personalizadas;
- arquivos `.mbox` exportados;
- entrada selecionada dentro de `.7z`, `.zip`, `.rar`, `.tar`, `.gz`, `.bz2` ou `.xz`.

Não selecione `.msf`, `global-messages-db.sqlite`, `prefs.js` ou outros arquivos auxiliares.

## Procedimento

1. Trabalhe sempre sobre uma cópia do backup.
2. Prefira destino NTFS ou exFAT.
3. Deixe o fracionamento desmarcado para obter um único MBOX; marque somente quando necessário.
4. Aguarde a finalização e confira `manifesto_recuperacao.json`, `recuperacao.log` e os hashes.
5. Feche o Thunderbird e confirme que `thunderbird.exe` não está em execução.
6. Copie o MBOX para `Mail\\Local Folders\\` de um perfil de recuperação.
7. Remova somente um `.msf` antigo de mesmo nome, caso exista.
8. Abra o Thunderbird e aguarde. Em caixa de dezenas de gigabytes, a reconstrução do `.msf` pode levar bastante tempo e bloquear o comando **Reparar pasta**.

## Validação prática da 1.2.0

Foi observado em uso real que o Thunderbird reconstruiu sozinho o `.msf` de uma caixa recuperada de aproximadamente 27,4 GiB após permanecer aberto e processando a pasta por algum tempo. A pasta inicialmente aparecia ocupada e a lista de mensagens não era exibida. Portanto, não se deve concluir imediatamente que a recuperação falhou nem acionar repetidamente **Reparar pasta** enquanto outra operação estiver usando a caixa.

## Limites

A linha 1.3 não remove flags lógicas de exclusão. Use a linha 1.4 para recuperar mensagens marcadas como expurgadas ou excluídas no IMAP que ainda estejam fisicamente presentes no MBOX.
