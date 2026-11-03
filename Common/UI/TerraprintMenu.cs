using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using Terraprint.Common.Blueprints;
using Terraprint.Common.Configs;
using Terraprint.Common.Systems;

namespace Terraprint.Common.UI
{
	/// <summary>
	/// Janela do Terraprint (abre com a tecla do mod, padrao K), no formato de catalogo:
	/// titulo e abas em botoes de texto no topo, lista a esquerda, previa grande a direita e acoes embaixo.
	/// </summary>
	public class TerraprintMenu : UIState
	{
		public enum Tab { Catalog, Save, Projection, Materials }

		private const float PanelWidth = 680f;
		private const float PanelHeight = 420f;
		private const float Pad = 12f;
		private const float HeaderHeight = 34f;
		private const float ContentTop = HeaderHeight + 10f;
		private const float ButtonHeight = 32f;
		private const float Gap = 6f;
		private const float LeftColumn = 200f;
		private const float RightColumn = LeftColumn + 12f;

		private static readonly string[] TabLabels = { "TabCatalog", "Save", "ProjectionHeader", "Materials" };

		private UITerraprintPanel _panel;
		private readonly UIElement[] _contents = new UIElement[4];
		private Tab _tab = Tab.Catalog;

		// Catalogo
		private UIList _fileList;
		private UIText _fileName, _fileInfo, _fileHint;
		private string _selected;
		private int _confirmDelete;

		// Salvar
		private UITextBox _nameBox;
		private UIText _selectionInfo;

		// Projecao
		private UIText _projectionInfo, _projectionHint;
		private UITextButton _buildButton;

		// Materiais
		private UIText _materialsInfo;

		public override void OnInitialize()
		{
			float width = Math.Min(PanelWidth, Main.screenWidth / Main.UIScale - 20f);
			_panel = new UITerraprintPanel {
				DragHeight = HeaderHeight + Pad,
			};
			_panel.Width.Set(width, 0f);
			_panel.Height.Set(PanelHeight, 0f);
			_panel.Left.Set(Main.screenWidth / Main.UIScale / 2f - width / 2f, 0f);
			_panel.Top.Set(110f, 0f);
			_panel.SetPadding(Pad);
			Append(_panel);

			var title = new UIText(Lang.UI("Title"), 1.15f);
			title.Top.Set(4f, 0f);
			_panel.Append(title);

			// Abas e fechar, alinhados a direita.
			var close = new UITextButton("X", UISystem.CloseMenu) { Tooltip = () => Lang.UI("Close") };
			Place(_panel, close, -ButtonHeight, 0f, ButtonHeight, right: true);
			float x = -ButtonHeight - Gap;
			var options = new UITextButton(Lang.UI("Options"), () => TerraprintConfig.Instance.Open());
			x -= 92f;
			Place(_panel, options, x, 0f, 92f, right: true);
			for (int i = TabLabels.Length - 1; i >= 0; i--) {
				var tab = (Tab)i;
				var b = new UITextButton(Lang.UI(TabLabels[i]), () => ShowTab(tab)) {
					IsActive = () => _tab == tab,
				};
				x -= 92f + Gap;
				Place(_panel, b, x, 0f, 92f, right: true);
			}

			_contents[(int)Tab.Catalog] = BuildCatalogTab();
			_contents[(int)Tab.Save] = BuildSaveTab();
			_contents[(int)Tab.Projection] = BuildProjectionTab();
			_contents[(int)Tab.Materials] = BuildMaterialsTab();
			_panel.Append(_contents[(int)_tab]);
		}

		// ----- Ajudantes de layout -----

		private static UIElement NewContent()
		{
			var c = new UIElement();
			c.Top.Set(ContentTop, 0f);
			c.Width.Set(0f, 1f);
			c.Height.Set(-ContentTop, 1f);
			return c;
		}

		/// <summary>Posiciona um elemento com largura fixa; right=true conta x a partir da borda direita.</summary>
		private static T Place<T>(UIElement parent, T el, float x, float y, float width, bool right = false, float height = ButtonHeight) where T : UIElement
		{
			el.Left.Set(x, right ? 1f : 0f);
			el.Top.Set(y, 0f);
			el.Width.Set(width, 0f);
			el.Height.Set(height, 0f);
			parent.Append(el);
			return el;
		}

