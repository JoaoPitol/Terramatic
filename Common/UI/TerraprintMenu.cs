using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terraprint.Common.Blueprints;
using Terraprint.Common.Configs;
using Terraprint.Common.Systems;

namespace Terraprint.Common.UI
{
	/// <summary>
	/// Janela do Terraprint (abre com a tecla do mod, padrao K), no visual da tela de crafting do Terraria:
	/// abas em forma de slot no topo, painel azul com borda preta arredondada e slots semitransparentes.
	/// </summary>
	public class TerraprintMenu : UIState
	{
		public enum Tab { Projection, Save, Files, Materials }

		private const float PanelWidth = 424f;
		private const float PanelHeight = 360f;
		private const float Pad = 10f;
		private const float TabSize = 36f;
		private const float TabGap = 4f;
		private const float Step = TerraprintStyle.SlotSize + 4f;
		private const float HeaderHeight = 30f;
		private const float ContentTop = HeaderHeight + 8f;

		private static readonly string[] TabIcons = { "Projection", "Select", "Folder", "Materials" };
		private static readonly string[] TabTitles = { "ProjectionHeader", "SaveHeader", "FilesHeader", "MaterialsTitle" };

		private UIElement _window;
		private UIElement _tabRow;
		private UITerraprintPanel _panel;
		private UIText _title;
		private readonly UIElement[] _contents = new UIElement[4];
		private Tab _tab = Tab.Files;

		private UITextBox _nameBox;
		private UIText _selectionInfo;
		private UIText _projectionInfo;
		private UIText _filesInfo;
		private UIText _materialsInfo;
		private UIIconButton _buildButton;
		private UIList _fileList;

		/// <summary>Linha da lista de arquivos sob o mouse; a previa grande aparece ao lado da janela.</summary>
		internal BlueprintFileRow HoveredRow;

		public override void OnInitialize()
		{
			_window = new UIElement();
			_window.Width.Set(PanelWidth, 0f);
			_window.Height.Set(TabSize + TabGap + PanelHeight, 0f);
			_window.Left.Set(Main.screenWidth / 2f - PanelWidth / 2f, 0f);
			_window.Top.Set(100f, 0f);
			Append(_window);

			// --- Abas no topo da janela ---
			_tabRow = new UIElement();
			_tabRow.Width.Set(0f, 1f);
			_tabRow.Height.Set(TabSize, 0f);
			_window.Append(_tabRow);
			for (int i = 0; i < TabIcons.Length; i++) {
				var tab = (Tab)i;
				var b = new UIIconButton(TabIcons[i], Lang.UI(TabTitles[i]), () => ShowTab(tab)) {
					IsActive = () => _tab == tab,
				};
				b.Width.Set(TabSize, 0f);
				b.Height.Set(TabSize, 0f);
				b.Left.Set(i * (TabSize + TabGap), 0f);
				_tabRow.Append(b);
			}
			var options = new UIIconButton("Options", Lang.UI("Options"), () => TerraprintConfig.Instance.Open());
			options.Width.Set(TabSize, 0f);
			options.Height.Set(TabSize, 0f);
			options.Left.Set(-TabSize, 1f);
			_tabRow.Append(options);

			// --- Painel ---
			_panel = new UITerraprintPanel {
				SeparatorY = HeaderHeight,
				DragTarget = _window,
				DragHeight = HeaderHeight + Pad,
			};
			_panel.Top.Set(TabSize + TabGap, 0f);
			_panel.Width.Set(0f, 1f);
			_panel.Height.Set(PanelHeight, 0f);
			_panel.SetPadding(Pad);
			_window.Append(_panel);

			_title = new UIText(Lang.UI(TabTitles[(int)_tab]), 0.95f);
			_title.Top.Set(4f, 0f);
			_panel.Append(_title);

			var close = new UIIconButton("Close", Lang.UI("Close"), UISystem.CloseMenu) { Bare = true };
			close.Width.Set(22f, 0f);
			close.Height.Set(22f, 0f);
			close.Left.Set(-22f, 1f);
			close.Top.Set(1f, 0f);
			_panel.Append(close);

			_contents[(int)Tab.Projection] = BuildProjectionTab();
			_contents[(int)Tab.Save] = BuildSaveTab();
			_contents[(int)Tab.Files] = BuildFilesTab();
			_contents[(int)Tab.Materials] = BuildMaterialsTab();
			_panel.Append(_contents[(int)_tab]);
		}

