# Análise da caixa recuperada sem mensagens visíveis

A captura mostra que o Thunderbird reconheceu `Inbox_2_Recuperada`, encontrou 19.925 mensagens e associou 27,4 GB à pasta, mas a lista permaneceu vazia. Ao tentar reparar, o Thunderbird informou que outra operação estava usando a pasta.

Foram identificados três problemas:

1. A caixa foi adicionada dentro da pasta da conta, conforme o local `mailbox://.../Inbox_2_Recuperada`, e não em `Mail\Local Folders\`.
2. A versão anterior criava um `.msf` vazio. Esse arquivo não é um índice Mork válido e não deve ser importado.
3. O mecanismo anterior preservava `X-Mozilla-Status` e `X-Mozilla-Status2`. Mensagens ainda presentes fisicamente, mas marcadas como expurgadas ou excluídas, continuavam invisíveis após a reconstrução.

A versão 1.4.0 remove a criação do `.msf` artificial e normaliza as flags internas das mensagens durante a reconstrução.