		/// <summary>Botao na faixa de baixo do conteudo.</summary>
		private static T Bottom<T>(UIElement parent, T el, float x, float width) where T : UIElement
		{
			el.Left.Set(x, 0f);
			el.Top.Set(-ButtonHeight, 1f);
			el.Width.Set(width, 0f);
			el.Height.Set(ButtonHeight, 0f);
			parent.Append(el);
			return el;
		}

		private static UIText Label(UIElement parent, float x, float y, float scale, Color color, bool fill = true)
		{
			var t = new UIText("", scale) {
				TextColor = color,
				IsWrapped = fill,
				TextOriginX = 0f,
			};
			t.Left.Set(x, 0f);
			t.Top.Set(y, 0f);
			if (fill)
				t.Width.Set(-x, 1f);
			parent.Append(t);
			return t;
		}

		private static UIPanel Box(UIElement parent, float x, float y, float heightFromBottom)
		{
			var box = new UIPanel {
				BackgroundColor = TerraprintStyle.PreviewBackground,
				BorderColor = Color.Black * 0.6f,
			};
			box.Left.Set(x, 0f);
			box.Top.Set(y, 0f);
			box.Width.Set(-x, 1f);
			box.Height.Set(-y - heightFromBottom, 1f);
			box.SetPadding(6f);
			parent.Append(box);
			return box;
		}

		// ----- Aba Blueprints (catalogo) -----

		private UIElement BuildCatalogTab()
		{
			var c = NewContent();

			_fileList = new UIList();
			_fileList.Width.Set(LeftColumn - 22f, 0f);
			_fileList.Height.Set(-ButtonHeight - Gap, 1f);
			_fileList.ListPadding = 4f;
			_fileList.ManualSortMethod = _ => { }; // mantem a ordem da pasta (mais recente primeiro)
			c.Append(_fileList);

			var scroll = new UIScrollbar();
			scroll.Left.Set(LeftColumn - 18f, 0f);
			scroll.Top.Set(4f, 0f);
			scroll.Height.Set(-ButtonHeight - Gap - 8f, 1f);
			c.Append(scroll);
			_fileList.SetScrollbar(scroll);

			float half = (LeftColumn - Gap) / 2f;
			Bottom(c, new UITextButton(Lang.UI("Refresh"), RefreshFiles), 0f, half);
			Bottom(c, new UITextButton(Lang.UI("OpenFolder"), () => {
				BlueprintIO.EnsureFolder();
				Utils.OpenFolder(BlueprintIO.Folder);
			}), half + Gap, half);

			_fileName = Label(c, RightColumn, 0f, 1f, Color.White, fill: false);
			_fileInfo = Label(c, RightColumn, 24f, 0.8f, TerraprintStyle.Muted, fill: false);
			var box = Box(c, RightColumn, 48f, ButtonHeight + Gap + 26f);
			box.Append(new UIPreviewImage(() => SelectedPreview));
			_fileHint = Label(c, RightColumn, 0f, 0.8f, TerraprintStyle.Highlight);
			_fileHint.Top.Set(-ButtonHeight - Gap - 22f, 1f);

			float w = (PanelWidth - 2 * Pad - RightColumn - 2 * Gap) / 3f;
			Bottom(c, new UITextButton(Lang.UI("Load"), LoadSelected) {
				IsEnabled = () => _selected != null,
			}, RightColumn, w);
			var delete = new UITextButton(() => Lang.UI(_confirmDelete > 0 ? "DeleteConfirm" : "Delete"), DeleteSelected) {
				IsEnabled = () => _selected != null,
				Tint = () => _confirmDelete > 0 ? new Color(170, 50, 50) : null,
			};
			Bottom(c, delete, RightColumn + w + Gap, w);
			Bottom(c, new UITextButton(Lang.UI("NewBlueprint"), () => ShowTab(Tab.Save)), RightColumn + 2 * (w + Gap), w);
			return c;
		}

