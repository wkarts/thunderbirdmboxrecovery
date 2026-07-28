# Thunderbird Recovery Suite 2.0

Aplicação portátil para Windows que reúne diagnóstico, exploração, reparo, extração, indexação assistida, backup e restauração de caixas e perfis do Mozilla Thunderbird.

A suíte trabalha sem alterar a origem: arquivos MBOX são abertos somente para leitura, as saídas são criadas em diretórios separados e operações críticas utilizam arquivos temporários antes da confirmação final.

## Recursos integrados

### Visão geral

- Detecta instalações do Thunderbird nos registros, `Program Files` e `LocalAppData`.
- Identifica versão e arquitetura `x86`, `x64` ou `ARM64` do executável PE.
- Localiza perfis pelo `profiles.ini` e pela pasta `Profiles`.
- Exibe tamanho estimado dos perfis e identifica o perfil marcado como padrão.

### Explorar e testar

- Abre MBOX direto, com ou sem extensão, ou uma entrada MBOX dentro de `.7z`, `.zip`, `.rar`, `.tar`, `.gz`, `.bz2` e `.xz`.
- Processa o conteúdo em fluxo, sem carregar integralmente caixas com dezenas de gigabytes.
- Lista assunto, remetente, destinatário, data, `Message-ID`, offsets, tamanho, flags Mozilla, exclusão e indicação de anexo.
- Calcula SHA-256 da origem.
- Detecta separadores inválidos, prefixo não reconhecido, `Message-ID` duplicado, cabeçalho sem terminador e status Mozilla malformado.
- Exporta inventário e diagnóstico em JSON e CSV.

### Reparar

- Reconstrói o MBOX em uma saída nova.
- Saída única é o padrão; fracionamento é opcional e ocorre somente entre mensagens completas.
- Normaliza `X-Mozilla-Status` e `X-Mozilla-Status2`.
- Pode remover `Expunged` e `IMAPDeleted`, recuperando mensagens que ainda permanecem fisicamente no arquivo.
- Preserva flags independentes, como lida, respondida, encaminhada e marcada.
- Gera manifesto, hashes SHA-256, instruções de importação e log técnico.
- Não fabrica `.msf` vazio ou incompatível.

### Extrair EML

- Extrai mensagens individuais para `.eml`.
- Filtra por assunto, remetente, destinatário, período, presença de anexo e estado de exclusão.
- Permite preservar ou remover cabeçalhos internos Mozilla.
- Gera índice CSV dos itens exportados.

### Indexar com o Thunderbird real

- Usa uma instalação real do Thunderbird encontrada no computador.
- Cria um perfil temporário e isolado.
- Copia o MBOX para `Mail/Local Folders`.
- Inicia uma instância separada com `-profile`, `-new-instance` e `-no-remote`.
- Tenta selecionar a pasta por Windows UI Automation; se a automação não conseguir, solicita apenas a seleção manual da pasta na janela isolada.
- Mantém `mail.panorama.enabled=false` no perfil temporário para solicitar a geração tradicional de `.msf` quando essa preferência for reconhecida pela versão instalada.
- Monitora tamanho, data de gravação e estabilidade do índice.
- Valida assinatura Mork e tenta comparar `numMsgs` com a contagem estrutural do MBOX.
- Quando houver divergência estável de contagem, aguarda um período adicional antes de concluir com aviso.
- Mantém detecção de `panorama.sqlite` como compatibilidade prospectiva quando a instalação ignorar ou substituir o fluxo Mork.
- Entrega o par MBOX/`.msf` realmente produzido pelo Thunderbird, ou preserva o banco Panorama quando não existir um `.msf` autônomo.

### Backup de perfil

- Modos completo, somente mensagens ou seletivo.
- Seleção de `Mail`, `ImapMail/News`, preferências, catálogos, calendários, credenciais/certificados, extensões, índices e caches.
- Bloqueia por padrão backup de perfil em uso; existe uma substituição explícita para atendimento emergencial, registrada no manifesto.
- ZIP gravado inicialmente como `.partial` e confirmado somente após conclusão.
- Manifesto interno com caminho, tamanho, data e SHA-256 por arquivo.
- SHA-256 do pacote e arquivo `.sha256` lateral.

### Restaurar perfil

