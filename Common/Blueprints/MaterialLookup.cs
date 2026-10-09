using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Terraprint.Common.Blueprints
{
	/// <summary>
	/// Descobre qual item coloca cada bloco, movel ou parede de um blueprint.
	/// O mapa e montado uma vez a partir de todos os itens carregados (vanilla e mods).
	/// </summary>
	public static class MaterialLookup
	{
		private static Dictionary<int, List<(int Style, int Item)>> _tileItems;
		private static Dictionary<int, int> _wallItems;

		private static void EnsureBuilt()
		{
			if (_tileItems != null)
				return;
			_tileItems = new Dictionary<int, List<(int, int)>>();
			_wallItems = new Dictionary<int, int>();
			for (int type = 1; type < ItemLoader.ItemCount; type++) {
				if (!ContentSamples.ItemsByType.TryGetValue(type, out Item item) || item.tileWand >= 0 || !item.consumable)
					continue;
				if (item.createTile >= TileID.Dirt) {
					if (!_tileItems.TryGetValue(item.createTile, out var list))
						_tileItems[item.createTile] = list = new List<(int, int)>();
					// Se dois itens colocam o mesmo estilo, fica o de menor ID (normalmente o vanilla).
					if (!list.Exists(e => e.Item1 == item.placeStyle))
						list.Add((item.placeStyle, type));
				}
				if (item.createWall > WallID.None && !_wallItems.ContainsKey(item.createWall))
					_wallItems[item.createWall] = type;
			}
		}

		public static void Unload()
		{
			_tileItems = null;
			_wallItems = null;
		}

		/// <summary>Item que coloca esta parede, ou 0.</summary>
		public static int ItemForWall(int wallType)
		{
			EnsureBuilt();
			return _wallItems.TryGetValue(wallType, out int item) ? item : 0;
		}

		/// <summary>Item que coloca o bloco/movel desta celula, ou 0.</summary>
		public static int ItemForTile(in BlueprintCell c)
		{
			EnsureBuilt();
			if (!c.HasTile)
				return 0;
			GetStyle(c, out int style, out _);
			if (_tileItems.TryGetValue(c.TileType, out var list)) {
				if (list.Count == 1)
					return list[0].Item;
				foreach (var e in list) {
					if (e.Style == style)
						return e.Item;
				}
			}
			int drop = TileLoader.GetItemDropFromTypeAndStyle(c.TileType, style);
			if (drop > 0)
				return drop;
			// Estilo desconhecido: usa o primeiro item que coloca esse tipo.
			return list != null && list.Count > 0 ? list[0].Item : 0;
		}

		/// <summary>Mesmo calculo de TileObjectData.GetTileStyle, mas a partir dos quadros guardados no blueprint.</summary>
		public static void GetStyle(in BlueprintCell c, out int style, out int alternate)
		{
			style = 0;
			alternate = 0;
			var data = TileObjectData.GetTileData(c.TileType, 0);
			if (data == null || c.FrameX < 0 || c.FrameY < 0 || data.CoordinateFullWidth <= 0 || data.CoordinateFullHeight <= 0)
				return;
			int col = c.FrameX / data.CoordinateFullWidth;
			int row = c.FrameY / data.CoordinateFullHeight;
			int wrap = data.StyleWrapLimit == 0 ? 1 : data.StyleWrapLimit;
			int skip = System.Math.Max(1, data.StyleLineSkip);
			int n = data.StyleHorizontal ? row / skip * wrap + col : col / skip * wrap + row;
			int mult = System.Math.Max(1, data.StyleMultiplier);
			style = n / mult;
			alternate = n % mult;
		}

		/// <summary>
		/// Para objetos com TileObjectData (moveis, tochas, plataformas...): tamanho do objeto e a posicao
		/// deste tile dentro dele. False para blocos comuns.
		/// </summary>
		public static bool TryGetObjectCell(in BlueprintCell c, out TileObjectData data, out int column, out int row)
		{
			column = row = 0;
			data = null;
			if (!c.HasTile || c.FrameX < 0 || c.FrameY < 0)
				return false;
			GetStyle(c, out int style, out int alt);
			data = TileObjectData.GetTileData(c.TileType, style, alt) ?? TileObjectData.GetTileData(c.TileType, 0);
			if (data == null)
				return false;

			int step = data.CoordinateWidth + data.CoordinatePadding;
			if (step > 0 && data.CoordinateFullWidth > 0)
				column = System.Math.Clamp(c.FrameX % data.CoordinateFullWidth / step, 0, data.Width - 1);

			if (data.CoordinateFullHeight > 0 && data.CoordinateHeights != null) {
				int y = c.FrameY % data.CoordinateFullHeight;
				int top = 0;
				for (int i = 0; i < data.CoordinateHeights.Length; i++) {
					top += data.CoordinateHeights[i] + data.CoordinatePadding;
					if (y < top) {
						row = i;
						break;
					}
				}
			}
			return true;
		}

		/// <summary>Um objeto de varios tiles e contado uma vez so, no tile do canto superior esquerdo.</summary>
		public static bool CountsAsItem(in BlueprintCell c)
		{
			if (!c.HasTile)
				return false;
			return !TryGetObjectCell(c, out _, out int col, out int row) || col == 0 && row == 0;
		}

		public static string TileName(int type)
		{
			var mod = TileLoader.GetTile(type);
			return mod != null ? mod.Name : TileID.Search.GetName(type);
		}

		public static string WallName(int type)
		{
			var mod = WallLoader.GetWall(type);
			return mod != null ? mod.Name : WallID.Search.GetName(type);
		}
	}
}
