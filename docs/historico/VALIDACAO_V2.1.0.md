# Validação prevista — Thunderbird Recovery Suite 2.1.0

## Automatizada no GitHub Actions

- SDK .NET 8.
- Análise sintática dos scripts PowerShell.
- Restore e build da solução com warnings tratados como erros.
- Smoke tests de reparo e recuperação de mensagens excluídas.
- Smoke test de extração EML filtrada por número da mensagem.
- Smoke test de backup/restauração ZIP.
- Smoke test de backup/restauração 7Z.
- Validação dos grafos x86 e x64.
- Publish self-contained e framework-dependent.
- Criação de ZIP e 7Z.
- Release somente após todas as validações.

## Manual recomendada

- Splash em escala 100%, 125%, 150% e 200%.
- Links do About.
- MBOX real grande com exportação de uma mensagem e de todas.
- Backup 7Z de perfil fechado.
- Restauração sobre perfil vazio.
- Restauração sobre perfil existente com e sem sobrescrita.
- Verificação do backup de segurança ZIP e 7Z.
- Execução x86 e x64.

## Validação estática realizada nesta entrega

- 41 arquivos C# verificados quanto a balanceamento léxico de blocos, parênteses e colchetes.
- XML dos projetos e manifesto Windows validado.
- JSON do `global.json` validado.
- YAML dos workflows carregado e validado.
- Recurso PNG incorporado conferido e idêntico ao arquivo fornecido.
- Versão `2.1.0`, recurso incorporado, WPF/UI Automation e SharpCompress conferidos no projeto.
- Ausência de `--clobber`, `upload-artifact`, `download-artifact` e release/tag `continuous` nos fluxos ativos.

## Limitação do ambiente de entrega

O ambiente utilizado para preparar este pacote não possui SDK .NET, PowerShell 7 nem runner Windows. Assim, a compilação real, os smoke tests executáveis e a publicação `win-x86`/`win-x64` serão confirmados pelo GitHub Actions antes da criação da release.