- Restaura ZIPs da suíte e arquivos compatíveis com SharpCompress: `.7z`, `.zip`, `.rar`, `.tar`, `.gz`, `.bz2` e `.xz`.
- Suporta restauração completa, somente mensagens ou seletiva.
- Aceita senha para arquivos compactados protegidos.
- Reconhece perfil na raiz, em `profile/` ou dentro de uma pasta superior única.
- Bloqueia caminhos absolutos e travessia de diretórios.
- Restaura por arquivos temporários e valida SHA-256 quando existe manifesto.
- Pode criar backup de segurança do destino.
- Pode registrar o perfil restaurado no `profiles.ini`, mantendo cópia de segurança do arquivo anterior.
- Pode marcar o perfil como padrão no `profiles.ini`; isso não altera configurações externas específicas de instalação que eventualmente existam em `installs.ini`.

## Formatos e tecnologias

- Mensagens: MBOX/Berkeley e arquivos sem extensão do Thunderbird.
- Compactação para leitura: 7z, ZIP, RAR, TAR, GZip, BZip2 e XZ.
- Backup nativo: ZIP.
- Índice tradicional: `.msf`/Mork criado pelo Thunderbird instalado.
- Compatibilidade prospectiva: detecção de `panorama.sqlite`.
- Runtime: .NET 8 Windows Desktop.
- Interface: Windows Forms e Windows UI Automation.
- Distribuição: executáveis self-contained, single-file, separados para `win-x86` e `win-x64`.

## Segurança operacional

1. Preserve o backup original e trabalhe em uma cópia.
2. Feche o Thunderbird antes de substituir caixas, restaurar perfis ou registrar um perfil.
3. Não compacte uma caixa suspeita antes de concluir a recuperação.
4. Valide hashes e manifestos antes de substituir dados de produção.
5. Teste o resultado em perfil isolado.
6. Em backup de perfil aberto, use a substituição emergencial somente quando não for possível interromper o Thunderbird e registre essa condição no atendimento.

## Compilação local

Requisitos:

- Windows 10/11 ou Windows Server compatível.
- SDK .NET 8 x64.
- PowerShell 7 recomendado.

```powershell
pwsh ./scripts/Test-PowerShellSyntax.ps1

dotnet restore ThunderbirdMboxRecovery.sln `
  -p:SelfContained=false `
  -p:PublishSingleFile=false

dotnet build ThunderbirdMboxRecovery.sln `
  -c Release `
  --no-restore `
  -p:SelfContained=false `
  -p:PublishSingleFile=false `
  -warnaserror

dotnet run `
  --project tests/ThunderbirdMboxRecovery.SmokeTests/ThunderbirdMboxRecovery.SmokeTests.csproj `
  -c Release `
  --no-build

pwsh ./scripts/Publish-Portable.ps1 -Version 2.0.0
```

## GitHub Actions e releases

- Pull Request: valida sintaxe PowerShell, SDK .NET 8, build com warnings como erro, smoke tests e publicação temporária `win-x86`/`win-x64` dentro do runner.
- Merge em `main` ou `master`: cria tag e release novas no padrão `v2.0.<GITHUB_RUN_NUMBER>`.
- Releases são imutáveis: não há tag `continuous`, movimentação de tag ou `--clobber`.
- A publicação envia os binários diretamente para GitHub Releases, sem depender de `actions/upload-artifact`.

Arquivos esperados:

```text
ThunderbirdRecoverySuite-v2.0.123-win-x86.exe
ThunderbirdRecoverySuite-v2.0.123-win-x64.exe
ThunderbirdRecoverySuite-v2.0.123-win-x86.zip
ThunderbirdRecoverySuite-v2.0.123-win-x64.zip
SHA256SUMS.txt
VERSION.txt
```

## Limitações conhecidas

- Bytes fisicamente truncados, sobrescritos ou ausentes não podem ser recriados.
- A detecção de anexos se baseia em cabeçalhos MIME e pode ser incompleta em mensagens severamente danificadas.
- A contagem `numMsgs` do Mork é uma validação auxiliar; o manifesto preserva também a contagem estrutural do MBOX.
- A automação de interface depende da árvore de acessibilidade exposta pela versão, idioma e tema do Thunderbird; existe fallback de seleção manual.
- Panorama continua em evolução. A suíte prioriza `.msf` no perfil isolado e preserva `panorama.sqlite` quando a instalação operar de forma diferente.
- A criação do índice exige uma instalação funcional do Thunderbird e pode levar horas em caixas muito grandes.
- Marcar `Default=1` no `profiles.ini` não garante substituir associações específicas já registradas em `installs.ini` por determinadas instalações do Thunderbird.
