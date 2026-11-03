using System;
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
	/// Menu principal do Terraprint (abre com a tecla do mod, padrao K). Segue o visual do inventario
	/// do Terraria: painel azul, abas e botoes em forma de slot, dicas ao passar o mouse.
	/// </summary>
	public class TerraprintMenu : UIState
	{
		public enum Tab { Projection, Save, Files }

		private const float PanelWidth = 372f;
		private const float PanelHeight = 330f;
		private const float Pad = 10f;
		private const float Step = TerraprintStyle.SlotSize + 4f;
		private const float TabsTop = 30f;
		private const float ContentTop = TabsTop + TerraprintStyle.SlotSize + 16f;

		private UITerraprintPanel _panel;
		private readonly UIElement[] _contents = new UIElement[3];
		private Tab _tab = Tab.Files;

		private UITextBox _nameBox;
		private UIText _selectionInfo;
		private UIText _projectionInfo;
		private UIText _filesInfo;
		private UIIconButton _buildButton;
		private UIList _fileList;

		public override void OnInitialize()
		{
			_panel = new UITerraprintPanel {
				SeparatorY = ContentTop - 8f,
			};
			_panel.Width.Set(PanelWidth, 0f);
			_panel.Height.Set(PanelHeight, 0f);
			_panel.Left.Set(Main.screenWidth / 2f - PanelWidth / 2f, 0f);
			_panel.Top.Set(120f, 0f);
			_panel.SetPadding(Pad);
			Append(_panel);

			// --- Titulo e fechar ---
			var title = new UIText(Lang.UI("Title"), 0.9f) { TextColor = TerraprintStyle.Title };
			title.Top.Set(2f, 0f);
			_panel.Append(title);

			var close = new UIIconButton("Close", Lang.UI("Close"), UISystem.CloseMenu) { Bare = true };
			close.Width.Set(26f, 0f);
			close.Height.Set(26f, 0f);
			close.Left.Set(-26f, 1f);
			close.Top.Set(-2f, 0f);
			_panel.Append(close);

			// --- Abas (esquerda) e atalhos (direita) ---
			AddTab(Tab.Projection, "Projection", "ProjectionHeader", 0);
			AddTab(Tab.Save, "Select", "SaveHeader", 1);
			AddTab(Tab.Files, "Folder", "FilesHeader", 2);

			var materials = new UIIconButton("Materials", Lang.UI("Materials"), UISystem.ToggleMaterials) {
				IsActive = () => UISystem.MaterialsOpen,
			};
			Put(_panel, materials, -2f * Step + 4f, TabsTop, right: true);
			var options = new UIIconButton("Options", Lang.UI("Options"), () => TerraprintConfig.Instance.Open());
			Put(_panel, options, -TerraprintStyle.SlotSize, TabsTop, right: true);

			_contents[(int)Tab.Projection] = BuildProjectionTab();
			_contents[(int)Tab.Save] = BuildSaveTab();
			_contents[(int)Tab.Files] = BuildFilesTab();
			_panel.Append(_contents[(int)_tab]);
		}

		private void AddTab(Tab tab, string icon, string nameKey, int index)
		{
			var b = new UIIconButton(icon, Lang.UI(nameKey), () => ShowTab(tab)) {
				IsActive = () => _tab == tab,
			};
			Put(_panel, b, index * Step, TabsTop);
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

		// ----- Aba Projecao -----

		private UIElement BuildProjectionTab()
		{
			var c = NewContent();

			_projectionInfo = Label("", 0.8f, Color.White);
			_projectionInfo.Height.Set(58f, 0f);
			c.Append(_projectionInfo);

			float y = 62f;
			Put(c, ProjectionButton("Rotate", "Rotate", ProjectionSystem.Rotate), 0f, y);
			Put(c, ProjectionButton("Mirror", "Flip", ProjectionSystem.Flip), Step, y);
			Put(c, ProjectionButton("Move", "Move", ProjectionSystem.StartMoving), 2 * Step, y);
			Put(c, ProjectionButton("Remove", "Remove", ProjectionSystem.Clear), 3 * Step, y);

			_buildButton = new UIIconButton(ItemID.IronHammer, Lang.UI("AutoBuild"), null) {
				WhileHeld = AutoBuildSystem.RequestHold,
				IsEnabled = () => ProjectionSystem.State == ProjectionSystem.ProjectionState.Fixed,
				Tint = () => new Color(150, 230, 150),
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

		// ----- Aba Arquivos -----

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

			float y = Step + 2f;
			var listPanel = new UIPanel {
				BackgroundColor = TerraprintStyle.InputBackground,
				BorderColor = TerraprintStyle.PanelBorder,
			};
			listPanel.Top.Set(y, 0f);
			listPanel.Width.Set(0f, 1f);
			listPanel.Height.Set(-y, 1f);
			listPanel.SetPadding(4f);
			c.Append(listPanel);

			_fileList = new UIList();
			_fileList.Width.Set(-22f, 1f);
			_fileList.Height.Set(0f, 1f);
			_fileList.ListPadding = 3f;
			_fileList.ManualSortMethod = _ => { }; // mantem a ordem da pasta (mais recente primeiro)
			listPanel.Append(_fileList);

			var scroll = new UIScrollbar();
			scroll.Height.Set(-8f, 1f);
			scroll.Top.Set(4f, 0f);
			scroll.Left.Set(-18f, 1f);
			listPanel.Append(scroll);
			_fileList.SetScrollbar(scroll);
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
				ShowTab(Tab.Files);
			else if (_tab == Tab.Files && ProjectionSystem.HasProjection)
				ShowTab(Tab.Projection);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

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
			}
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

		public bool IsMouseOverPanel => _panel != null && _panel.ContainsPoint(Main.MouseScreen);
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
			_filesInfo.SetText(files.Count == 0 ? Lang.UI("NoFiles") : Lang.UI("FilesCount", files.Count));
			foreach (string file in files)
				_fileList.Add(new BlueprintFileRow(file));
		}
	}

	/// <summary>Uma linha da lista de arquivos .tprint: nome, carregar e excluir.</summary>
	public class BlueprintFileRow : UIElement
	{
		private const float ButtonSize = 30f;

		public readonly string FilePath;
		private int _confirmTimer;

		public BlueprintFileRow(string path)
		{
			FilePath = path;
			Width.Set(0f, 1f);
			Height.Set(36f, 0f);

			string display = Path.GetFileNameWithoutExtension(path);
			if (display.Length > 26)
				display = display.Substring(0, 25) + "…";
			var name = new UIText(display, 0.85f);
			name.Left.Set(8f, 0f);
			name.VAlign = 0.5f;
			Append(name);

			var load = new UIIconButton("Load", Lang.UI("Load"), LoadFile);
			Size(load);
			load.Left.Set(-ButtonSize - 3f, 1f);
			Append(load);

			var delete = new UIIconButton("Delete", Lang.UI("Delete"), DeleteFile) {
				Tint = () => _confirmTimer > 0 ? new Color(255, 120, 120) : null,
			};
			delete.Tooltip = () => Lang.UI(_confirmTimer > 0 ? "DeleteConfirm" : "Delete");
			Size(delete);
			delete.Left.Set(-2 * ButtonSize - 6f, 1f);
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
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			var r = GetDimensions().ToRectangle();
			Color c = IsMouseHovering ? new Color(63, 105, 170) * 0.8f : new Color(45, 85, 150) * 0.6f;
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, r, c);
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
