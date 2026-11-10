# Terraprint: roteiro de testes por passo

Cada passo foi um commit separado (`git log` mostra). Para compilar: abra o tModLoader,
vá em **Workshop > Develop Mods**, encontre **Terraprint** e clique em **Build + Reload**.

## Passo 1: base, tecla, menu e salvar blueprint

1. Entre em um mundo e aperte **K** (troque em Configurações > Controles > Mod Controls, se quiser).
   O menu do Terraprint deve abrir. Arraste pela barra de cima; o X fecha.
2. Clique em **Selecionar área**, depois clique em um canto e no canto oposto de uma construção.
   Um retângulo azul com o tamanho (ex.: `20 x 12`) aparece enquanto você seleciona.
   O botão direito cancela. Enquanto seleciona, o item da mão não é usado.
3. Digite um nome no campo (clique nele, digite, Enter) e clique em **Salvar**.
   O chat confirma e o arquivo aparece na lista **Blueprints salvos**.
4. **Abrir pasta** abre `Documents/My Games/Terraria/tModLoader/Terraprint`, onde ficam os `.tprint`.
5. Salvar duas vezes com o mesmo nome cria `nome (2).tprint` em vez de sobrescrever.

## Passo 2: importar e posicionar

1. No menu, clique em **Carregar** em um blueprint salvo. Um contorno dourado com as células
   em azul segue o mouse (o mouse fica no centro da base da construção).
2. Enquanto ele segue o mouse: **.** (ponto) gira 90°, **,** (vírgula) espelha. Os botões
   **Girar** e **Espelhar** fazem o mesmo.
3. Clique esquerdo fixa. Com o menu aberto, as setas do teclado (ou os botões `< ^ v >`) movem 1 bloco.
4. **Mover** volta a seguir o mouse; botão direito durante o movimento volta para onde estava.
   **Remover** apaga a projeção.
5. Saia e entre de novo no mundo: a projeção fixada continua no mesmo lugar.
6. Para importar um arquivo de outra pessoa, coloque o `.tprint` na pasta (botão **Abrir pasta**)
   e clique em **Atualizar**.

Regras de transformação (o padrão combinado):
- Blocos, paredes e rampas giram e espelham. Meio-blocos viram blocos inteiros ao girar.
- Móveis e objetos maiores que 1 bloco: ao espelhar, trocam de lado e viram para o outro lado
  (cadeiras, camas, banheiras...). Ao girar, continuam em pé e só a posição deles gira; os que
  não couberem somem do fantasma e o chat avisa quantos (veja o passo 8).

## Passo 3: fantasma no mundo e no mapa

1. Fixe um blueprint em um lugar vazio. Os blocos aparecem com a textura real, em azul
   translúcido (falta colocar). Paredes aparecem mais apagadas por trás.
2. Coloque um bloco certo: ele some do fantasma (ou fica verde com "Destacar blocos certos").
   Coloque um bloco diferente: fica vermelho. Bloco sólido onde deveria ser vazio: X vermelho.
3. No topo da tela aparece `nome: certos/total (%), faltam X, errados Y`. O mesmo texto
   aparece no menu, na seção Projeção. Ele atualiza sozinho a cada 1/4 de segundo.
4. Minimapa e mapa sobreposto mostram a área colorida. No mapa grande (M), há contorno
   dourado e, passando o mouse por cima, o progresso.
5. Teste uma construção com rampas, meio-blocos e móveis (portas, mesas, cadeiras, tochas)
   para ver se o desenho bate com o original.

## Passo 4: opções e acabamento

1. No menu, a linha de botões **Fantasma / Mapa / Certos** liga e desliga na hora
   (fica salvo, igual às opções). **Opções** abre a tela completa de configurações.
2. Na tela de opções, confira as seções Geral, Captura, Fantasma no mundo, Mapa e Cores.
   Mude a opacidade e as cores e veja o fantasma e o mapa mudarem.
3. **Ponto do mouse**: troque entre Centro da base, Centro e Canto superior esquerdo e carregue
   um blueprint para ver qual ponto segue o mouse.
4. **Mensagens no chat** desligado: as dicas somem, mas avisos e erros continuam.
5. **Excluir** na lista pede confirmação ("Certeza?") e só apaga no segundo clique.
6. Em Configurações > Controles, as teclas do Terraprint aparecem com nome:
   abrir menu (K), girar (.), espelhar (,), mover com as setas.

## Passo 5: lista de materiais

1. Carregue e fixe um blueprint. No menu, clique em **Materiais**: abre uma segunda janela,
   que pode ser arrastada e continua aberta mesmo fechando o menu (K). O X fecha.
2. Cada linha mostra o ícone e o nome do item, e três números: **Total** (quanto o blueprint usa),
   **Falta** (quanto ainda falta colocar) e **Tem** (quanto você tem no inventário, contando
   moedas e munição).
3. Cores: vermelho = você não tem o suficiente para o que falta; amarelo = tem o suficiente;
   verde = já está tudo colocado. Os vermelhos ficam no topo da lista.
