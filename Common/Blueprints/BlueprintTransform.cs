using Terraria;
using Terraria.Enums;
using Terraria.ObjectData;

namespace Terraprint.Common.Blueprints
{
	/// <summary>
	/// Espelhar e girar blueprints.
	/// - Blocos e paredes: giram 90 graus e espelham. Rampas sao convertidas para a orientacao certa.
	/// - Moveis e objetos de varios tiles: ao espelhar, o objeto muda de lugar e, quando o jogo tem a versao
	///   virada para o outro lado (cadeira, cama, banheira...), passa a usar essa versao.
	///   Ao girar, o objeto continua em pe: so a posicao dele gira. Se nao couber, fica de fora.
	/// </summary>
	public static class BlueprintTransform
	{
		/// <summary>Marca em FrameX: o enquadramento original nao vale mais, o fantasma usa um quadro generico.</summary>
		public const short Reframe = -1;

		public static Blueprint Apply(Blueprint source, int rotation, bool flipX, out int droppedTiles)
		{
			droppedTiles = 0;
			Blueprint bp = source;
			if (flipX)
				bp = FlipX(bp);
			for (int i = 0; i < ((rotation % 4) + 4) % 4; i++)
				bp = RotateCW(bp, ref droppedTiles);
			return bp;
		}

		/// <summary>Largura/altura do objeto e a coluna deste tile dentro dele; null se nao for objeto multi-tile.</summary>
		public static bool TryGetMultitile(in BlueprintCell c, out int width, out int column)
		{
			width = 1;
			column = 0;
			if (!c.HasTile)
				return false;
			var data = TileObjectData.GetTileData(c.TileType, 0);
			if (data == null || (data.Width <= 1 && data.Height <= 1))
				return false;

			width = data.Width;
			int step = data.CoordinateWidth + data.CoordinatePadding;
			int full = data.CoordinateFullWidth;
			if (step > 0 && full > 0 && c.FrameX >= 0)
				column = System.Math.Clamp((c.FrameX % full) / step, 0, width - 1);
			return true;
		}

		public static bool IsMultitile(in BlueprintCell c) => TryGetMultitile(c, out _, out _);

		private static Blueprint NewLike(Blueprint s, int w, int h) => new(w, h) {
			Name = s.Name, Author = s.Author, CreatedUtc = s.CreatedUtc,
		};

		private static void CopyTile(in BlueprintCell from, ref BlueprintCell to)
		{
			to.HasTile = from.HasTile;
			to.TileType = from.TileType;
			to.FrameX = from.FrameX;
			to.FrameY = from.FrameY;
			to.Slope = from.Slope;
			to.HalfBlock = from.HalfBlock;
			to.TilePaint = from.TilePaint;
		}

		private static void CopyWallAndLiquid(in BlueprintCell from, ref BlueprintCell to)
		{
			to.WallType = from.WallType;
			to.WallPaint = from.WallPaint;
			to.LiquidAmount = from.LiquidAmount;
			to.LiquidType = from.LiquidType;
		}

		public static byte MirrorSlope(byte slope) => slope switch {
			1 => 2, 2 => 1, 3 => 4, 4 => 3, _ => slope,
		};

		/// <summary>Rampa apos girar 90 graus no sentido horario (o lado solido gira junto).</summary>
		public static byte RotateSlopeCW(byte slope) => slope switch {
			1 => 3, 2 => 1, 3 => 4, 4 => 2, _ => slope,
		};

		public static Blueprint FlipX(Blueprint s)
		{
			var d = NewLike(s, s.Width, s.Height);
			for (int y = 0; y < s.Height; y++) {
				for (int x = 0; x < s.Width; x++) {
					ref BlueprintCell c = ref s[x, y];
					CopyWallAndLiquid(c, ref d[s.Width - 1 - x, y]);
					if (!c.HasTile)
						continue;

					if (TryGetMultitile(c, out int w, out int col)) {
						// O objeto inteiro muda de lugar; cada tile mantem sua coluna dentro dele.
						int nx = s.Width - x - w + 2 * col;
						if (nx >= 0 && nx < s.Width) {
							CopyTile(c, ref d[nx, y]);
							MirrorFacing(ref d[nx, y]);
						}
					}
					else {
						ref BlueprintCell t = ref d[s.Width - 1 - x, y];
						CopyTile(c, ref t);
						t.Slope = MirrorSlope(c.Slope);
						if (Main.tileSolid[c.TileType])
							t.FrameX = Reframe;
					}
				}
			}
			return d;
		}