		private BlueprintPreview.Entry SelectedPreview => _selected == null ? null : BlueprintPreview.Get(_selected);

		internal string Selected => _selected;

		internal void Select(string path)
		{
			_selected = path;
			_confirmDelete = 0;
		}

		internal void LoadSelected()
		{
			if (_selected == null)
				return;
			try {
				var bp = BlueprintIO.Load(_selected);
				ProjectionSystem.Load(bp);
				if (TerraprintConfig.Instance.CloseMenuOnLoad)
					UISystem.CloseMenu();
				else
					ShowTab(Tab.Projection);
			}
			catch (Exception e) {
				Main.NewText(Lang.UI("LoadError", Path.GetFileName(_selected), e.Message), Color.OrangeRed);
				ModContent.GetInstance<Terraprint>().Logger.Error("Failed to load blueprint " + _selected, e);
			}
		}

		/// <summary>Primeiro clique pede confirmacao; o segundo (em ate 3 s) apaga o arquivo.</summary>
		private void DeleteSelected()
		{
			if (_selected == null)
				return;
			if (_confirmDelete == 0) {
				_confirmDelete = 180;
				return;
			}
			try {
				File.Delete(_selected);
				Lang.Info(Lang.UI("Deleted", Path.GetFileName(_selected)));
			}
			catch (Exception e) {
				Main.NewText(Lang.UI("DeleteError", e.Message), Color.OrangeRed);
			}
			_selected = null;
			_confirmDelete = 0;
			RefreshFiles();
		}

		public void RefreshFiles()
		{
			if (_fileList == null)
				return;
			_fileList.Clear();
			var files = BlueprintIO.ListFiles();
			BlueprintPreview.Retain(new HashSet<string>(files));
			if (_selected != null && !files.Contains(_selected))
				_selected = null;
			if (_selected == null && files.Count > 0)
				_selected = files[0];
			foreach (string file in files)
				_fileList.Add(new CatalogRow(file));
		}

		// ----- Aba Salvar -----

