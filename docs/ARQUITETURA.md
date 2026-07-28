# Arquitetura — Thunderbird Recovery Suite 2.1

## Camadas

- **UI Windows Forms:** splash screen, janela Sobre e módulos operacionais.
- **Parser MBOX em fluxo:** análise, diagnóstico, reparo e extração EML.
- **Integração Thunderbird:** detecção de instalação, perfil temporário e geração assistida de MSF.
- **Backup/Restauração:** classificação de arquivos, ZIP/7Z, manifestos, hashes e restauração segura.
- **Distribuição:** publish self-contained e framework-dependent para x86/x64, pacotes ZIP/7Z e releases imutáveis.

## Branding

A logomarca do desenvolvedor é recurso incorporado ao assembly e não depende de arquivo externo no computador do cliente.

## Seleção de mensagens

A grade da aba Explorar mantém o número lógico e os offsets de cada mensagem. A extração seletiva reutiliza o parser em fluxo e filtra pelos números escolhidos, sem carregar o MBOX inteiro na memória.

## Backup 7Z

O escritor 7Z usa LZMA2 e grava cada arquivo do perfil sequencialmente, seguido pelo manifesto. A restauração utiliza leitura sequencial e validação opcional dos hashes registrados.

## Restauração existente

A interface executa as confirmações de risco. O serviço:

- bloqueia perfil em uso;
- cria backup de segurança opcional;
- grava cada arquivo em `.restore-partial`;
- valida SHA-256 antes da substituição;
- move o parcial somente após sucesso;
- protege contra caminhos absolutos e travessia de diretórios.