		public static Blueprint RotateCW(Blueprint s, ref int dropped)
		{
			var d = NewLike(s, s.Height, s.Width);
			int furnitureTiles = 0, furnitureKept = 0;
			for (int y = 0; y < s.Height; y++) {
				for (int x = 0; x < s.Width; x++) {
					ref BlueprintCell c = ref s[x, y];
					ref BlueprintCell t = ref d[s.Height - 1 - y, x];
					CopyWallAndLiquid(c, ref t);
					if (!c.HasTile)
						continue;
					if (IsMultitile(c)) {
						furnitureTiles++;
						continue;
					}
					CopyTile(c, ref t);
					t.Slope = RotateSlopeCW(c.Slope);
					t.HalfBlock = false;
					if (Main.tileSolid[c.TileType])
						t.FrameX = Reframe;
				}
			}

			// Moveis entram depois dos blocos, em pe, no lugar para onde o retangulo deles girou.
			for (int y = 0; y < s.Height; y++) {
				for (int x = 0; x < s.Width; x++) {
					ref BlueprintCell c = ref s[x, y];
					if (!IsMultitile(c) || !MaterialLookup.TryGetObjectCell(c, out var data, out int col, out int row) || col != 0 || row != 0)
						continue;
					int w = data.Width, h = data.Height;
					// Depois de girar, o retangulo do objeto fica com h de largura e w de altura.
					int left = s.Height - y - h, top = x;
					int tx = left + (h - w) / 2, ty = top + (w - h); // centralizado e apoiado na base
					furnitureKept += PlaceObjectCopy(s, d, x, y, w, h, c.TileType, tx, ty);
				}
			}
			dropped += furnitureTiles - furnitureKept;
			return d;
		}

		/// <summary>Copia um objeto inteiro (sem girar o sprite) se couber sem cobrir outros blocos. Devolve quantos tiles copiou.</summary>
		private static int PlaceObjectCopy(Blueprint s, Blueprint d, int sx, int sy, int w, int h, ushort type, int tx, int ty)
		{
			if (tx < 0 || ty < 0 || tx + w > d.Width || ty + h > d.Height)
				return 0;
			for (int r = 0; r < h; r++) {
				for (int c = 0; c < w; c++) {
					if (d[tx + c, ty + r].HasTile)
						return 0;
				}
			}
			int copied = 0;
			for (int r = 0; r < h; r++) {
				for (int c = 0; c < w; c++) {
					int x = sx + c, y = sy + r;
					if (x >= s.Width || y >= s.Height)
						continue;
					ref BlueprintCell src = ref s[x, y];
					if (!src.HasTile || src.TileType != type)
						continue;
					CopyTile(src, ref d[tx + c, ty + r]);
					copied++;
				}
			}
			return copied;
		}

		/// <summary>
		/// Troca o quadro de um tile de objeto pela versao virada para o outro lado (se o objeto tiver uma),
		/// mantendo o estilo e a posicao do tile dentro do objeto.
		/// </summary>
		public static void MirrorFacing(ref BlueprintCell c)
		{
			var baseData = TileObjectData.GetTileData(c.TileType, 0);
			var data = MaterialLookup.GetData(c, out int style, out int alt, out int offset);
			if (baseData == null || data == null || data.Direction == TileObjectDirection.None
				|| baseData.CoordinateFullWidth <= 0 || baseData.CoordinateFullHeight <= 0)
				return;
			var want = data.Direction == TileObjectDirection.PlaceLeft ? TileObjectDirection.PlaceRight : TileObjectDirection.PlaceLeft;

			int newAlt = -1;
			var styleData = TileObjectData.GetTileData(c.TileType, style);
			if (styleData != data && Matches(styleData))
				newAlt = 0;
			for (int k = 1; newAlt < 0 && k <= MaterialLookup.AlternatesCount(styleData); k++) {
				var d = TileObjectData.GetTileData(c.TileType, style, k);
				if (d != null && d != data && Matches(d))
					newAlt = d.Style + System.Math.Min(alt - offset, d.RandomStyleRange);
			}
			if (newAlt < 0)
				return;

			// Caminho inverso de GetStyle: do numero do estilo de volta para a linha/coluna no spritesheet.
			int mult = System.Math.Max(1, baseData.StyleMultiplier);
			int wrap = baseData.StyleWrapLimit == 0 ? 1 : baseData.StyleWrapLimit;
			int skip = System.Math.Max(1, baseData.StyleLineSkip);
			int n = style * mult + newAlt;
			int col = c.FrameX / baseData.CoordinateFullWidth, row = c.FrameY / baseData.CoordinateFullHeight;
			if (baseData.StyleHorizontal) {
				col = n % wrap;
				row = n / wrap * skip + row % skip;
			}
			else {
				row = n % wrap;
				col = n / wrap * skip + col % skip;
			}
			c.FrameX = (short)(col * baseData.CoordinateFullWidth + c.FrameX % baseData.CoordinateFullWidth);
			c.FrameY = (short)(row * baseData.CoordinateFullHeight + c.FrameY % baseData.CoordinateFullHeight);

			bool Matches(TileObjectData d) => d != null && d.Direction == want && d.Width == data.Width && d.Height == data.Height;
		}
	}
}
