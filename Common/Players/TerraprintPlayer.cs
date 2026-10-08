using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraprint.Common.Systems;
using Terraprint.Common.UI;

namespace Terraprint.Common.Players
{
	/// <summary>Le teclas e cliques do jogador local e repassa para as ferramentas do mod.</summary>
	public class TerraprintPlayer : ModPlayer
	{
		/// <summary>Alguma ferramenta do Terraprint esta capturando o mouse no mundo.</summary>
		public static bool ToolActive => SelectionSystem.Active;

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (KeybindSystem.ToggleMenu.JustPressed)
				UISystem.ToggleMenu();

			if (!ToolActive || Main.drawingPlayerChat || PlayerInput.WritingText)
				return;

			bool overUI = Player.mouseInterface || UISystem.MouseOverMenu;

			if (Main.mouseRight && Main.mouseRightRelease) {
				SelectionSystem.Cancel();
				Main.NewText(Lang.UI("Cancelled"));
				return;
			}

			if (!overUI && Main.mouseLeft && Main.mouseLeftRelease)
				SelectionSystem.Click(Main.MouseWorld.ToTileCoordinates());
		}

		// Enquanto uma ferramenta usa o mouse, nao deixa o item da mao ser usado (minerar, atacar...).
		public override bool CanUseItem(Item item) => !ToolActive;
	}
}
