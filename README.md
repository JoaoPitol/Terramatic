# Terraprint

Mod para tModLoader que salva construções do Terraria como blueprints e projeta um
"fantasma" delas no mundo e no mapa, para você construir seguindo o molde.

## Como usar

1. Aperte **K** para abrir o menu.
2. **Selecionar área**: clique em dois cantos de uma construção. Dê um nome e clique **Salvar**.
   O arquivo vai para `Documents/My Games/Terraria/tModLoader/Terraprint/*.tprint`.
3. **Carregar** um blueprint da lista: ele segue o mouse. **.** gira, **,** espelha,
   clique esquerdo fixa, botão direito cancela.
4. Com o blueprint fixado: azul = falta colocar, vermelho = errado ou precisa remover,
   verde = certo (opcional). O progresso aparece no topo da tela, no menu e no mapa.
5. Para usar o blueprint de outra pessoa, coloque o `.tprint` na pasta (**Abrir pasta**) e clique
   em **Atualizar**.

## Limitações conhecidas

- Multiplayer: o servidor e todos os jogadores precisam do mod. Cada jogador tem a sua projeção, salva no próprio PC
  (pasta Terraprint/Projections, um arquivo por mundo). A construção automática coloca no máximo
  15 blocos por segundo em servidores, para não cair no anti-spam do jogo.
- Girar: blocos, paredes e rampas giram; móveis continuam em pé e só a posição deles gira
  (os que não couberem ficam de fora). Ao espelhar, móveis com versão para o outro lado viram junto.
- Fios, atuadores e itens dentro de baús não são salvos.
- Uma projeção por mundo.

## Estrutura

| Pasta | O que tem |
|---|---|
| `Common/Blueprints` | Formato do blueprint, leitura/escrita `.tprint`, girar/espelhar |
| `Common/Systems` | Seleção, projeção, fantasma, progresso, mapa, teclas, UI |
| `Common/UI` | Menu e elementos (campo de texto, botões, painel arrastável) |
| `Common/Configs` | Opções do mod |
| `Localization` | Textos em português (pt-BR) e inglês (en-US) |

O formato `.tprint` é um TagCompound comprimido: tamanho, paletas de tipos de bloco/parede por
nome (`Terraria/Dirt`, `OutroMod/Tijolo`) e as células em binário. Blueprints que usam blocos de
mods desativados carregam mesmo assim; esses blocos são ignorados com aviso no chat.
