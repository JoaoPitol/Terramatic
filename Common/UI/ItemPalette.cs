using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.UI;

namespace Terraprint.Common.UI
{
	public enum PaletteCategory { Blocks, Walls, Furniture, Decoration, Entities }

	/// <summary>Uma coisa que da para colocar: o item e o bloco/estilo ou a parede que ele coloca.</summary>
	public class PaletteEntry
	{
		public int ItemType;
		public PaletteCategory Category;
		public int TileType = -1;
		public int Style;
		public int WallType;
		public string Name;
		public string SearchName;

		public bool IsWall => WallType > 0;
	}

	/// <summary>Todos os blocos, paredes, moveis, decoracoes e entidades que algum item coloca (vanilla e mods).</summary>
	public static class PaletteData
	{
		private static List<PaletteEntry> _all;

		public static List<PaletteEntry> All
		{
			get {
				if (_all == null)
					Build();
				return _all;
			}
		}

		public static void Unload() => _all = null;

		private static void Build()
		{
			_all = new List<PaletteEntry>();
			var seenTiles = new HashSet<long>();
			var seenWalls = new HashSet<int>();
			for (int type = 1; type < ItemLoader.ItemCount; type++) {
				if (!ContentSamples.ItemsByType.TryGetValue(type, out Item item) || item.tileWand >= 0 || !item.consumable)
					continue;
				if (item.createTile >= TileID.Dirt && item.createTile < TileLoader.TileCount) {
					long key = ((long)item.createTile << 16) | (uint)(item.placeStyle & 0xFFFF);
					if (!seenTiles.Add(key))
						continue;
					_all.Add(new PaletteEntry {
						ItemType = type,
						TileType = item.createTile,
						Style = item.placeStyle,
						Category = Classify(item.createTile, item.placeStyle),
						Name = item.Name,
						SearchName = item.Name.ToLowerInvariant(),
					});
				}
				else if (item.createWall > WallID.None && item.createWall < WallLoader.WallCount) {
					if (!seenWalls.Add(item.createWall))
						continue;
					_all.Add(new PaletteEntry {
						ItemType = type,
						WallType = item.createWall,
						Category = PaletteCategory.Walls,
						Name = item.Name,
						SearchName = item.Name.ToLowerInvariant(),
					});
				}
			}
		}

		private static PaletteCategory Classify(int tile, int style)
		{
			TileObjectData data = null;
			try {
				data = TileObjectData.GetTileData(tile, style);
			}
			catch {
				// alguns mods registram dados estranhos; trata como bloco comum
			}
			// Entidades: baus, gavetas e objetos com dados proprios (molduras, manequins, sensores, pilones...).
			if (Main.tileContainer[tile] || data != null && data.HookPostPlaceMyPlayer.hook != null)
				return PaletteCategory.Entities;
			if (Main.tileSolid[tile] || TileID.Sets.Platforms[tile])
				return PaletteCategory.Blocks;
			if (data != null && (data.Width > 1 || data.Height > 1))
				return PaletteCategory.Furniture;
			return PaletteCategory.Decoration;
		}

		public static PaletteEntry FindByItem(int itemType)
		{
			foreach (var e in All) {
				if (e.ItemType == itemType)
					return e;
			}
			return null;
		}

		public static PaletteEntry FindTile(int tileType, int style)
		{
			PaletteEntry any = null;
			foreach (var e in All) {
				if (e.TileType != tileType)
					continue;
				if (e.Style == style)
					return e;
				any ??= e;
			}
			return any;
		}

		public static PaletteEntry FindWall(int wallType)
		{
			int placeable = Blueprints.MaterialLookup.PlaceableWall(wallType);
			foreach (var e in All) {
				if (e.WallType == wallType || e.WallType == placeable)
					return e;
			}
			return null;
		}
	}

	/// <summary>
	/// Grade de icones da paleta, com filtro por categoria e busca por nome. Rola de linha em linha.
	/// </summary>
	public class ItemPalette : UIElement
	{
		public const float Slot = 36f;
		public const float Step = Slot + 4f;

		public UIScrollbar Scrollbar;
		public Func<PaletteEntry, bool> Filter = _ => true;
		public Action<PaletteEntry> OnPick;
		public Func<PaletteEntry> Selected = () => null;

		private readonly Item _item = new();
		private List<PaletteEntry> _visible = new();
		private string _filterKey;
		private int _hovered = -1;

		/// <summary>Chamar quando a categoria ou a busca mudam.</summary>
		public void Refilter(string key)
		{
			if (key == _filterKey)
				return;
			_filterKey = key;
			_visible = PaletteData.All.FindAll(e => Filter(e));
			if (Scrollbar != null)
				Scrollbar.ViewPosition = 0f;
		}

		public int Count => _visible.Count;

		private int Columns => Math.Max(1, (int)((GetDimensions().Width + 4f) / Step));
		private int VisibleRows => Math.Max(1, (int)((GetDimensions().Height + 4f) / Step));
		private int FirstRow => Scrollbar == null ? 0 : (int)Math.Round(Scrollbar.ViewPosition);

		public override void ScrollWheel(UIScrollWheelEvent evt)
		{
			base.ScrollWheel(evt);
			if (Scrollbar != null)
				Scrollbar.ViewPosition -= evt.ScrollWheelValue / 120f;
		}

