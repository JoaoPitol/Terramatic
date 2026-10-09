using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using Terraprint.Common.Systems;

namespace Terraprint.Common.UI
{
	/// <summary>Janela separada com os materiais da projecao: quanto precisa, quanto falta colocar e quanto voce tem.</summary>
	public class MaterialsPanel : UIState
	{
		private const float PanelWidth = 400f;

		private UIDragPanel _panel;
		private UIText _summary;
		private UIList _list;
		private int _shownListVersion = -1;

		public override void OnInitialize()
		{
			_panel = new UIDragPanel();
			_panel.Width.Set(PanelWidth, 0f);
			_panel.Height.Set(System.Math.Min(520f, Main.screenHeight - 160f), 0f);
			_panel.MinHeight.Set(260f, 0f);
			_panel.Left.Set(System.Math.Min(Main.screenWidth / 2f + 270f, Main.screenWidth - PanelWidth - 10f), 0f);
			_panel.Top.Set(120f, 0f);
			_panel.SetPadding(10f);
			Append(_panel);

			_panel.Append(new UIText(Lang.UI("MaterialsTitle"), 0.5f, large: true));

			var close = new UIButton("X", UISystem.CloseMaterials);
			close.Width.Set(30f, 0f);
			close.Height.Set(30f, 0f);
			close.Left.Set(-30f, 1f);
			_panel.Append(close);

			_summary = new UIText("", 0.8f);
			_summary.Top.Set(38f, 0f);
			_summary.Width.Set(0f, 1f);
			_summary.IsWrapped = true;
			_panel.Append(_summary);

			// Cabecalho das colunas, alinhado com as linhas da lista.
			float top = 82f;
			AddColumn(Lang.UI("ColNeeded"), top, MaterialRow.ColNeeded, Color.LightSkyBlue);
			AddColumn(Lang.UI("ColRemaining"), top, MaterialRow.ColRemaining, Color.LightSkyBlue);
			AddColumn(Lang.UI("ColHave"), top, MaterialRow.ColHave, Color.LightSkyBlue);
			top += 22f;

			var listPanel = new UIPanel();
			listPanel.Top.Set(top, 0f);
			listPanel.Width.Set(0f, 1f);
			listPanel.Height.Set(-top, 1f);
			listPanel.SetPadding(6f);
			listPanel.BackgroundColor = new Color(33, 43, 79) * 0.6f;
			_panel.Append(listPanel);

			_list = new UIList();
			_list.Width.Set(-24f, 1f);
			_list.Height.Set(0f, 1f);
			_list.ListPadding = 3f;
			_list.ManualSortMethod = _ => { }; // mantem a ordem do MaterialsTracker
			listPanel.Append(_list);

			var scroll = new UIScrollbar();
			scroll.Height.Set(0f, 1f);
			scroll.Left.Set(-18f, 1f);
			listPanel.Append(scroll);
			_list.SetScrollbar(scroll);
		}

		// As colunas ficam a direita; o deslocamento extra compensa a barra de rolagem e o padding da lista.
		private void AddColumn(string text, float top, float rightOffset, Color color)
		{
			var t = new UIText(text, 0.75f) {
				TextOriginX = 1f,
				TextColor = color,
			};
			t.Top.Set(top, 0f);
			t.Width.Set(MaterialRow.ColWidth, 0f);
			t.Left.Set(rightOffset - MaterialRow.ColWidth - 36f, 1f);
			_panel.Append(t);
		}

		public bool IsMouseOverPanel => _panel != null && _panel.ContainsPoint(Main.MouseScreen);

		public override void Update(GameTime gameTime)
		{
			MaterialsTracker.Update();
			if (_shownListVersion != MaterialsTracker.ListVersion) {
				_shownListVersion = MaterialsTracker.ListVersion;
				_list.Clear();
				foreach (var e in MaterialsTracker.Entries)
					_list.Add(new MaterialRow(e));
			}

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

	/// <summary>Uma linha da lista de materiais: icone, nome e as tres quantidades.</summary>
	public class MaterialRow : UIPanel
	{
		public const float ColWidth = 54f;
		public const float ColNeeded = -2f * ColWidth;
		public const float ColRemaining = -ColWidth;
		public const float ColHave = 0f;

		private static readonly Color Lacking = new(255, 110, 110);
		private static readonly Color Enough = new(255, 240, 140);
		private static readonly Color Done = new(120, 230, 130);

		private readonly MaterialEntry _entry;
		private readonly UIText _name, _needed, _remaining, _have;
		private readonly Item _item;

		public MaterialRow(MaterialEntry entry)
		{
			_entry = entry;
			Width.Set(0f, 1f);
			Height.Set(38f, 0f);
			SetPadding(4f);

			float nameLeft = 4f;
			if (entry.ItemType > 0) {
				_item = new Item(entry.ItemType);
				var icon = new UIItemIcon(_item, false);
				icon.VAlign = 0.5f;
				Append(icon);
				nameLeft = 38f;
			}

			string name = entry.ItemType > 0 ? entry.Name : Lang.UI("NoItem", entry.Name);
			if (name.Length > 22)
				name = name.Substring(0, 21) + "…";
			_name = new UIText(name, 0.8f);
			_name.Left.Set(nameLeft, 0f);
			_name.VAlign = 0.5f;
			Append(_name);

			_needed = Column(ColNeeded);
			_remaining = Column(ColRemaining);
			_have = Column(ColHave);
		}

		private UIText Column(float rightOffset)
		{
			var t = new UIText("", 0.8f) { TextOriginX = 1f };
			t.Width.Set(ColWidth, 0f);
			t.Left.Set(rightOffset - ColWidth, 1f);
			t.VAlign = 0.5f;
			Append(t);
			return t;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			_needed.SetText(_entry.Needed.ToString());
			_remaining.SetText(_entry.Remaining.ToString());
			_have.SetText(_entry.ItemType > 0 ? _entry.Have.ToString() : "-");

			Color c = _entry.Done ? Done : _entry.Lacking ? Lacking : Enough;
			_name.TextColor = _remaining.TextColor = _have.TextColor = c;
			BackgroundColor = _entry.Lacking ? new Color(90, 40, 50) * 0.75f : new Color(63, 82, 151) * 0.7f;

			if (IsMouseHovering && _item != null) {
				Main.HoverItem = _item.Clone();
				Main.hoverItemName = _item.Name;
			}
		}
	}
}