		private static UIElement NewContent()
		{
			var c = new UIElement();
			c.Top.Set(ContentTop, 0f);
			c.Width.Set(0f, 1f);
			c.Height.Set(-ContentTop, 1f);
			return c;
		}

		/// <summary>Coloca um elemento na posicao (x, y); com right=true, x conta a partir da borda direita.</summary>
		private static T Put<T>(UIElement parent, T el, float x, float y, bool right = false) where T : UIElement
		{
			el.Left.Set(x, right ? 1f : 0f);
			el.Top.Set(y, 0f);
			parent.Append(el);
			return el;
		}

		private static UIText Label(string text, float scale, Color color)
		{
			var t = new UIText(text, scale) {
				TextColor = color,
				IsWrapped = true,
				TextOriginX = 0f,
			};
			t.Width.Set(0f, 1f);
			return t;
		}

		private static UISeparator Separator(UIElement parent, float y)
		{
			var s = new UISeparator();
			s.Top.Set(y, 0f);
			parent.Append(s);
			return s;
		}

		// ----- Aba Projecao -----

		private UIElement BuildProjectionTab()
		{
			var c = NewContent();

			_projectionInfo = Label("", 0.8f, Color.White);
			_projectionInfo.Height.Set(58f, 0f);
			c.Append(_projectionInfo);

			float y = 64f;
			Put(c, ProjectionButton("Rotate", "Rotate", ProjectionSystem.Rotate), 0f, y);
			Put(c, ProjectionButton("Mirror", "Flip", ProjectionSystem.Flip), Step, y);
			Put(c, ProjectionButton("Move", "Move", ProjectionSystem.StartMoving), 2 * Step, y);
			Put(c, ProjectionButton("Remove", "Remove", ProjectionSystem.Clear), 3 * Step, y);

			_buildButton = new UIIconButton(ItemID.IronHammer, Lang.UI("AutoBuild"), null) {
				WhileHeld = AutoBuildSystem.RequestHold,
				IsEnabled = () => ProjectionSystem.State == ProjectionSystem.ProjectionState.Fixed,
			};
			_buildButton.Tooltip = () => Lang.UI(_buildButton.Held ? "AutoBuilding" : "AutoBuild");
			Put(c, _buildButton, -TerraprintStyle.SlotSize, y, right: true);

			y += Step;
			Put(c, ProjectionButton("Up", "NudgeHint", () => ProjectionSystem.Nudge(-1, 0), 3), 0f, y);
			Put(c, ProjectionButton("Up", "NudgeHint", () => ProjectionSystem.Nudge(0, -1), 0), Step, y);
			Put(c, ProjectionButton("Up", "NudgeHint", () => ProjectionSystem.Nudge(0, 1), 2), 2 * Step, y);
			Put(c, ProjectionButton("Up", "NudgeHint", () => ProjectionSystem.Nudge(1, 0), 1), 3 * Step, y);

			var cfg = () => TerraprintConfig.Instance;
			var ghost = new UIIconButton("Ghost", "", () => TerraprintConfig.Set(x => x.ShowGhost = !x.ShowGhost)) {
				IsActive = () => cfg().ShowGhost,
				Tooltip = () => Lang.UI("ToggleGhost", OnOff(cfg().ShowGhost)),
			};
			var map = new UIIconButton("Map", "", () => TerraprintConfig.Set(x => {
				bool on = !(x.ShowOnMinimap || x.ShowOnFullscreenMap);
				x.ShowOnMinimap = x.ShowOnFullscreenMap = on;
			})) {
				IsActive = () => cfg().ShowOnMinimap || cfg().ShowOnFullscreenMap,
				Tooltip = () => Lang.UI("ToggleMap", OnOff(cfg().ShowOnMinimap || cfg().ShowOnFullscreenMap)),
			};
			var correct = new UIIconButton("Check", "", () => TerraprintConfig.Set(x => x.ShowCorrect = !x.ShowCorrect)) {
				IsActive = () => cfg().ShowCorrect,
				Tooltip = () => Lang.UI("ToggleCorrect", OnOff(cfg().ShowCorrect)),
			};
			Put(c, ghost, -3 * Step + 4f, y, right: true);
			Put(c, map, -2 * Step + 4f, y, right: true);
			Put(c, correct, -TerraprintStyle.SlotSize, y, right: true);
			return c;
		}

