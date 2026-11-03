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
	public static class MaterialsSummary
	{
		/// <summary>Texto de resumo da lista de materiais da projecao ativa.</summary>
		public static string Text()
		{
			if (!ProjectionSystem.HasProjection)
				return Lang.UI("MaterialsNone");
			if (MaterialsTracker.TotalRemaining == 0)
				return Lang.UI("MaterialsDone");
			if (MaterialsTracker.TotalLacking == 0)
				return Lang.UI("MaterialsEnough", MaterialsTracker.TotalRemaining);
			return Lang.UI("MaterialsLacking", MaterialsTracker.TotalRemaining, MaterialsTracker.TotalLacking);
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
