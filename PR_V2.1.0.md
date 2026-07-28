# Pull Request — Thunderbird Recovery Suite 2.1.0

## Branch

```text
feature/v2.1-branding-eml-backup-restore
```

## Título

```text
feat: adiciona branding, extração seletiva EML, backup 7Z e restauração segura
```

## Descrição

### Objetivo

Evoluir a suíte com acabamento institucional, extração direta de mensagens selecionadas, novos formatos de backup e proteções adicionais para restauração sobre perfis existentes.

### Entregas

- Splash screen com versão e arquitetura.
- Janela Sobre com logomarca do desenvolvedor e contatos.
- Integração da identidade de WWSoftware's Sistemas e Tecnologias e Wallace Kleiton.
- Listagem MBOX com seleção múltipla.
- Extração de uma, várias ou todas as mensagens para EML.
- Backup completo, de mensagens ou seletivo em ZIP ou 7Z/LZMA2.
- Backup de segurança em ZIP ou 7Z antes da restauração.
- Confirmação múltipla para restauração sobre perfil existente.
- Explicação explícita dos riscos de substituição de dados.
- Pacotes ZIP/7Z do executável self-contained.
- Variante menor que exige .NET 8 Desktop Runtime.
- UPX opcional e desativado por padrão.
- Releases automáticas novas e imutáveis `v2.1.<run_number>`.

### Critérios de aceitação

- Build com .NET 8 sem warnings.
- Splash e About abrem sem arquivo de imagem externo.
- Contatos e links funcionam.
- Grade lista mensagens e permite múltipla seleção.
- Extração seletiva gera somente os EML escolhidos.
- Extração total processa o MBOX completo.
- Backup ZIP e 7Z contêm manifesto e hashes.
- Restauração de ZIP e 7Z recupera os arquivos validados.
- Perfil existente exige confirmações e Thunderbird fechado.
- Release gera x86/x64 em EXE, ZIP e 7Z, preservando versões anteriores.

## Commit

```text
feat: entrega branding e fluxos avançados de EML, backup e restauração
```

## Mensagem de merge

```text
feat: release Thunderbird Recovery Suite 2.1
```