		private UIElement BuildSaveTab()
		{
			var c = NewContent();

			var nameLabel = Label(c, 0f, 8f, 0.9f, Color.White, fill: false);
			nameLabel.SetText(Lang.UI("NameLabel"));
			_nameBox = new UITextBox(Lang.UI("NameHint"));
			Place(c, _nameBox, 64f, 0f, 340f);

			Place(c, new UITextButton(Lang.UI("SelectArea"), SelectionSystem.Begin) {
				IsActive = () => SelectionSystem.Active,
			}, 0f, 44f, 196f);
			Place(c, new UITextButton(Lang.UI("Save"), SaveSelection) {
				IsEnabled = () => SelectionSystem.HasSelection,
			}, 196f + Gap, 44f, 196f);

			_selectionInfo = Label(c, 0f, 90f, 0.85f, TerraprintStyle.Muted);
			var hint = Label(c, 0f, 120f, 0.8f, TerraprintStyle.Highlight);
			hint.SetText(Lang.UI("SaveHint"));
			return c;
		}

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
				_selected = path;
				RefreshFiles();
				ShowTab(Tab.Catalog);
			}
			catch (Exception e) {
				Main.NewText(Lang.UI("SaveError", e.Message), Color.OrangeRed);
				ModContent.GetInstance<Terraprint>().Logger.Error("Failed to save blueprint", e);
			}
		}

		// ----- Aba Projecao -----

		private UIElement BuildProjectionTab()
		{
			var c = NewContent();

			_projectionInfo = Label(c, 0f, 0f, 0.85f, Color.White);
			_projectionInfo.Width.Set(LeftColumn, 0f);

			var cfg = () => TerraprintConfig.Instance;
			float y = 96f;
			Place(c, new UITextButton(() => Lang.UI("ToggleGhost", OnOff(cfg().ShowGhost)),
				() => TerraprintConfig.Set(x => x.ShowGhost = !x.ShowGhost)) {
				IsActive = () => cfg().ShowGhost,
			}, 0f, y, LeftColumn);
			Place(c, new UITextButton(() => Lang.UI("ToggleMap", OnOff(cfg().ShowOnMinimap || cfg().ShowOnFullscreenMap)),
				() => TerraprintConfig.Set(x => {
					bool on = !(x.ShowOnMinimap || x.ShowOnFullscreenMap);
					x.ShowOnMinimap = x.ShowOnFullscreenMap = on;
				})) {
				IsActive = () => cfg().ShowOnMinimap || cfg().ShowOnFullscreenMap,
			}, 0f, y + ButtonHeight + Gap, LeftColumn);
			Place(c, new UITextButton(() => Lang.UI("ToggleCorrect", OnOff(cfg().ShowCorrect)),
				() => TerraprintConfig.Set(x => x.ShowCorrect = !x.ShowCorrect)) {
				IsActive = () => cfg().ShowCorrect,
			}, 0f, y + 2 * (ButtonHeight + Gap), LeftColumn);

			// Setas: movem 1 bloco (as setas do teclado tambem).
			y += 3 * (ButtonHeight + Gap) + 8f;
			float a = (LeftColumn - 3 * Gap) / 4f;
			int[] rot = { 3, 0, 2, 1 };
			Point[] dir = { new(-1, 0), new(0, -1), new(0, 1), new(1, 0) };
			for (int i = 0; i < 4; i++) {
				Point d = dir[i];
				Place(c, new UITextButton("Up", Lang.UI("NudgeHint"), () => ProjectionSystem.Nudge(d.X, d.Y), rot[i]) {
					IsEnabled = () => ProjectionSystem.HasProjection,
				}, i * (a + Gap), y, a);
			}

			var box = Box(c, RightColumn, 0f, ButtonHeight + Gap + 26f);
			box.Append(new UIPreviewImage(() => ProjectionSystem.HasProjection
				? BlueprintPreview.ForProjection(ProjectionSystem.Current, ProjectionSystem.Version) : null));
			_projectionHint = Label(c, RightColumn, 0f, 0.8f, TerraprintStyle.Highlight);
			_projectionHint.Top.Set(-ButtonHeight - Gap - 22f, 1f);

			float w = (PanelWidth - 2 * Pad - 4 * Gap) / 5f;
			Bottom(c, ProjectionButton("Rotate", ProjectionSystem.Rotate), 0f, w);
			Bottom(c, ProjectionButton("Flip", ProjectionSystem.Flip), w + Gap, w);
			Bottom(c, ProjectionButton("Move", ProjectionSystem.StartMoving), 2 * (w + Gap), w);
			Bottom(c, ProjectionButton("Remove", ProjectionSystem.Clear), 3 * (w + Gap), w);
			_buildButton = new UITextButton(() => Lang.UI(_buildButton.Held ? "AutoBuilding" : "AutoBuild"), null) {
				WhileHeld = AutoBuildSystem.RequestHold,
				IsEnabled = () => ProjectionSystem.State == ProjectionSystem.ProjectionState.Fixed,
			};
			Bottom(c, _buildButton, 4 * (w + Gap), w);
			return c;
		}

		private static UITextButton ProjectionButton(string key, Action onClick) => new(Lang.UI(key), onClick) {
			IsEnabled = () => ProjectionSystem.HasProjection,
		};

		private static string OnOff(bool on) => on ? Lang.UI("On") : Lang.UI("Off");

		// ----- Aba Materiais -----

		private UIElement BuildMaterialsTab()
		{
			var c = NewContent();
			_materialsInfo = Label(c, 0f, 0f, 0.85f, TerraprintStyle.Muted);
			_materialsInfo.Height.Set(40f, 0f);

			float y = 44f;
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
			_nameBox?.Unfocus();
			_panel.Recalculate();
		}

		public override void OnActivate()
		{
			RefreshFiles();
			if (_tab == Tab.Projection && !ProjectionSystem.HasProjection && !SelectionSystem.Active)
				ShowTab(Tab.Catalog);
			else if (_tab == Tab.Catalog && ProjectionSystem.HasProjection)
				ShowTab(Tab.Projection);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (_confirmDelete > 0)
				_confirmDelete--;

			switch (_tab) {
				case Tab.Catalog:
					UpdateCatalogText();
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
				case Tab.Projection:
					UpdateProjectionText();
					break;
				case Tab.Materials:
					MaterialsTracker.Update();
					_materialsInfo.SetText(MaterialsSummary.Text());
					break;
			}
		}

		private void UpdateCatalogText()
		{
			if (_selected == null) {
				_fileName.SetText(Lang.UI("NoFiles"));
				_fileInfo.SetText("");
				_fileHint.SetText("");
				return;
			}
			_fileName.SetText(Path.GetFileNameWithoutExtension(_selected));
			var e = SelectedPreview;
			if (e == null || e.Failed) {
				_fileInfo.SetText("");
				_fileHint.SetText(Lang.UI("PreviewError"));
				return;
			}
			_fileInfo.SetText(Lang.UI("FileInfo", e.Width, e.Height, e.Tiles));
			_fileHint.SetText(Lang.UI("LoadHint"));
		}

		private void UpdateProjectionText()
		{
			if (!ProjectionSystem.HasProjection) {
				_projectionInfo.SetText(Lang.UI("NoProjection"));
				_projectionHint.SetText("");
				return;
			}
			var bp = ProjectionSystem.Current;
			string flip = ProjectionSystem.FlippedX ? Lang.UI("Mirrored") : "";
			_projectionInfo.SetText(Lang.UI("ProjectionLine", bp.Name, bp.Width, bp.Height, ProjectionSystem.Rotation * 90, flip));
			if (ProjectionSystem.State != ProjectionSystem.ProjectionState.Fixed)
				_projectionHint.SetText(Lang.UI("StateMoving"));
			else if (_buildButton.Held)
				_projectionHint.SetText(Lang.UI("AutoBuilding"));
			else
				_projectionHint.SetText(GhostSystem.ProgressText());
		}

		public bool IsMouseOverPanel => _panel != null && _panel.ContainsPoint(Main.MouseScreen);
		public bool IsTyping => _nameBox != null && _nameBox.Focused;
	}

	/// <summary>Imagem de previa que preenche o elemento pai, centralizada e sem distorcer.</summary>
	public class UIPreviewImage : UIElement
	{
		private readonly Func<BlueprintPreview.Entry> _source;

		public UIPreviewImage(Func<BlueprintPreview.Entry> source)
		{
			_source = source;
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			BlueprintPreview.Draw(spriteBatch, _source(), GetDimensions().ToRectangle());
		}
	}

	/// <summary>Uma linha da lista de blueprints: nome e tamanho. Clique seleciona; clique duplo carrega.</summary>
	public class CatalogRow : UIPanel
	{
		public readonly string FilePath;
		private readonly UIText _size;
		private bool _sizeSet;

		public CatalogRow(string path)
		{
			FilePath = path;
			Width.Set(0f, 1f);
			Height.Set(28f, 0f);
			SetPadding(0f);
			BorderColor = Color.Black;

			string display = Path.GetFileNameWithoutExtension(path);
			if (display.Length > 17)
				display = display.Substring(0, 16) + "…";
			var name = new UIText(display, 0.8f);
			name.Left.Set(8f, 0f);
			name.VAlign = 0.5f;
			Append(name);

			_size = new UIText("", 0.65f) { TextColor = TerraprintStyle.Muted, TextOriginX = 1f };
			_size.Width.Set(60f, 0f);
			_size.Left.Set(-66f, 1f);
			_size.VAlign = 0.5f;
			Append(_size);
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick);
			UISystem.Menu?.Select(FilePath);
		}

		public override void LeftDoubleClick(UIMouseEvent evt)
		{
			base.LeftDoubleClick(evt);
			UISystem.Menu?.Select(FilePath);
			UISystem.Menu?.LoadSelected();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (!_sizeSet) {
				_sizeSet = true;
				var e = BlueprintPreview.Get(FilePath);
				if (e != null && !e.Failed)
					_size.SetText($"{e.Width}x{e.Height}");
			}
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			bool selected = UISystem.Menu?.Selected == FilePath;
			BackgroundColor = selected ? TerraprintStyle.RowSelected : IsMouseHovering ? TerraprintStyle.RowHover : TerraprintStyle.Row;
			base.DrawSelf(spriteBatch);
		}
	}
}
