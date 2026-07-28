# Operação técnica — Thunderbird Recovery Suite 2.0

## 1. Preparação

- Trabalhe em cópia da origem.
- Reserve espaço para saída, temporários e backup de segurança.
- Use NTFS ou exFAT para arquivo único maior que 4 GiB.
- Não compacte o MBOX original suspeito.
- Feche o Thunderbird antes de backup consistente, restauração, registro de perfil ou substituição de arquivos.

## 2. Diagnóstico e exploração

1. Abra **Testar**.
2. Selecione MBOX direto ou entrada dentro de backup compactado.
3. Informe senha quando necessário.
4. Execute o diagnóstico e salve o JSON.
5. Use **Explorar** para inventário e exportação CSV/JSON.

Ocorrências relevantes:

- `SEM_MENSAGENS`: nenhum separador MBOX reconhecido.
- `CABECALHO_SEM_TERMINADOR`: ausência da linha vazia entre cabeçalho e corpo.
- `STATUS_MOZILLA_MALFORMADO`: flags internas inválidas.
- `MESSAGE_ID_DUPLICADO`: possível duplicação ou concatenação.
- `PREFIXO_NAO_RECONHECIDO`: bytes antes da primeira mensagem.

## 3. Reparo e recuperação de excluídas

Configuração recomendada:

```text
Fracionar saída: desmarcado
Recuperar mensagens excluídas/expurgadas: marcado
Normalizar X-Mozilla-Status: marcado
```

A saída padrão contém:

```text
Inbox_Recuperada
manifesto_recuperacao.json
recuperacao.log
COMO_IMPORTAR_NO_THUNDERBIRD.txt
```

A ferramenta não cria `.msf` artificial. Use **Indexar MSF** para gerar um índice pela instalação real do Thunderbird.

## 4. Extração EML

Use **Extrair EML** para filtrar itens por remetente, destinatário, assunto, período, anexos ou exclusão. Com a opção de preservar cabeçalhos Mozilla desmarcada, `X-Mozilla-Status`, `X-Mozilla-Status2` e `X-Mozilla-Keys` são removidos.

## 5. Indexação assistida

1. Abra **Indexar MSF**.
2. Selecione o MBOX reparado.
3. Detecte ou selecione `thunderbird.exe`.
4. Informe um nome simples para a caixa.
5. Mantenha automação habilitada.
6. Ajuste timeout para caixas grandes; o padrão é 6 horas.
7. Inicie.

A instância isolada é iniciada com:

```text
thunderbird.exe -profile "<perfil-temporario>" -new-instance -no-remote
```

O perfil temporário solicita o modo tradicional Mork desabilitando Panorama. Se a versão instalada não respeitar essa preferência ou já utilizar outra arquitetura, a suíte também monitora `panorama.sqlite`.

A conclusão exige índice válido e estável. Para `.msf`, a suíte compara a contagem `numMsgs` com os separadores MBOX. Divergência persistente não é ocultada: o resultado é entregue com aviso técnico.

Quando UI Automation não localizar a pasta, selecione-a manualmente uma vez na janela isolada. Não feche essa janela até a conclusão.

Entrega tradicional:

```text
Inbox_Recuperada
Inbox_Recuperada.msf
manifesto_indexacao.json
indexacao_thunderbird.log
```

## 6. Backup

### Completo

Inclui perfil operacional e índices; caches somente quando selecionados.

### Somente mensagens

Inclui `Mail`, `ImapMail` e `News`, sem `.msf` e bancos globais reconstruíveis.

### Seletivo

Permite escolher categorias.

Por padrão, a operação é bloqueada se houver lock de perfil. A opção **Permitir perfil aberto** existe somente para contingência e registra `SourceWasInUse=true` no manifesto.

## 7. Restauração

1. Feche todas as instâncias do Thunderbird.
2. Selecione backup e senha, quando necessário.
3. Escolha pasta de destino.
4. Selecione modo completo, somente mensagens ou seletivo.
5. Mantenha backup de segurança e validação SHA-256 ativos.
6. Marque sobrescrita somente quando intencional.
7. Para registrar o perfil, informe um nome e mantenha o Thunderbird fechado.
8. A opção de padrão altera `Default=1` no `profiles.ini`; ela não promete reconfigurar entradas específicas de `installs.ini`.

A restauração rejeita caminhos absolutos e `../`, usa `.restore-partial` e mantém cópia única do `profiles.ini` antes do registro.

## 8. Importação manual

Com o Thunderbird fechado, copie o MBOX sem extensão para:

```text
<perfil>\Mail\Local Folders\
```

Sem o módulo de indexação, abra o Thunderbird e aguarde. Em caixas com dezenas de gigabytes, o `.msf` pode ser reconstruído depois de bastante tempo e a pasta pode permanecer temporariamente ocupada.

## 9. Evidências de atendimento

Preserve:

- origem e SHA-256;
- diagnóstico JSON;
- manifesto e log do reparo;
- EMLs/CSV quando usados;
- manifesto e log da indexação;
- manifesto e SHA-256 do backup;
- versão e arquitetura do Thunderbird;
- informação de perfil aberto durante backup, quando aplicável.
