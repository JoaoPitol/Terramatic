using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
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

		/// <summary>
		/// Modo NoClip do jogador local: atravessa blocos e voa livremente (WASD/setas, pulo sobe, Shift acelera).
		/// Serve para sair de dentro de construcoes, por exemplo quando a construcao automatica fecha o jogador.
		/// </summary>
		public static bool NoClip { get; private set; }

		private const float NoClipSpeed = 8f;
		private const float NoClipFastSpeed = 22f;

		/// <summary>
		/// Posicao "verdadeira" do jogador no NoClip. O jogo mexe na posicao por conta propria
		/// (degraus, rampas, esteiras, empurrao ao bater a cabeca num bloco), entao guardamos a nossa
		/// e reaplicamos no fim de cada tick.
		/// </summary>
		private static Vector2? _noClipPos;
		/// <summary>Distancia acima da qual a mudanca de posicao foi um teleporte de verdade (espelho magico, morte...).</summary>
		private const float TeleportThreshold = 64f;

		public static void SetNoClip(bool on)
		{
			if (NoClip == on)
				return;
			NoClip = on;
			_noClipPos = null;
			Player p = Main.LocalPlayer;
			if (p != null && p.active) {
				p.velocity = Vector2.Zero;
				p.fallStart = (int)(p.position.Y / 16f);
			}
			Lang.Info(Lang.UI(on ? "NoClipOn" : "NoClipOff"), on ? Color.LightGreen : Color.White);
		}

		// So roda para o jogador local, tanto em single player quanto ao entrar num servidor.
		public override void OnEnterWorld()
		{
			NoClip = false;
			_noClipPos = null;
			ProjectionSystem.OnEnterWorld();
		}

		public override void PreUpdateMovement()
		{
			if (!NoClip || Player.whoAmI != Main.myPlayer || Player.dead)
				return;

			Vector2 dir = Vector2.Zero;
			if (Player.controlLeft) dir.X -= 1f;
			if (Player.controlRight) dir.X += 1f;
			if (Player.controlUp || Player.controlJump) dir.Y -= 1f;
			if (Player.controlDown) dir.Y += 1f;
			if (dir != Vector2.Zero)
				dir.Normalize();
			if (dir.X != 0f)
				Player.direction = dir.X > 0f ? 1 : -1;

			// Parte da posicao guardada, ignorando os ajustes que o jogo fez neste tick (degraus, rampas...).
			// Se a diferenca for grande, foi um teleporte de verdade e vale a posicao nova.
			Vector2 basePos = _noClipPos is Vector2 saved && Vector2.Distance(saved, Player.position) < TeleportThreshold
				? saved : Player.position;

			float speed = Main.keyState.PressingShift() ? NoClipFastSpeed : NoClipSpeed;
			Vector2 pos = basePos + dir * speed;
			// Nao deixa sair pelas bordas do mundo.
			pos.X = MathHelper.Clamp(pos.X, Main.leftWorld + 41 * 16f, Main.rightWorld - 42 * 16f - Player.width);
			pos.Y = MathHelper.Clamp(pos.Y, Main.topWorld + 41 * 16f, Main.bottomWorld - 42 * 16f - Player.height);

			// Velocidade zero: o jogo nao calcula colisao. O resto dos ajustes e desfeito no PostUpdate.
			_noClipPos = pos;
			Player.position = pos;
			Player.velocity = Vector2.Zero;
			Player.jump = 0;
			Player.gravity = 0f;
			Player.fallStart = (int)(Player.position.Y / 16f);
		}

		/// <summary>
		/// Roda no fim do tick, depois da colisao do jogo. Desfaz rampas, degraus, esteiras e o empurrao
		/// de bater a cabeca, que faziam o jogador "seguir" blocos ou travar ao subir por dentro deles.
		/// </summary>
		public override void PostUpdate()
		{
			if (!NoClip || Player.whoAmI != Main.myPlayer || Player.dead || _noClipPos is not Vector2 pos)
				return;
			if (Vector2.Distance(pos, Player.position) >= TeleportThreshold) {
				_noClipPos = Player.position;
				return;
			}
			Player.position = pos;
			Player.oldPosition = pos;
			Player.velocity = Vector2.Zero;
			Player.gfxOffY = 0f;
			Player.stepSpeed = 1f;
		}

		public override void PostUpdateMiscEffects()
		{
			if (!NoClip || Player.whoAmI != Main.myPlayer)
				return;
			// Dentro de blocos o jogo conta como afogamento/sufocamento; no NoClip isso nao vale.
			Player.breath = Player.breathMax;
			Player.buffImmune[BuffID.Suffocation] = true;
			Player.noFallDmg = true;
		}

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (Main.drawingPlayerChat || PlayerInput.WritingText || UISystem.Typing)
				return;

			if (KeybindSystem.ToggleMenu.JustPressed)
				UISystem.ToggleMenu();

			if (ProjectionSystem.HasProjection && (Moving || UISystem.MenuOpen && !UISystem.EditorOpen)) {
				if (KeybindSystem.Rotate.JustPressed)
					ProjectionSystem.Rotate();
				if (KeybindSystem.Flip.JustPressed)
					ProjectionSystem.Flip();
			}

			if (ProjectionSystem.State == ProjectionSystem.ProjectionState.Fixed && UISystem.MenuOpen && !UISystem.EditorOpen) {
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
		// So vale para o jogador local: no multiplayer os outros jogadores continuam usando seus itens.
		public override bool CanUseItem(Item item) => Player.whoAmI != Main.myPlayer || !ToolActive;
	}
}
