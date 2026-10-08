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