		private static UIIconButton ProjectionButton(string icon, string tipKey, Action onClick, int rotation = 0)
		{
			return new UIIconButton(icon, Lang.UI(tipKey), onClick, rotation) {
				IsEnabled = () => ProjectionSystem.HasProjection,
			};
		}

		// ----- Aba Salvar -----

		private UIElement BuildSaveTab()
		{
			var c = NewContent();

			var nameLabel = new UIText(Lang.UI("NameLabel"), 0.85f);
			nameLabel.Top.Set(8f, 0f);
			c.Append(nameLabel);

			_nameBox = new UITextBox(Lang.UI("NameHint"));
			_nameBox.Left.Set(58f, 0f);
			_nameBox.Width.Set(-58f, 1f);
			_nameBox.Height.Set(32f, 0f);
			c.Append(_nameBox);

			float y = 42f;
			var select = new UIIconButton("Select", Lang.UI("SelectArea"), SelectionSystem.Begin) {
				IsActive = () => SelectionSystem.Active,
			};
			Put(c, select, 0f, y);
			var save = new UIIconButton("Save", Lang.UI("Save"), SaveSelection) {
				IsEnabled = () => SelectionSystem.HasSelection,
			};
			Put(c, save, Step, y);

			_selectionInfo = Label("", 0.8f, TerraprintStyle.Muted);
			_selectionInfo.Left.Set(2 * Step + 4f, 0f);
			_selectionInfo.Width.Set(-(2 * Step + 4f), 1f);
			_selectionInfo.Top.Set(y + 2f, 0f);
			c.Append(_selectionInfo);
			return c;
		}

		// ----- Aba Blueprints salvos -----

		private UIElement BuildFilesTab()
		{
			var c = NewContent();

			Put(c, new UIIconButton("Refresh", Lang.UI("Refresh"), RefreshFiles), 0f, 0f);
			Put(c, new UIIconButton("Folder", Lang.UI("OpenFolder"), () => {
				BlueprintIO.EnsureFolder();
				Utils.OpenFolder(BlueprintIO.Folder);
			}), Step, 0f);

			_filesInfo = Label("", 0.8f, TerraprintStyle.Muted);
			_filesInfo.Left.Set(2 * Step + 4f, 0f);
			_filesInfo.Width.Set(-(2 * Step + 4f), 1f);
			_filesInfo.Top.Set(12f, 0f);
			c.Append(_filesInfo);

			Separator(c, Step + 2f);

			float y = Step + 10f;
			_fileList = new UIList();
			_fileList.Top.Set(y, 0f);
			_fileList.Width.Set(-22f, 1f);
			_fileList.Height.Set(-y, 1f);
			_fileList.ListPadding = 4f;
			_fileList.ManualSortMethod = _ => { }; // mantem a ordem da pasta (mais recente primeiro)
			c.Append(_fileList);

			var scroll = new UIScrollbar();
			scroll.Top.Set(y + 4f, 0f);
			scroll.Height.Set(-y - 8f, 1f);
			scroll.Left.Set(-18f, 1f);
			c.Append(scroll);
			_fileList.SetScrollbar(scroll);
			return c;
		}