4. Coloque alguns blocos certos: **Falta** diminui em até 1/4 de segundo. Pegue ou jogue fora
   itens: **Tem** atualiza sozinho.
5. Móveis contam 1 por objeto (uma mesa 3x2 conta 1 mesa, não 6). Paredes aparecem como
   o item da parede.
6. Coisas que nenhum item coloca (grama, árvores) aparecem como "(sem item)".
7. Passe o mouse em cima de uma linha para ver a dica do item.

## Passo 6: construir automaticamente (segurar)

1. Fixe um blueprint, tenha os materiais no inventário e fique perto dele.
2. No menu, **segure** o botão **Construir (segure)** (fica verde e mostra "Construindo...").
   Os blocos ao seu alcance vão aparecendo aos poucos, os itens saem do inventário e a lista
   de materiais atualiza. Soltar o botão para na hora.
3. A tecla **P** faz o mesmo sem abrir o menu (troque em Configurações > Controles).
4. Ordem: primeiro blocos sólidos, depois paredes, depois móveis e decoração; sempre do mais
   perto para o mais longe. Ande ao longo da construção segurando para cobrir tudo.
5. Posições já ocupadas (por qualquer bloco, certo ou errado) são puladas. Materiais que você
   não tem são pulados. Se nada for colocado, ao soltar aparece um aviso no chat.
6. Rampas e meio-blocos saem iguais ao blueprint. Móveis saem virados para o mesmo lado.
   Baús, placas e afins funcionam normalmente depois de colocados.
7. Você nunca fica preso: blocos sólidos em cima do personagem são pulados até você sair dali.
8. Nas opções, seção **Construção automática**: blocos por tick (1 a 10, padrão 2), e ligar/desligar
   paredes, móveis e rampas.

## Passo 7: multiplayer

O mod é só do cliente: funciona num servidor **sem** o mod (ou com Host & Play). Se der, chame um amigo sem o mod.

1. Entre no servidor, carregue e fixe um blueprint. O fantasma, o mapa e o progresso funcionam igual.
2. Saia e entre de novo: a projeção volta no mesmo lugar. Ela fica salva no seu PC, em
   `Terraprint/Projections` (um arquivo por mundo). Cada jogador tem a sua.
3. Segure **Construir**: os blocos aparecem para todos os jogadores (peça para o amigo conferir).
   No servidor o ritmo é de no máximo 15 blocos por segundo, para o jogo não te expulsar por spam.
4. Rampas, meio-blocos, paredes, móveis virados para o lado certo e baús funcionam para todos.
5. Selecionar e salvar uma área longe de você avisa que parte dela ainda não veio do servidor.
6. Enquanto você seleciona uma área, os outros jogadores continuam usando os itens normalmente.
7. Um mundo de single player que já tinha projeção salva (versão anterior do mod) continua com ela.
8. Num servidor sem o Terraprint, entre com ele ativo: o jogo não pede para baixar nem sincronizar mods,
   e um amigo sem o mod entra normalmente.

## Passo 8: móveis ao espelhar e girar

1. Salve uma construção com cadeiras, camas, banheiras, mesas, portas e baús.
2. Carregue e clique em **Espelhar**: os móveis trocam de lado e cadeiras/camas/banheiras passam a olhar
   para o outro lado no fantasma.
3. Fixe e construa (à mão ou com **Construir**): os móveis colocados ficam virados igual ao fantasma,
   também no multiplayer.
4. Clique em **Girar**: os blocos giram e os móveis continuam aparecendo, em pé, no lugar para onde giraram.
   Se algum não couber (ficaria em cima de outro bloco), ele some e o chat diz quantos blocos de móveis ficaram de fora.
5. Gire 4 vezes: tudo volta exatamente como era.

## Passo 9: menu novo (barra lateral)

1. Aperte **K**. À esquerda fica a barra com **Biblioteca**, **Salvar área**, **Projeção**, **Materiais** e **Opções**
   (a ativa tem uma faixa dourada) e, embaixo, um cartão com a projeção atual e a barra de progresso
   (clique nele para ir à Projeção). O título da página e uma frase de ajuda ficam no topo; **X** fecha.
2. **Biblioteca**: cartões com a prévia, o nome e o tamanho de cada blueprint. Busca por nome no campo
   **buscar...**; a roda do mouse rola. Clique escolhe (borda dourada), clique duplo projeta.
   Embaixo: **Carregar**, **Editar**, **Excluir** (vermelho, pede um segundo clique). No topo: **Atualizar**,
   **Abrir pasta** e **Nova em branco**.
3. **Salvar área**: três passos (selecionar, nome, salvar). Depois de salvar, volta para a Biblioteca com o arquivo novo.
4. **Projeção**: prévia grande, nome e barra de progresso. À direita: Girar/Espelhar, Mover e as setas,
   interruptores de Fantasma, Mapa e Certos, **Construir (segure)**, **Editar** e **Remover**.
5. **Materiais**: igual antes (número = falta colocar, vermelho = falta no inventário).

## Passo 10: opções no menu

