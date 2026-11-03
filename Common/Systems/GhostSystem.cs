using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terraprint.Common.Blueprints;
using Terraprint.Common.Configs;
using Terraprint.Common.UI;

namespace Terraprint.Common.Systems
{
	/// <summary>
	/// Desenha o fantasma do blueprint no mundo, por cima dos blocos:
	/// azul = falta colocar, vermelho = errado ou precisa remover, verde = certo (opcional).
	/// As camadas (blocos, paredes, tinta), o piscar e os atuadores seguem as opcoes.
	/// </summary>
	[Autoload(Side = ModSide.Client)]
	public class GhostSystem : ModSystem
	{
		private static readonly Color ActuatorColor = new(255, 150, 40);

		public override void PostDrawTiles()
		{
			var cfg = TerraprintConfig.Instance;
			if (!ProjectionSystem.HasProjection || !cfg.ShowGhost)
				return;

			var bp = ProjectionSystem.Current;
			var o = ProjectionSystem.Origin;
			bool moving = ProjectionSystem.State == ProjectionSystem.ProjectionState.Moving;
			var tiles = ProgressTracker.TileStatus;
			var walls = ProgressTracker.WallStatus;
			bool haveStatus = tiles.Length == bp.Cells.Length;

			// So percorre o pedaco visivel na tela.
			int x0 = System.Math.Max(0, (int)(Main.screenPosition.X / 16f) - o.X - 2);
			int y0 = System.Math.Max(0, (int)(Main.screenPosition.Y / 16f) - o.Y - 2);
			int x1 = System.Math.Min(bp.Width - 1, (int)((Main.screenPosition.X + Main.screenWidth) / 16f) - o.X + 2);
			int y1 = System.Math.Min(bp.Height - 1, (int)((Main.screenPosition.Y + Main.screenHeight) / 16f) - o.Y + 2);

			var sb = Main.spriteBatch;
			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None,
				Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			float alpha = cfg.GhostOpacity;
			float tileAlpha = alpha * Blink(cfg.EnableBlinking, cfg.TileBlinkDelay);
			float wallAlpha = alpha * 0.7f * Blink(cfg.EnableBlinking, cfg.WallBlinkDelay);

			// Paredes primeiro, blocos por cima.
			if (cfg.ShowWalls && wallAlpha > 0f) {
				for (int y = y0; y <= y1; y++) {
					for (int x = x0; x <= x1; x++) {
						int i = y * bp.Width + x;
						ref BlueprintCell c = ref bp.Cells[i];
						if (c.WallType == WallID.None)
							continue;
						var status = haveStatus && !moving ? walls[i] : CellStatus.Missing;
						Color? color = StatusColor(status, cfg);
						if (!color.HasValue)
							continue;
						Color col = cfg.ShowPaint ? WithPaint(color.Value, c.WallPaint) : color.Value;
						CellRenderer.DrawWall(sb, c, ScreenPos(o.X + x, o.Y + y), col * wallAlpha);
					}
				}
			}

			int border = System.Math.Clamp(cfg.ActuatorBorder, 1, 4);
			for (int y = y0; y <= y1; y++) {
				for (int x = x0; x <= x1; x++) {
					int i = y * bp.Width + x;
					ref BlueprintCell c = ref bp.Cells[i];
					var pos = ScreenPos(o.X + x, o.Y + y);

					if (cfg.ShowLiquids && c.LiquidAmount > 0 && !c.HasTile)
						CellRenderer.DrawLiquid(sb, c, pos, alpha);

					var status = haveStatus && !moving ? tiles[i] : (c.HasTile ? CellStatus.Missing : CellStatus.None);
					if (!c.HasTile) {
						if (status == CellStatus.Wrong) // obstrucao: algo solido onde deveria estar vazio
							CellRenderer.DrawCross(sb, pos, cfg.ColorWrong * alpha);
						continue;
					}

					// Atuador diferente do blueprint (falta, sobra, ou bloco ligado/desligado diferente).
					if (cfg.HighlightActuatorMismatch && !moving && ActuatorMismatch(c, o.X + x, o.Y + y))
						CellRenderer.DrawBorder(sb, new Rectangle((int)pos.X, (int)pos.Y, 16, 16), ActuatorColor * System.Math.Max(alpha, 0.6f), border);

					if (!cfg.ShowTiles || tileAlpha <= 0f)
						continue;
					if (c.Inactive && !cfg.ShowActuated)
						continue;
					Color? color = StatusColor(status, cfg);
					if (!color.HasValue)
						continue;
					Color col = cfg.ShowPaint ? WithPaint(color.Value, c.TilePaint) : color.Value;
					float a = tileAlpha;
					if (c.Inactive) {
						// Bloco desativado: mais escuro e com a opacidade propria, como o jogo mostra.
						col = Color.Lerp(col, Color.Black, 0.35f);
						a *= cfg.ActuatedOpacity;
					}
					CellRenderer.DrawTile(sb, c, pos, col * a);
				}
			}

			if (cfg.ShowOutline || moving) {
				var r = new Rectangle(o.X * 16 - (int)Main.screenPosition.X, o.Y * 16 - (int)Main.screenPosition.Y, bp.Width * 16, bp.Height * 16);
				SelectionSystem.DrawRect(sb, r, Color.Transparent, (moving ? Color.Gold : Color.Gold * 0.5f), 2);
			}

			sb.End();
		}

