using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using Terraprint.Common.Systems;

namespace Terraprint.Common.UI
{
	/// <summary>
	/// Janela separada com os materiais da projecao, em slots como o inventario do jogo.
	/// O numero do slot e quanto ainda falta colocar; a dica mostra total, falta e quanto voce tem.
	/// </summary>
	public class MaterialsPanel : UIState
	{
		private const int Columns = 8;
		private const float Pad = 10f;
		private const float GridTop = 62f;
		private static readonly float PanelWidth = Columns * MaterialsGrid.Step - 4f + 2 * Pad + 22f;

		private UITerraprintPanel _panel;
		private UIText _summary;

		public override void OnInitialize()
		{
			_panel = new UITerraprintPanel {
				SeparatorY = GridTop - 8f,
			};
			_panel.Width.Set(PanelWidth, 0f);
			_panel.Height.Set(Math.Min(GridTop + 2 * Pad + 6 * MaterialsGrid.Step, Main.screenHeight - 160f), 0f);
			_panel.Left.Set(Math.Min(Main.screenWidth / 2f + 196f, Main.screenWidth - PanelWidth - 10f), 0f);
			_panel.Top.Set(120f, 0f);
			_panel.SetPadding(Pad);
			Append(_panel);

			var title = new UIText(Lang.UI("MaterialsTitle"), 0.9f);
			title.Top.Set(2f, 0f);
			_panel.Append(title);

			var close = new UIIconButton("Close", Lang.UI("Close"), UISystem.CloseMaterials) { Bare = true };
			close.Width.Set(26f, 0f);
			close.Height.Set(26f, 0f);
			close.Left.Set(-26f, 1f);
			close.Top.Set(-2f, 0f);
			_panel.Append(close);

			_summary = new UIText("", 0.75f) {
				TextColor = TerraprintStyle.Muted,
				IsWrapped = true,
				TextOriginX = 0f,
			};
			_summary.Top.Set(28f, 0f);
			_summary.Width.Set(0f, 1f);
			_panel.Append(_summary);

			var grid = new MaterialsGrid();
			grid.Top.Set(GridTop, 0f);
			grid.Width.Set(-22f, 1f);
			grid.Height.Set(-GridTop, 1f);
			_panel.Append(grid);

			var scroll = new UIScrollbar();
			scroll.Top.Set(GridTop + 4f, 0f);
			scroll.Height.Set(-GridTop - 8f, 1f);
			scroll.Left.Set(-18f, 1f);
			_panel.Append(scroll);
			grid.Scrollbar = scroll;
		}

		public bool IsMouseOverPanel => _panel != null && _panel.ContainsPoint(Main.MouseScreen);

		public override void Update(GameTime gameTime)
		{
			MaterialsTracker.Update();

			if (!ProjectionSystem.HasProjection)
				_summary.SetText(Lang.UI("MaterialsNone"));
			else if (MaterialsTracker.TotalRemaining == 0)
				_summary.SetText(Lang.UI("MaterialsDone"));
			else if (MaterialsTracker.TotalLacking == 0)
				_summary.SetText(Lang.UI("MaterialsEnough", MaterialsTracker.TotalRemaining));
			else
				_summary.SetText(Lang.UI("MaterialsLacking", MaterialsTracker.TotalRemaining, MaterialsTracker.TotalLacking));

			base.Update(gameTime);
		}
	}

	/// <summary>Grade de slots dos materiais, rolando de linha em linha.</summary>
	public class MaterialsGrid : UIElement
	{
		public const float Step = TerraprintStyle.SlotSize + 4f;

		private static readonly Color LackingTint = new(255, 120, 120);
		private static readonly Color DoneTint = new(130, 200, 140);

		public UIScrollbar Scrollbar;
		private readonly Item _item = new();
		private int _hovered = -1;

		private int ColumnCount => Math.Max(1, (int)((GetDimensions().Width + 4f) / Step));
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
			int cols = ColumnCount;
			int row = index / cols - first;
			int col = index % cols;
			return new Rectangle((int)(d.X + col * Step), (int)(d.Y + row * Step), (int)TerraprintStyle.SlotSize, (int)TerraprintStyle.SlotSize);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			var entries = MaterialsTracker.Entries;
			int cols = ColumnCount;
			int rows = (entries.Count + cols - 1) / cols;
			Scrollbar?.SetView(VisibleRows, Math.Max(rows, VisibleRows));

			_hovered = -1;
			if (!IsMouseHovering)
				return;
			int first = FirstRow;
			int last = Math.Min(entries.Count, (first + VisibleRows) * cols);
			for (int i = first * cols; i < last; i++) {
				if (!SlotRect(i, first).Contains(Main.MouseScreen.ToPoint()))
					continue;
				_hovered = i;
				Main.LocalPlayer.mouseInterface = true;
				break;
			}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			var entries = MaterialsTracker.Entries;
			int cols = ColumnCount;
			int first = FirstRow;
			int last = Math.Min(entries.Count, (first + VisibleRows) * cols);
			for (int i = first * cols; i < last; i++) {
				var e = entries[i];
				var r = SlotRect(i, first);
				Color? tint = e.Done ? DoneTint : e.Lacking ? LackingTint : null;
				TerraprintStyle.DrawSlot(sb, r, false, i == _hovered, true, tint);
				if (i == _hovered) {
					string counts = $"{Lang.UI("ColNeeded")} {e.Needed} · {Lang.UI("ColRemaining")} {e.Remaining} · {Lang.UI("ColHave")} {(e.ItemType > 0 ? e.Have : 0)}";
					string name = e.ItemType > 0 ? e.Name : Lang.UI("NoItem", e.Name);
					Main.hoverItemName = name + "\n" + counts;
				}

				Vector2 center = r.Center.ToVector2();
				if (e.ItemType > 0) {
					if (_item.type != e.ItemType)
						_item.SetDefaults(e.ItemType);
					ItemSlot.DrawItemIcon(_item, ItemSlot.Context.InventoryItem, sb, center, _item.scale, 30f, e.Done ? Color.White * 0.5f : Color.White);
				}
				else {
					var font = FontAssets.MouseText.Value;
					Utils.DrawBorderString(sb, "?", center - font.MeasureString("?") / 2f + new Vector2(0f, 4f), TerraprintStyle.Muted);
				}

				if (!e.Done)
					TerraprintStyle.DrawStackNumber(sb, e.Remaining.ToString(), r, e.Lacking ? LackingTint : Color.White);
				else
					sb.Draw(TerraprintStyle.Icon("Check"), new Vector2(r.Right - 16f, r.Bottom - 16f), null, DoneTint, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
			}
		}
	}
}
