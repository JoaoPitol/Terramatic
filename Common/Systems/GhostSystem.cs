using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.UI;
using Terraprint.Common.Blueprints;
using Terraprint.Common.Configs;

namespace Terraprint.Common.Systems
{
	/// <summary>
	/// Desenha o fantasma do blueprint no mundo, por cima dos blocos:
	/// azul = falta colocar, vermelho = errado ou precisa remover, verde = certo (opcional).
	/// </summary>
	[Autoload(Side = ModSide.Client)]
	public class GhostSystem : ModSystem
	{
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

			// Paredes primeiro, blocos por cima.
			if (cfg.ShowWalls) {
				for (int y = y0; y <= y1; y++) {
					for (int x = x0; x <= x1; x++) {
						int i = y * bp.Width + x;
						ref BlueprintCell c = ref bp.Cells[i];
						if (c.WallType == WallID.None)
							continue;
						var status = haveStatus && !moving ? walls[i] : CellStatus.Missing;
						Color? color = StatusColor(status, cfg);
						if (color.HasValue)
							DrawWall(sb, c, ScreenPos(o.X + x, o.Y + y), color.Value * (alpha * 0.7f));
					}
				}
			}

			for (int y = y0; y <= y1; y++) {
				for (int x = x0; x <= x1; x++) {
					int i = y * bp.Width + x;
					ref BlueprintCell c = ref bp.Cells[i];
					var pos = ScreenPos(o.X + x, o.Y + y);

					if (cfg.ShowLiquids && c.LiquidAmount > 0 && !c.HasTile)
						DrawLiquid(sb, c, pos, alpha);

					var status = haveStatus && !moving ? tiles[i] : (c.HasTile ? CellStatus.Missing : CellStatus.None);
					if (!c.HasTile) {
						if (status == CellStatus.Wrong) // obstrucao: algo solido onde deveria estar vazio
							DrawCross(sb, pos, cfg.ColorWrong * alpha);
						continue;
					}
					Color? color = StatusColor(status, cfg);
					if (color.HasValue)
						DrawTile(sb, c, pos, color.Value * alpha);
				}
			}

			if (cfg.ShowOutline || moving) {
				var r = new Rectangle(o.X * 16 - (int)Main.screenPosition.X, o.Y * 16 - (int)Main.screenPosition.Y, bp.Width * 16, bp.Height * 16);
				SelectionSystem.DrawRect(sb, r, Color.Transparent, (moving ? Color.Gold : Color.Gold * 0.5f), 2);
			}

			sb.End();
		}

		private static Vector2 ScreenPos(int tx, int ty) => new Vector2(tx * 16, ty * 16) - Main.screenPosition;

		private static Color? StatusColor(CellStatus s, TerraprintConfig cfg) => s switch {
			CellStatus.Missing => cfg.ColorMissing,
			CellStatus.Wrong => cfg.ColorWrong,
			CellStatus.Correct when cfg.ShowCorrect => cfg.ColorCorrect * 0.6f,
			_ => null,
		};

		private static void DrawWall(SpriteBatch sb, in BlueprintCell c, Vector2 pos, Color color)
		{
			Main.instance.LoadWall(c.WallType);
			var tex = TextureAssets.Wall[c.WallType].Value;
			// Miolo de um quadro de parede (36x36 por quadro): textura lisa, sem bordas.
			sb.Draw(tex, pos, new Rectangle(44, 44, 16, 16), color);
		}

		private static void DrawLiquid(SpriteBatch sb, in BlueprintCell c, Vector2 pos, float alpha)
		{
			Color color = (int)c.LiquidType switch {
				LiquidID.Lava => new Color(255, 120, 30),
				LiquidID.Honey => new Color(255, 200, 40),
				LiquidID.Shimmer => new Color(220, 150, 255),
				_ => new Color(40, 110, 255),
			};
			int h = System.Math.Max(1, c.LiquidAmount * 16 / 255);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)pos.X, (int)pos.Y + 16 - h, 16, h), color * (alpha * 0.5f));
		}

		private static void DrawCross(SpriteBatch sb, Vector2 pos, Color color)
		{
			var px = TextureAssets.MagicPixel.Value;
			for (int k = 0; k < 16; k += 2) {
				sb.Draw(px, new Rectangle((int)pos.X + k, (int)pos.Y + k, 2, 2), color);
				sb.Draw(px, new Rectangle((int)pos.X + 14 - k, (int)pos.Y + k, 2, 2), color);
			}
		}

		private static void DrawTile(SpriteBatch sb, in BlueprintCell c, Vector2 pos, Color color)
		{
			Main.instance.LoadTiles(c.TileType);
			var tex = TextureAssets.Tile[c.TileType].Value;

			var data = TileObjectData.GetTileData(c.TileType, 0);
			bool multi = data != null && (data.Width > 1 || data.Height > 1);

			if (multi || (!Main.tileSolid[c.TileType] && c.FrameX >= 0)) {
				// Objetos: usa o quadro original do sprite.
				int w = data?.CoordinateWidth ?? 16;
				int h = RowHeight(data, c.FrameY);
				int yOff = data?.DrawYOffset ?? 0;
				var src = new Rectangle(c.FrameX, c.FrameY, w, h);
				if (src.Right <= tex.Width && src.Bottom <= tex.Height)
					sb.Draw(tex, pos + new Vector2(0, yOff), src, color);
				else
					sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)pos.X, (int)pos.Y, 16, 16), color * 0.6f);
				return;
			}

			// Blocos: quadro original, ou um quadro "bloco inteiro" quando foi girado/espelhado.
			int fx = c.FrameX >= 0 ? c.FrameX : 18;
			int fy = c.FrameX >= 0 ? c.FrameY : 18;
			if (fx + 16 > tex.Width || fy + 16 > tex.Height) {
				fx = 18;
				fy = 18;
			}

			if (c.HalfBlock) {
				sb.Draw(tex, pos + new Vector2(0, 8), new Rectangle(fx, fy, 16, 8), color);
				return;
			}
			if (c.Slope == 0) {
				sb.Draw(tex, pos, new Rectangle(fx, fy, 16, 16), color);
				return;
			}

			// Rampas em tiras de 2px, como o jogo faz.
			for (int k = 0; k < 8; k++) {
				int col = k * 2;
				int top, height;
				switch (c.Slope) {
					case 1: top = col; height = 16 - col; break;           // base + esquerda solidas
					case 2: top = 14 - col; height = col + 2; break;       // base + direita
					case 3: top = 0; height = 16 - col; break;             // topo + esquerda
					default: top = 0; height = col + 2; break;             // topo + direita
				}
				sb.Draw(tex, pos + new Vector2(col, top), new Rectangle(fx + col, fy + top, 2, height), color);
			}
		}

		/// <summary>Altura da linha do objeto a que este FrameY pertence (alguns objetos tem a ultima linha com 18px).</summary>
		private static int RowHeight(TileObjectData data, short frameY)
		{
			if (data?.CoordinateHeights == null || data.CoordinateHeights.Length == 0)
				return 16;
			int full = data.CoordinateFullHeight;
			int fy = full > 0 ? frameY % full : frameY;
			int acc = 0;
			foreach (int h in data.CoordinateHeights) {
				if (fy < acc + h + data.CoordinatePadding)
					return h;
				acc += h + data.CoordinatePadding;
			}
			return 16;
		}

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