		private Rectangle SlotRect(int index, int first)
		{
			var d = GetDimensions();
			int cols = Columns;
			return new Rectangle((int)(d.X + index % cols * Step), (int)(d.Y + (index / cols - first) * Step), (int)Slot, (int)Slot);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			int cols = Columns;
			int rows = (_visible.Count + cols - 1) / cols;
			Scrollbar?.SetView(VisibleRows, Math.Max(rows, VisibleRows));
			_hovered = -1;
			if (!IsMouseHovering)
				return;
			Main.LocalPlayer.mouseInterface = true;
			Terraria.GameInput.PlayerInput.LockVanillaMouseScroll("Terraprint/Palette");
			int first = FirstRow;
			int last = Math.Min(_visible.Count, (first + VisibleRows) * cols);
			for (int i = first * cols; i < last; i++) {
				if (SlotRect(i, first).Contains(Main.MouseScreen.ToPoint())) {
					_hovered = i;
					break;
				}
			}
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			if (_hovered >= 0 && _hovered < _visible.Count) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				OnPick?.Invoke(_visible[_hovered]);
			}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			int cols = Columns;
			int first = FirstRow;
			int last = Math.Min(_visible.Count, (first + VisibleRows) * cols);
			var selected = Selected();
			for (int i = first * cols; i < last; i++) {
				var e = _visible[i];
				var r = SlotRect(i, first);
				bool active = e == selected;
				bool hover = i == _hovered;
				TerraprintStyle.DrawSlot(sb, r, active, hover, true);
				var item = ContentSamples.ItemsByType.TryGetValue(e.ItemType, out var sample) ? sample : _item;
				ItemSlot.DrawItemIcon(item, ItemSlot.Context.InventoryItem, sb, r.Center.ToVector2(), item.scale, 26f, Color.White * TerraprintStyle.IconAlpha(active, hover, true));
				if (hover)
					Main.hoverItemName = e.Name;
			}
			if (_visible.Count == 0) {
				var d = GetDimensions();
				TerraprintStyle.Text(sb, Lang.UI("PaletteEmpty"), new Vector2(d.X + 4, d.Y + 4), TerraprintStyle.Muted, 0.8f, d.Width - 8);
			}
		}
	}

	/// <summary>
	/// Painel com busca, abas de categoria e a grade de icones. Usado pelo editor e pelo seletor das listas de bloqueio.
	/// </summary>
	public class UIPaletteBrowser : UIElement
	{
		private static readonly string[] CategoryKeys = { "CatBlocks", "CatWalls", "CatFurniture", "CatDecoration", "CatEntities" };

		public readonly ItemPalette Palette;
		private readonly UITextBox _search;
		private readonly PaletteCategory[] _categories;
		private PaletteCategory _category;

		public PaletteCategory Category => _category;

		public UIPaletteBrowser(PaletteCategory[] categories, Action<PaletteEntry> onPick, Func<PaletteEntry> selected)
		{
			_categories = categories;
			_category = categories[0];

			_search = new UITextBox(Lang.UI("SearchHint"));
			_search.Width.Set(0f, 1f);
			_search.Height.Set(30f, 0f);
			Append(_search);

			// Abas de categoria em ate duas linhas.
			int perRow = categories.Length <= 3 ? categories.Length : (categories.Length + 1) / 2;
			int rows = (categories.Length + perRow - 1) / perRow;
			const float tabH = 26f, gap = 4f;
			for (int i = 0; i < categories.Length; i++) {
				var cat = categories[i];
				var b = new UITextButton(Lang.UI(CategoryKeys[(int)cat]), () => SetCategory(cat)) {
					IsActive = () => _category == cat,
					TextScale = 0.75f,
				};
				int row = i / perRow, col = i % perRow;
				b.Left.Set(0f, col / (float)perRow);
				b.Width.Set(-gap, 1f / perRow);
				b.Top.Set(36f + row * (tabH + gap), 0f);
				b.Height.Set(tabH, 0f);
				Append(b);
			}
			float top = 36f + rows * (tabH + gap) + 4f;

			Palette = new ItemPalette {
				OnPick = onPick,
				Selected = selected,
				Filter = e => e.Category == _category && (string.IsNullOrEmpty(_search.Text) || e.SearchName.Contains(_search.Text.ToLowerInvariant())),
			};
			Palette.Top.Set(top, 0f);
			Palette.Width.Set(-22f, 1f);
			Palette.Height.Set(-top, 1f);
			Append(Palette);

			var scroll = new UIScrollbar();
			scroll.Top.Set(top + 4f, 0f);
			scroll.Height.Set(-top - 8f, 1f);
			scroll.Left.Set(-18f, 1f);
			Append(scroll);
			Palette.Scrollbar = scroll;
		}

		public void SetCategory(PaletteCategory cat)
		{
			if (Array.IndexOf(_categories, cat) >= 0)
				_category = cat;
		}

		public bool IsTyping => _search.Focused;

		public void Unfocus() => _search.Unfocus();

		public override void Update(GameTime gameTime)
		{
			Palette.Refilter((int)_category + "|" + _search.Text);
			base.Update(gameTime);
		}
	}
}