		// ----- Aba Materiais -----

		private UIElement BuildMaterialsTab()
		{
			var c = NewContent();

			_materialsInfo = Label("", 0.8f, TerraprintStyle.Muted);
			_materialsInfo.Height.Set(40f, 0f);
			c.Append(_materialsInfo);

			Separator(c, 42f);

			float y = 50f;
			var grid = new MaterialsGrid();
			grid.Top.Set(y, 0f);
			grid.Width.Set(-22f, 1f);
			grid.Height.Set(-y, 1f);
			c.Append(grid);

			var scroll = new UIScrollbar();
			scroll.Top.Set(y + 4f, 0f);
			scroll.Height.Set(-y - 8f, 1f);
			scroll.Left.Set(-18f, 1f);
			c.Append(scroll);
			grid.Scrollbar = scroll;
			return c;
		}

		// ----- Estado -----

		public Tab CurrentTab => _tab;

		public void ShowTab(Tab tab)
		{
			if (_panel == null) {
				_tab = tab;
				return;
			}
			if (tab == _tab)
				return;
			_contents[(int)_tab].Remove();
			_tab = tab;
			_panel.Append(_contents[(int)tab]);
			_title.SetText(Lang.UI(TabTitles[(int)tab]));
			_nameBox?.Unfocus();
			_panel.Recalculate();
		}

		public override void OnActivate()
		{
			RefreshFiles();
			if (_tab == Tab.Projection && !ProjectionSystem.HasProjection && !SelectionSystem.Active)
				ShowTab(Tab.Files);
			else if (_tab == Tab.Files && ProjectionSystem.HasProjection)
				ShowTab(Tab.Projection);
		}

		public override void Update(GameTime gameTime)
		{
			HoveredRow = null;
			base.Update(gameTime);

			if (_tabRow.ContainsPoint(Main.MouseScreen))
				Main.LocalPlayer.mouseInterface = true;

			switch (_tab) {
				case Tab.Projection:
					_projectionInfo.SetText(ProjectionInfoText());
					break;
				case Tab.Save:
					if (SelectionSystem.Active)
						_selectionInfo.SetText(SelectionSystem.CornerA.HasValue ? Lang.UI("InfoSecondCorner") : Lang.UI("InfoFirstCorner"));
					else if (SelectionSystem.HasSelection) {
						var r = SelectionSystem.SelectionRect;
						_selectionInfo.SetText(Lang.UI("InfoSelection", r.Width, r.Height));
					}
					else
						_selectionInfo.SetText(Lang.UI("InfoNoSelection"));
					break;
				case Tab.Materials:
					MaterialsTracker.Update();
					_materialsInfo.SetText(MaterialsSummary.Text());
					break;
			}
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			base.Draw(spriteBatch);
			if (_tab == Tab.Files && HoveredRow != null)
				DrawLargePreview(spriteBatch, HoveredRow);
		}

		/// <summary>Previa grande do blueprint sob o mouse, ao lado da janela (do lado que tiver espaco).</summary>
		private void DrawLargePreview(SpriteBatch sb, BlueprintFileRow row)
		{
			var e = row.Preview;
			if (e?.Texture == null)
				return;
			const int boxW = 260, boxH = 200, pad = 10;
			var panel = _panel.GetDimensions().ToRectangle();
			int x = panel.Right + 8;
			if (x + boxW > Main.screenWidth / Main.UIScale)
				x = panel.Left - 8 - boxW;
			var box = new Rectangle(x, panel.Top, boxW, boxH + 26);
			Utils.DrawInvBG(sb, box, TerraprintStyle.PanelBackground);
			BlueprintPreview.Draw(sb, e, new Rectangle(box.X + pad, box.Y + pad, boxW - 2 * pad, boxH - 2 * pad));
			string size = $"{e.Width} x {e.Height}";
			Utils.DrawBorderString(sb, size, new Vector2(box.X + pad, box.Bottom - 28), TerraprintStyle.Muted, 0.8f);
		}

