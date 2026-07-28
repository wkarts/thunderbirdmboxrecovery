# Operação técnica — Thunderbird Recovery Suite 2.2

## Backup recomendado

1. Feche completamente o Thunderbird.
2. Abra o módulo **Backup**.
3. Selecione **Thunderbird completo** para preservar todos os perfis armazenados na raiz, `profiles.ini` e `installs.ini`.
4. Confirme o diretório detectado em Roaming.
5. Mantenha **Incluir AppData Local** desmarcado, salvo necessidade de preservar caches.
6. Escolha 7Z para menor tamanho ou ZIP para compatibilidade.
7. Guarde também o arquivo `.sha256`.
8. Caso a tela indique perfil absoluto fora da raiz Roaming, faça também um backup individual desse perfil.

## Restaurar criando um novo perfil

1. Selecione o backup de um perfil.
2. Mantenha **Criar um novo perfil**.
3. Escolha a raiz de dados detectada.
4. Informe o nome do perfil.
5. Confira o caminho calculado.
6. Execute a restauração.
7. O perfil será registrado automaticamente sem alterar o perfil atual.

## Substituir um perfil

1. Feche o Thunderbird.
2. Selecione **Substituir um perfil existente**.
3. Escolha explicitamente o perfil.
4. Confirme o formato do backup de segurança.
5. Marque a ciência do risco.
6. Digite `SUBSTITUIR PERFIL`.
7. Confirme a operação crítica.

## Restaurar somente mensagens

1. Selecione **Restaurar somente mensagens em um perfil existente**.
2. Escolha o perfil de destino.
3. Informe o nome da pasta de importação.
4. As caixas serão colocadas em `Mail\Local Folders\<nome>.sbd`.
5. Inbox, Sent e demais caixas atuais não serão sobrescritas.

## Restaurar Thunderbird completo

1. Use apenas um backup identificado como `ThunderbirdDataRoot`.
2. Feche o Thunderbird.
3. Escolha o diretório de dados detectado.
4. Mantenha o backup de segurança obrigatório.
5. Digite `SUBSTITUIR THUNDERBIRD`.
6. Restaure o cache local somente quando realmente necessário.

## Observação sobre AppData Local

Na instalação tradicional, `%LOCALAPPDATA%\Thunderbird` contém principalmente caches. Os dados essenciais ficam em `%APPDATA%\Thunderbird`. Na versão Microsoft Store, a área Roaming virtualizada fica dentro de `%LOCALAPPDATA%\Packages\...\LocalCache\Roaming\Thunderbird` e é tratada como diretório principal.

## Regras adicionais da restauração 2.2

- A raiz de dados é escolhida automaticamente de acordo com o tipo registrado no manifesto, quando disponível.
- O caminho sugerido para um novo perfil é exclusivo e permanece estável enquanto o operador altera o nome.
- Um novo perfil restaurado recebe uma estrutura mínima válida antes de ser registrado no `profiles.ini`.
- Substituições em destinos que já contêm dados exigem backup de segurança e autorização de sobrescrita também no núcleo da aplicação.
- A importação de mensagens em perfil existente ignora `.msf`, bancos SQLite e arquivos de controle como `popstate.dat` e `msgFilterRules.dat`; o Thunderbird reconstrói os índices necessários.

### Troca transacional do destino

Ao substituir um perfil ou a raiz completa, a suíte não extrai diretamente sobre os dados em uso. A extração e a validação ocorrem em um diretório irmão de estágio. Somente após o processamento integral o diretório original é movido temporariamente e o estágio assume o caminho definitivo. Se alguma troca falhar, os diretórios já alterados são revertidos na ordem inversa.
