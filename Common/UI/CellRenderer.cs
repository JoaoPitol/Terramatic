using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ObjectData;
using Terraprint.Common.Blueprints;

namespace Terraprint.Common.UI
{
	/// <summary>
	/// Desenha uma celula de blueprint com as texturas do jogo, em qualquer escala.
	/// Usado pelo fantasma no mundo (escala 1, 16 px por bloco) e pelo editor (com zoom).
	/// </summary>
	public static class CellRenderer
	{
		private static Texture2D Pixel => TextureAssets.MagicPixel.Value;

		public static void DrawWall(SpriteBatch sb, in BlueprintCell c, Vector2 pos, Color color, float scale = 1f)
		{
			if (c.WallType <= WallID.None || c.WallType >= TextureAssets.Wall.Length)
				return;
			Main.instance.LoadWall(c.WallType);
			var tex = TextureAssets.Wall[c.WallType].Value;
			// Miolo de um quadro de parede (36x36 por quadro): textura lisa, sem bordas.
			sb.Draw(tex, pos, new Rectangle(44, 44, 16, 16), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		public static Color LiquidColor(byte type) => (int)type switch {
			LiquidID.Lava => new Color(255, 120, 30),
			LiquidID.Honey => new Color(255, 200, 40),
			LiquidID.Shimmer => new Color(220, 150, 255),
			_ => new Color(40, 110, 255),
		};

		public static void DrawLiquid(SpriteBatch sb, in BlueprintCell c, Vector2 pos, float alpha, float scale = 1f)
		{
			float size = 16f * scale;
			float h = System.Math.Max(1f, c.LiquidAmount * size / 255f);
			sb.Draw(Pixel, new Rectangle((int)pos.X, (int)(pos.Y + size - h), (int)System.Math.Ceiling(size), (int)System.Math.Ceiling(h)), LiquidColor(c.LiquidType) * (alpha * 0.5f));
		}

		public static void DrawCross(SpriteBatch sb, Vector2 pos, Color color, float scale = 1f)
		{
			int d = System.Math.Max(1, (int)(2 * scale));
			for (int k = 0; k < 16; k += 2) {
				sb.Draw(Pixel, new Rectangle((int)(pos.X + k * scale), (int)(pos.Y + k * scale), d, d), color);
				sb.Draw(Pixel, new Rectangle((int)(pos.X + (14 - k) * scale), (int)(pos.Y + k * scale), d, d), color);
			}
		}

		/// <summary>Contorno de um retangulo (borda para dentro).</summary>
		public static void DrawBorder(SpriteBatch sb, Rectangle r, Color color, int thickness)
		{
			sb.Draw(Pixel, new Rectangle(r.X, r.Y, r.Width, thickness), color);
			sb.Draw(Pixel, new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), color);
			sb.Draw(Pixel, new Rectangle(r.X, r.Y + thickness, thickness, r.Height - 2 * thickness), color);
			sb.Draw(Pixel, new Rectangle(r.Right - thickness, r.Y + thickness, thickness, r.Height - 2 * thickness), color);
		}

		public static void DrawTile(SpriteBatch sb, in BlueprintCell c, Vector2 pos, Color color, float scale = 1f)
		{
			if (c.TileType >= TextureAssets.Tile.Length)
				return;
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
				if (c.FrameX >= 0 && c.FrameY >= 0 && src.Right <= tex.Width && src.Bottom <= tex.Height)
					sb.Draw(tex, pos + new Vector2(0, yOff * scale), src, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
				else
					sb.Draw(Pixel, new Rectangle((int)pos.X, (int)pos.Y, (int)(16 * scale), (int)(16 * scale)), color * 0.6f);
				return;
			}

			// Blocos: quadro original, ou um quadro "bloco inteiro" quando foi girado/espelhado ou criado no editor.
			int fx = c.FrameX >= 0 ? c.FrameX : 18;
			int fy = c.FrameX >= 0 ? c.FrameY : 18;
			if (fx + 16 > tex.Width || fy + 16 > tex.Height) {
				fx = 18;
				fy = 18;
			}

			if (c.HalfBlock) {
				sb.Draw(tex, pos + new Vector2(0, 8 * scale), new Rectangle(fx, fy, 16, 8), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
				return;
			}
			if (c.Slope == 0) {
				sb.Draw(tex, pos, new Rectangle(fx, fy, 16, 16), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
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
				sb.Draw(tex, pos + new Vector2(col, top) * scale, new Rectangle(fx + col, fy + top, 2, height), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
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

		/// <summary>Cor da tinta (0 = sem tinta).</summary>
		public static Color? PaintColor(byte paint)
		{
			if (paint == 0)
				return null;
			try {
				return WorldGen.paintColor(paint);
			}
			catch {
				return null;
			}
		}
	}
}