		private static string OnOff(bool on) => on ? Lang.UI("On") : Lang.UI("Off");

		/// <summary>Texto do estado da projecao e, quando fixada, o progresso da construcao.</summary>
		protected virtual string ProjectionInfoText()
		{
			if (!ProjectionSystem.HasProjection)
				return Lang.UI("NoProjection");
			var bp = ProjectionSystem.Current;
			string flip = ProjectionSystem.FlippedX ? Lang.UI("Mirrored") : "";
			string line = Lang.UI("ProjectionLine", bp.Name, bp.Width, bp.Height, ProjectionSystem.Rotation * 90, flip);
			if (ProjectionSystem.State != ProjectionSystem.ProjectionState.Fixed)
				return line + "\n" + Lang.UI("StateMoving");
			if (_buildButton != null && _buildButton.Held)
				return line + "\n" + Lang.UI("AutoBuilding");
			return line + "\n" + GhostSystem.ProgressText();
		}

		public bool IsMouseOverPanel => _panel != null && (_panel.ContainsPoint(Main.MouseScreen) || _tabRow.ContainsPoint(Main.MouseScreen));
		public bool IsTyping => _nameBox != null && _nameBox.Focused;

		private void SaveSelection()
		{
			if (!SelectionSystem.HasSelection)
				return;
			if (!SelectionSystem.SelectionLoaded()) {
				Main.NewText(Lang.UI("SelectNotLoaded"), Color.OrangeRed);
				return;
			}
			try {
				var bp = SelectionSystem.CaptureSelection(_nameBox.Text);
				string path = BlueprintIO.Save(bp);
				Lang.Info(Lang.UI("Saved", Path.GetFileName(path), bp.Width, bp.Height), Color.LightGreen);
				_nameBox.Text = "";
				SelectionSystem.Cancel();
				RefreshFiles();
				ShowTab(Tab.Files);
			}
			catch (Exception e) {
				Main.NewText(Lang.UI("SaveError", e.Message), Color.OrangeRed);
				ModContent.GetInstance<Terraprint>().Logger.Error("Failed to save blueprint", e);
			}
		}

		public void RefreshFiles()
		{
			if (_fileList == null)
				return;
			_fileList.Clear();
			var files = BlueprintIO.ListFiles();
			BlueprintPreview.Retain(new HashSet<string>(files));
			_filesInfo.SetText(files.Count == 0 ? Lang.UI("NoFiles") : Lang.UI("FilesCount", files.Count));
			foreach (string file in files)
				_fileList.Add(new BlueprintFileRow(file));
		}
	}

	/// <summary>Linha clara e fina, como a que separa as partes da tela de crafting.</summary>
	public class UISeparator : UIElement
	{
		public UISeparator()
		{
			Width.Set(0f, 1f);
			Height.Set(2f, 0f);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			var d = GetDimensions();
			TerraprintStyle.DrawSeparator(spriteBatch, d.X, d.Y, d.Width);
		}
	}

	/// <summary>Uma linha da lista de blueprints salvos: previa, nome, tamanho, carregar e excluir.</summary>
	public class BlueprintFileRow : UIElement
	{
		private const float RowHeight = 56f;
		private const float ThumbWidth = 76f;
		private const float ButtonSize = 34f;

		public readonly string FilePath;
		private int _confirmTimer;
		private BlueprintPreview.Entry _preview;
		private bool _previewLoaded;
		private readonly UIText _size;

		public BlueprintPreview.Entry Preview
		{
			get {
				if (!_previewLoaded) {
					_previewLoaded = true;
					_preview = BlueprintPreview.Get(FilePath);
					if (_preview != null && !_preview.Failed)
						_size.SetText($"{_preview.Width} x {_preview.Height}");
				}
				return _preview;
			}
		}

