# Operação técnica de recuperação

## Escolha do executável

Use `win-x64` em Windows 10/11 de 64 bits. Use `win-x86` somente quando o Windows for realmente 32 bits.

## Espaço necessário

Para uma Inbox descompactada de 28 GiB, reserve pelo menos 31 GiB livres no destino. O backup original deve permanecer em outra pasta ou unidade sempre que possível.

## Saída

Cada execução cria uma pasta exclusiva:

```text
Recuperacao_<NomeDaCaixa>_20260727_153000\
├── <NomeDaCaixa>_Recuperada_001
├── <NomeDaCaixa>_Recuperada_002
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

## Tipo de reparo realizado

A aplicação reconstrói a estrutura utilizável da caixa MBOX em arquivos menores. Ela procura os delimitadores de início das mensagens que ainda estão presentes e copia o conteúdo correspondente para novas caixas, sem modificar a origem.

Esse processo é apropriado para Inbox excessivamente grande, índice `.msf` inconsistente e corrupção localizada. Não é possível reconstruir dados que já tenham sido fisicamente sobrescritos, truncados ou removidos do backup.

## Distribuição dos binários

Os workflows não utilizam mais `actions/upload-artifact`. Os executáveis e pacotes são enviados diretamente para GitHub Releases:

- `continuous`: build automático após atualização da branch principal;
- `v*`: release estável criada por tag.

Isso evita falhas causadas pela cota compartilhada de armazenamento de artifacts do GitHub Actions.
