using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraprint.Common.Blueprints;
using Terraprint.Common.Configs;

namespace Terraprint.Common.Systems
{
	public enum CellStatus : byte
	{
		None,     // o blueprint nao pede nada aqui
		Missing,  // falta colocar
		Wrong,    // tem algo diferente do blueprint (ou algo que deveria ser removido)
		Correct,  // igual ao blueprint
	}

	/// <summary>
	/// Compara o blueprint projetado com o mundo e guarda o estado de cada celula
	/// (bloco e parede separados). Recalcula a cada poucos ticks ou quando a projecao muda.
	/// </summary>
	[Autoload(Side = ModSide.Client)]
	public class ProgressTracker : ModSystem
	{
		private const int RefreshTicks = 15;

		public static CellStatus[] TileStatus { get; private set; } = System.Array.Empty<CellStatus>();
		public static CellStatus[] WallStatus { get; private set; } = System.Array.Empty<CellStatus>();

		public static int Total { get; private set; }
		public static int Correct { get; private set; }
		public static int Missing { get; private set; }
		public static int Wrong { get; private set; }

		/// <summary>Incrementa a cada recalculo (o mapa usa para saber quando refazer a textura).</summary>
		public static int Revision { get; private set; }

		private static int _timer;
		private static int _seenVersion = -1;
		private static int _seenConfig = -1;

		public static float Percent => Total == 0 ? 1f : Correct / (float)Total;

		public override void PostUpdateEverything()
		{
			// Enquanto segue o mouse nao ha o que comparar (tudo aparece como "falta").
			if (ProjectionSystem.State == ProjectionSystem.ProjectionState.Moving)
				return;
			if (!ProjectionSystem.HasProjection) {
				if (Total != 0 || TileStatus.Length != 0)
					Reset();
				return;
			}
			if (++_timer >= RefreshTicks || _seenVersion != ProjectionSystem.Version || _seenConfig != TerraprintConfig.ChangeCount) {
				_timer = 0;
				_seenVersion = ProjectionSystem.Version;
				_seenConfig = TerraprintConfig.ChangeCount;
				Recalculate();
			}
		}

		public override void ClearWorld() => Reset();

		/// <summary>Recalcula em poucos ticks (usado depois da construcao automatica colocar blocos).</summary>
		public static void RequestRefresh() => _timer = System.Math.Max(_timer, RefreshTicks - 4);

		private static void Reset()
		{
			TileStatus = System.Array.Empty<CellStatus>();
			WallStatus = System.Array.Empty<CellStatus>();
			Total = Correct = Missing = Wrong = 0;
			_seenVersion = -1;
			Revision++;
		}

		public static void Recalculate()
		{
			var bp = ProjectionSystem.Current;
			var o = ProjectionSystem.Origin;
			var cfg = TerraprintConfig.Instance;
			int n = bp.Width * bp.Height;
			if (TileStatus.Length != n) {
				TileStatus = new CellStatus[n];
				WallStatus = new CellStatus[n];
			}

			int total = 0, correct = 0, missing = 0, wrong = 0;
			for (int y = 0; y < bp.Height; y++) {
				for (int x = 0; x < bp.Width; x++) {
					int i = y * bp.Width + x;
					int wx = o.X + x, wy = o.Y + y;
					ref BlueprintCell c = ref bp.Cells[i];

					if (!WorldGen.InWorld(wx, wy, 1)) {
						TileStatus[i] = c.HasTile ? CellStatus.Wrong : CellStatus.None;
						WallStatus[i] = c.WallType != WallID.None ? CellStatus.Wrong : CellStatus.None;
						continue;
					}

					Tile t = Main.tile[wx, wy];
					TileStatus[i] = CompareTile(c, t, cfg);
					WallStatus[i] = CompareWall(c, t, cfg);

					Count(TileStatus[i], c.HasTile);
					Count(WallStatus[i], c.WallType != WallID.None);
				}
			}

			Total = total;
			Correct = correct;
			Missing = missing;
			Wrong = wrong;
			Revision++;

			void Count(CellStatus s, bool required)
			{
				if (required)
					total++;
				switch (s) {
					case CellStatus.Correct: correct++; break;
					case CellStatus.Missing: missing++; break;
					case CellStatus.Wrong: wrong++; break;
				}
			}
		}

		private static CellStatus CompareTile(in BlueprintCell c, Tile t, TerraprintConfig cfg)
		{
			if (!c.HasTile) {
				// Bloco solido onde o blueprint pede vazio: precisa ser removido.
				if (cfg.MarkObstructions && t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
					return CellStatus.Wrong;
				return CellStatus.None;
			}
			if (!t.HasTile)
				return CellStatus.Missing;
			if (t.TileType != c.TileType)
				return CellStatus.Wrong;
			if (cfg.CheckSlopes && Main.tileSolid[c.TileType] && !BlueprintTransform.IsMultitile(c)
				&& ((byte)t.Slope != c.Slope || t.IsHalfBlock != c.HalfBlock))
				return CellStatus.Wrong;
			if (cfg.CheckPaint && t.TileColor != c.TilePaint)
				return CellStatus.Wrong;
			return CellStatus.Correct;
		}

		private static CellStatus CompareWall(in BlueprintCell c, Tile t, TerraprintConfig cfg)
		{
			if (c.WallType == WallID.None)
				return CellStatus.None;
			if (t.WallType == WallID.None)
				return CellStatus.Missing;
			if (!MaterialLookup.SameWall(t.WallType, c.WallType))
				return CellStatus.Wrong;
			if (cfg.CheckPaint && t.WallColor != c.WallPaint)
				return CellStatus.Wrong;
			return CellStatus.Correct;
		}
	}
}
