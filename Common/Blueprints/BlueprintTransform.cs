using Terraria;
using Terraria.ObjectData;

namespace Terraprint.Common.Blueprints
{
	/// <summary>
	/// Espelhar e girar blueprints.
	/// - Blocos e paredes: giram 90 graus e espelham. Rampas sao convertidas para a orientacao certa.
	/// - Moveis e objetos de varios tiles: so espelham (a posicao do objeto inteiro e espelhada,
	///   o sprite continua virado para o mesmo lado). Ao girar, eles sao deixados de fora.
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
						if (nx >= 0 && nx < s.Width)
							CopyTile(c, ref d[nx, y]);
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
			for (int y = 0; y < s.Height; y++) {
				for (int x = 0; x < s.Width; x++) {
					ref BlueprintCell c = ref s[x, y];
					ref BlueprintCell t = ref d[s.Height - 1 - y, x];
					CopyWallAndLiquid(c, ref t);
					if (!c.HasTile)
						continue;
					if (IsMultitile(c)) {
						dropped++;
						continue;
					}
					CopyTile(c, ref t);
					t.Slope = RotateSlopeCW(c.Slope);
					t.HalfBlock = false;
					if (Main.tileSolid[c.TileType])
						t.FrameX = Reframe;
				}
			}
			return d;
		}
	}
}
