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
- Móveis e objetos maiores que 1 bloco só espelham (o objeto troca de lado, mas o sprite
  continua virado para o mesmo lado). Ao girar, eles somem do fantasma e o chat avisa quantos.