1. Em **Opções**, confira as seções **Listas de bloqueio**, **Renderização**, **Camadas**, **Atuador**,
   **Fantasma e mapa** e **Construção automática**. Interruptores mudam na hora; as barras gravam ao soltar o mouse;
   os **- / +** mudam os números.
2. **Blocos ignorados**: clique no **+**, busque "grama" e escolha **Sementes de Grama**. A grama do blueprint some do
   fantasma, do progresso e dos materiais. Clique no ícone na lista para tirar. Faça o mesmo com **Paredes ignoradas** e
   **Parede de Terra** (as paredes de terra naturais também somem).
3. **Piscar o fantasma** ligado: blocos e paredes piscam; os intervalos mudam a velocidade.
4. **Camadas**: desligue blocos, paredes e tinta, um de cada vez, e veja o fantasma mudar.
5. **Atuador**: salve uma área com atuadores e blocos desativados, projete e confira: blocos desativados aparecem
   mais escuros (somem com "Mostrar blocos desativados" desligado). Tire um atuador no mundo: aparece a borda laranja.
6. Construção automática com atuadores no inventário: coloca os atuadores junto.

## Passo 11: editor de schematic

1. Na Biblioteca, escolha um blueprint e clique em **Editar** (ou **Nova em branco**). Abre uma janela própria.
2. Paleta à direita com abas **Blocos**, **Paredes**, **Móveis**, **Decoração** e **Entidades** (baús, molduras,
   manequins...), com busca. Escolha um item e pinte na grade com o botão esquerdo; o direito apaga.
3. Ferramentas: **Retângulo** (arraste), **Borracha** (camada Blocos ou Paredes), **Conta-gotas**, **Martelo**
   (inteiro, meio-bloco, rampas), **Atuador** (esquerdo põe/tira, direito desativa) e **Mão**.
   Roda do mouse = zoom, botão do meio arrasta, **Ctrl+Z / Ctrl+Y** desfazem e refazem.
4. Móveis aparecem inteiros e são apagados inteiros. **Bordas +4** aumenta a área, **Cortar vazio** tira bordas vazias.
5. **Salvar** grava no mesmo arquivo (com * quando há mudanças). Se esse blueprint estiver projetado, o fantasma
   atualiza na hora. **Projetar** salva e coloca no mundo. **X** com mudanças pede confirmação.

## Passo 12: blocos de mods

Teste com Calamity, Thorium ou outro mod de conteúdo ativo.

1. Construa algo com blocos, paredes e móveis do mod e salve a área. Na **Biblioteca**, a linha de baixo mostra
   `Mods: Calamity Mod` (os mods que o blueprint usa).
2. Carregue e fixe: o fantasma mostra os blocos do mod com a textura certa, e **Materiais** lista os itens do mod.
   **Construir** e **Construir tudo** colocam os blocos e móveis do mod.
3. Salve uma área de bioma do mod com paredes naturais (as que não têm item). Em **Materiais** elas aparecem com o
   item da parede que o jogador coloca, e essa parede conta como certa no progresso.
4. No editor, busque o nome do mod na paleta (ex.: "calamity"): aparecem só os itens dele. A dica do item diz o mod.
5. Desative o mod e recarregue. Na **Biblioteca** aparece `(inativos: CalamityMod)`. Ao carregar, o chat avisa quais
   mods faltam e esses blocos ficam fora do fantasma. Abra no **Editor**, mude um bloco vanilla e salve.
   Ative o mod de novo: os blocos do mod continuam no blueprint.

## Passo 13: Magic Storage (opcional)

Só com o Magic Storage ativo. Sem ele, nada muda.

1. Fixe um blueprint e abra o terminal do storage (coração, acesso ou acesso remoto). Ao lado do painel do storage
   aparece o botão **Terraprint**. Ele some ao fechar o storage e pode ser desligado em **Opções > Outros mods**.
2. Clique nele: abre o painel com o resumo do storage (unidades, espaços, itens e tipos), o blueprint (nome,
   tamanho, distância e progresso) e a lista de materiais com **Falta**, **Inventário** e **Storage**.
   Verde: o inventário já basta; amarelo: o storage cobre; vermelho: não tem o suficiente. Passe o mouse para ver a dica.
3. Arraste o painel do storage ou mude a altura dele: o botão e o painel do Terraprint acompanham.
4. **Pegar o que falta** tira do storage o que falta (descontando o inventário) e põe no inventário. Com o inventário
   cheio, o que não couber volta para o storage e o chat avisa.
5. Clique esquerdo num material com receita (a dica do item avisa): o terminal do storage dá lugar ao Crafting Terminal
   conectado, já com a receita do item selecionada (a primeira que dá para fazer agora, se houver mais de uma).
   Sem Crafting Terminal no storage, ou item sem receita, o chat avisa. Com o terminal longe ou aberto pelo acesso
   portátil, ele abre como acesso remoto.
6. Sem blueprint fixado, o painel avisa e o botão fica desligado. Em multiplayer, teste num servidor com o
   Magic Storage: os itens chegam pelo próprio storage.