		/// <summary>Piscar: alterna entre cheio e quase apagado a cada "delay" ticks.</summary>
		private static float Blink(bool enabled, int delay)
		{
			if (!enabled || delay <= 0)
				return 1f;
			return Main.GameUpdateCount / (uint)delay % 2 == 0 ? 1f : 0.15f;
		}

		private static Color WithPaint(Color status, byte paint)
		{
			var p = CellRenderer.PaintColor(paint);
			return p.HasValue ? Color.Lerp(status, p.Value, 0.55f) : status;
		}

		private static bool ActuatorMismatch(in BlueprintCell c, int wx, int wy)
		{
			if (!WorldGen.InWorld(wx, wy, 1))
				return false;
			Tile t = Main.tile[wx, wy];
			if (!t.HasTile || t.TileType != c.TileType)
				return false;
			return t.HasActuator != c.HasActuator || t.IsActuated != c.Inactive;
		}

		private static Vector2 ScreenPos(int tx, int ty) => new Vector2(tx * 16, ty * 16) - Main.screenPosition;

		private static Color? StatusColor(CellStatus s, TerraprintConfig cfg) => s switch {
			CellStatus.Missing => cfg.ColorMissing,
			CellStatus.Wrong => cfg.ColorWrong,
			CellStatus.Correct when cfg.ShowCorrect => cfg.ColorCorrect * 0.6f,
			_ => null,
		};

		// --- Texto de progresso no topo da tela ---

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Inventory");
			if (index < 0)
				return;
			layers.Insert(index, new LegacyGameInterfaceLayer("Terraprint: Progress", () => {
				DrawProgress(Main.spriteBatch);
				return true;
			}, InterfaceScaleType.UI));
		}

		private static void DrawProgress(SpriteBatch sb)
		{
			var cfg = TerraprintConfig.Instance;
			if (!cfg.ShowProgress || ProjectionSystem.State != ProjectionSystem.ProjectionState.Fixed || Main.gameMenu)
				return;
			string text = ProgressText();
			var size = FontAssets.MouseText.Value.MeasureString(text);
			var pos = new Vector2(Main.screenWidth / 2f - size.X / 2f, 84f);
			Utils.DrawBorderString(sb, text, pos, ProgressTracker.Percent >= 1f ? cfg.ColorCorrect : Color.White);
		}

		public static string ProgressText() => Lang.UI("Progress",
			ProjectionSystem.Current.Name,
			ProgressTracker.Correct, ProgressTracker.Total,
			(int)(ProgressTracker.Percent * 100f),
			ProgressTracker.Missing, ProgressTracker.Wrong);
	}
}
