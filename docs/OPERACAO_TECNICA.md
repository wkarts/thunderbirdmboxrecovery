# Operação técnica de recuperação

## Escolha do executável

Use `win-x64` em Windows 10/11 de 64 bits. Use `win-x86` somente quando o Windows for realmente 32 bits.

## Espaço necessário

Para uma Inbox descompactada de 28 GiB, reserve pelo menos 31 GiB livres no destino. O backup original deve permanecer em outra pasta ou unidade sempre que possível.

## Saída

Cada execução cria uma pasta exclusiva:

```text
Recuperacao_Inbox_20260727_153000\
├── Inbox_Recuperada_001
├── Inbox_Recuperada_002
├── ...
├── manifesto_recuperacao.json
├── recuperacao.log
└── COMO_IMPORTAR_NO_THUNDERBIRD.txt
```

Arquivos MBOX válidos são gerados sem extensão. Arquivos `.partial` indicam cancelamento ou falha e não devem ser importados.

## Importação

Com o Thunderbird fechado, copie as partes válidas para:

```text
<perfil>\Mail\Local Folders\
```

Abra o Thunderbird e aguarde a geração dos respectivos arquivos `.msf`.

## Limites

A ferramenta recupera mensagens cujos separadores MBOX ainda podem ser reconhecidos. Ela não recria bytes que tenham sido sobrescritos, truncados ou removidos antes da criação do backup.
