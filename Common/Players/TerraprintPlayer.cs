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
		private static bool Moving => ProjectionSystem.State == ProjectionSystem.ProjectionState.Moving;

		/// <summary>Alguma ferramenta do Terraprint esta capturando o mouse no mundo.</summary>
		public static bool ToolActive => SelectionSystem.Active || Moving;

		private static bool _waitForRelease;

		/// <summary>
		/// Ignora o clique atual ate o botao ser solto. Evita que o clique no botao do menu
		/// (ex.: Carregar) ja fixe o blueprint ou marque um canto no mesmo quadro.
		/// </summary>
		public static void SwallowClick() => _waitForRelease = true;

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (Main.drawingPlayerChat || PlayerInput.WritingText || UISystem.Typing)
				return;

			if (KeybindSystem.ToggleMenu.JustPressed)
				UISystem.ToggleMenu();

			if (ProjectionSystem.HasProjection && (Moving || UISystem.MenuOpen)) {
				if (KeybindSystem.Rotate.JustPressed)
					ProjectionSystem.Rotate();
				if (KeybindSystem.Flip.JustPressed)
					ProjectionSystem.Flip();
			}

			if (ProjectionSystem.State == ProjectionSystem.ProjectionState.Fixed && UISystem.MenuOpen) {
				if (KeybindSystem.NudgeUp.JustPressed) ProjectionSystem.Nudge(0, -1);
				if (KeybindSystem.NudgeDown.JustPressed) ProjectionSystem.Nudge(0, 1);
				if (KeybindSystem.NudgeLeft.JustPressed) ProjectionSystem.Nudge(-1, 0);
				if (KeybindSystem.NudgeRight.JustPressed) ProjectionSystem.Nudge(1, 0);
			}

			if (KeybindSystem.AutoBuild.Current)
				AutoBuildSystem.RequestHold();

			if (_waitForRelease) {
				if (!Main.mouseLeft)
					_waitForRelease = false;
				return;
			}

			if (!ToolActive)
				return;

			bool overUI = Player.mouseInterface || UISystem.MouseOverMenu;

			if (Main.mouseRight && Main.mouseRightRelease) {
				if (SelectionSystem.Active) {
					SelectionSystem.Cancel();
					Lang.Info(Lang.UI("Cancelled"));
				}
				else
					ProjectionSystem.CancelMoving();
				return;
			}

			if (overUI || !(Main.mouseLeft && Main.mouseLeftRelease))
				return;

			if (SelectionSystem.Active)
				SelectionSystem.Click(Main.MouseWorld.ToTileCoordinates());
			else if (Moving)
				ProjectionSystem.Fix();
		}

		// Enquanto uma ferramenta usa o mouse, nao deixa o item da mao ser usado (minerar, atacar...).
		public override bool CanUseItem(Item item) => !ToolActive;
	}
}