		public BlueprintFileRow(string path)
		{
			FilePath = path;
			Width.Set(0f, 1f);
			Height.Set(RowHeight, 0f);

			string display = Path.GetFileNameWithoutExtension(path);
			if (display.Length > 20)
				display = display.Substring(0, 19) + "…";
			var name = new UIText(display, 0.85f);
			name.Left.Set(ThumbWidth + 14f, 0f);
			name.Top.Set(9f, 0f);
			Append(name);

			_size = new UIText("", 0.75f) { TextColor = TerraprintStyle.Muted };
			_size.Left.Set(ThumbWidth + 14f, 0f);
			_size.Top.Set(31f, 0f);
			Append(_size);

			var load = new UIIconButton("Load", Lang.UI("Load"), LoadFile);
			Size(load);
			load.Left.Set(-ButtonSize - 6f, 1f);
			Append(load);

			var delete = new UIIconButton("Delete", Lang.UI("Delete"), DeleteFile) {
				Tint = () => _confirmTimer > 0 ? new Color(255, 120, 120) : null,
			};
			delete.Tooltip = () => Lang.UI(_confirmTimer > 0 ? "DeleteConfirm" : "Delete");
			Size(delete);
			delete.Left.Set(-2 * ButtonSize - 10f, 1f);
			Append(delete);
		}

		private static void Size(UIElement el)
		{
			el.Width.Set(ButtonSize, 0f);
			el.Height.Set(ButtonSize, 0f);
			el.VAlign = 0.5f;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (_confirmTimer > 0)
				_confirmTimer--;
			if (IsMouseHovering && UISystem.Menu != null)
				UISystem.Menu.HoveredRow = this;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			var r = GetDimensions().ToRectangle();
			// Fundo da linha: o mesmo slot do inventario, esticado e semitransparente.
			Utils.DrawInvBG(spriteBatch, r, new Color(53, 88, 160) * (IsMouseHovering ? 0.75f : 0.5f));

			var thumb = new Rectangle(r.X + 6, r.Y + 6, (int)ThumbWidth, r.Height - 12);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, thumb, new Color(10, 20, 45) * 0.6f);
			var e = Preview;
			if (e != null && !e.Failed) {
				BlueprintPreview.Draw(spriteBatch, e, new Rectangle(thumb.X + 2, thumb.Y + 2, thumb.Width - 4, thumb.Height - 4));
			}
			else if (e != null) {
				Utils.DrawBorderString(spriteBatch, "?", thumb.Center.ToVector2() - new Vector2(4f, 10f), TerraprintStyle.Muted);
			}
		}

		/// <summary>Primeiro clique pede confirmacao; o segundo (em ate 3 s) apaga o arquivo.</summary>
		private void DeleteFile()
		{
			if (_confirmTimer == 0) {
				_confirmTimer = 180;
				return;
			}
			try {
				File.Delete(FilePath);
				Lang.Info(Lang.UI("Deleted", Path.GetFileName(FilePath)));
			}
			catch (Exception e) {
				Main.NewText(Lang.UI("DeleteError", e.Message), Color.OrangeRed);
			}
			UISystem.Menu?.RefreshFiles();
		}

		private void LoadFile()
		{
			try {
				var bp = BlueprintIO.Load(FilePath);
				ProjectionSystem.Load(bp);
				if (TerraprintConfig.Instance.CloseMenuOnLoad)
					UISystem.CloseMenu();
				else
					UISystem.Menu?.ShowTab(TerraprintMenu.Tab.Projection);
			}
			catch (Exception e) {
				Main.NewText(Lang.UI("LoadError", Path.GetFileName(FilePath), e.Message), Color.OrangeRed);
				ModContent.GetInstance<Terraprint>().Logger.Error("Failed to load blueprint " + FilePath, e);
			}
		}
	}
}
