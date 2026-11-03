using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader.Config;
using Terraprint.Common.Configs;

namespace Terraprint.Common.Blueprints
{
	/// <summary>
	/// Listas de bloqueio das opcoes: itens cujos blocos ou paredes o Terraprint ignora na projecao
	/// (nao aparecem no fantasma e nao contam no progresso, nos materiais nem na construcao automatica).
	/// Um bloco e ignorado quando o item que o coloca esta na lista (ex.: Sementes de Grama ignora grama).
	/// </summary>
	public static class Blacklist
	{
		private static readonly HashSet<int> _tileItems = new();
		private static readonly HashSet<int> _wallItems = new();
		private static int _seenConfig = -1;

		/// <summary>Texto que muda quando alguma das listas muda (a projecao e refeita).</summary>
		public static string Signature { get; private set; } = "";

		private static void Refresh()
		{
			if (_seenConfig == TerraprintConfig.ChangeCount)
				return;
			_seenConfig = TerraprintConfig.ChangeCount;
			var cfg = TerraprintConfig.Instance;
			_tileItems.Clear();
			_wallItems.Clear();
			Fill(cfg?.TileBlacklist, _tileItems);
			Fill(cfg?.WallBlacklist, _wallItems);
			var parts = new List<int>(_tileItems);
			parts.Sort();
			var wparts = new List<int>(_wallItems);
			wparts.Sort();
			Signature = string.Join(",", parts) + "|" + string.Join(",", wparts);
		}

		private static void Fill(List<ItemDefinition> list, HashSet<int> into)
		{
			if (list == null)
				return;
			foreach (var def in list) {
				if (def != null && !def.IsUnloaded && def.Type > ItemID.None)
					into.Add(def.Type);
			}
		}

		public static bool Empty
		{
			get {
				Refresh();
				return _tileItems.Count == 0 && _wallItems.Count == 0;
			}
		}

		public static string CurrentSignature()
		{
			Refresh();
			return Signature;
		}

		/// <summary>Marca as celulas ignoradas (tira o bloco ou a parede delas). O blueprint deve ser uma copia.</summary>
		public static void Apply(Blueprint bp)
		{
			Refresh();
			if (_tileItems.Count == 0 && _wallItems.Count == 0)
				return;
			var tileCache = new Dictionary<long, bool>();
			for (int i = 0; i < bp.Cells.Length; i++) {
				ref BlueprintCell c = ref bp.Cells[i];
				if (c.HasTile && _tileItems.Count > 0) {
					long key = ((long)c.TileType << 32) | ((long)(ushort)c.FrameX << 16) | (long)(ushort)c.FrameY;
					if (!tileCache.TryGetValue(key, out bool skip))
						tileCache[key] = skip = _tileItems.Contains(MaterialLookup.ItemForTile(c));
					if (skip) {
						c.HasTile = false;
						c.HasActuator = false;
						c.Inactive = false;
						c.SkipTile = true;
					}
				}
				if (c.WallType != WallID.None && _wallItems.Count > 0 && _wallItems.Contains(MaterialLookup.ItemForWall(c.WallType))) {
					c.WallType = WallID.None;
					c.WallPaint = 0;
					c.SkipWall = true;
				}
			}
		}

		public static bool BlocksTileItem(int item)
		{
			Refresh();
			return _tileItems.Contains(item);
		}
	}
}
