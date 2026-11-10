using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
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
	/// Janela do Terraprint (tecla do mod, padrao K). Barra lateral com as paginas (Biblioteca, Salvar area,
	/// Projecao, Materiais, Opcoes) e o estado da projecao embaixo; a pagina escolhida ocupa o resto.
	/// O editor de schematic abre numa janela propria, no lugar desta.
	/// </summary>
	public class TerraprintMenu : UIState
	{
		public enum Tab { Library, Save, Projection, Materials, Options }

		private const float SidebarW = 178f;
		private const float Pad = 14f;
		private const float Inset = 8f;
		private const float HeaderH = 44f;
		// Topo da area abaixo do cabecalho (barra lateral e titulo da pagina).
		private const float ContentTop = Inset + HeaderH + Inset;
		private const float PageLeft = Inset + SidebarW + Pad;
		private const float PageTop = ContentTop + 58f;
		private const float BtnH = 32f;
		private const float Gap = 6f;

		private static readonly (string icon, string key)[] Pages = {
			("Load", "PageLibrary"), ("Save", "PageSave"), ("Projection", "PageProjection"), ("Materials", "PageMaterials"), ("Options", "PageOptions"),
		};

		private UITerraprintPanel _panel;
		private UIElement _body;
		private readonly UIElement[] _pages = new UIElement[5];
		private Tab _tab = Tab.Library;

		// Biblioteca
		private UITextBox _search;
		private BlueprintGrid _grid;
		private string _selected;
		private int _confirmDelete;

		// Salvar
		private UITextBox _nameBox;

		// Projecao
		private UITextButton _buildButton;

		// Opcoes
		private UIElement _pickerOverlay;
		private UIPaletteBrowser _picker;
		private Action<int> _pickerCallback;

		// Editor
		private SchematicEditor _editor;

		public bool EditorOpen => _editor?.Parent != null;

		public override void OnInitialize()
		{
			float sw = Main.screenWidth / Main.UIScale, sh = Main.screenHeight / Main.UIScale;
			float w = Math.Min(920f, sw - 40f), h = Math.Min(600f, sh - 100f);
			_panel = new UITerraprintPanel { DragHeight = Inset + HeaderH, HeaderHeight = HeaderH, HeaderInset = Inset };
			_panel.Width.Set(w, 0f);
			_panel.Height.Set(h, 0f);
			_panel.Left.Set((sw - w) / 2f, 0f);
			_panel.Top.Set(Math.Max(40f, (sh - h) / 2f - 30f), 0f);
			_panel.SetPadding(0f);
			Append(_panel);

			BuildSidebar();

			var close = new UITextButton("X", UISystem.CloseMenu) { Tooltip = () => Lang.UI("Close") };
			Put(_panel, close, -Inset - 8f - 30f, Inset + (HeaderH - 30f) / 2f, 30f, 30f, rightAligned: true);

			_body = new UIElement();
			_body.Left.Set(PageLeft, 0f);
			_body.Top.Set(PageTop, 0f);
			_body.Width.Set(-PageLeft - Pad, 1f);
			_body.Height.Set(-PageTop - Pad, 1f);
			_panel.Append(_body);

			_pages[(int)Tab.Library] = BuildLibrary();
			_pages[(int)Tab.Save] = BuildSave();
			_pages[(int)Tab.Projection] = BuildProjection();
			_pages[(int)Tab.Materials] = BuildMaterials();
			_pages[(int)Tab.Options] = BuildOptions();
			_body.Append(_pages[(int)_tab]);

			_editor = new SchematicEditor(CloseEditor);
		}

		// ----- Ajudantes -----

		private static T Put<T>(UIElement parent, T el, float x, float y, float w, float h, bool rightAligned = false) where T : UIElement
		{
			el.Left.Set(x, rightAligned ? 1f : 0f);
			el.Top.Set(y, 0f);
			el.Width.Set(w, 0f);
			el.Height.Set(h, 0f);
			parent.Append(el);
			return el;
		}

		private static UIElement Page()
		{
			var p = new UIElement();
			p.Width.Set(0f, 1f);
			p.Height.Set(0f, 1f);
			return p;
		}

		private static UIText Wrapped(UIElement parent, string text, float y, float scale, Color color)
		{
			var t = new UIText(text, scale) { IsWrapped = true, TextOriginX = 0f, TextColor = color };
			t.Top.Set(y, 0f);
			t.Width.Set(0f, 1f);
			parent.Append(t);
			return t;
		}

		private static TerraprintConfig Cfg => TerraprintConfig.Instance;

		// ----- Barra lateral -----

		private void BuildSidebar()
		{
			// Cabecalho: nome do mod com a fonte de titulos do jogo e o subtitulo ao lado.
			var title = new UILabel(() => Lang.UI("Title"), 0.62f, TerraprintStyle.Title) { Big = true, VCenter = true };
			Put(_panel, title, Inset + 14f, Inset, 260f, HeaderH);
			var subtitle = new UILabel(() => Lang.UI("Subtitle"), 0.75f, TerraprintStyle.Title * 0.8f) { VCenter = true };
			float titleW = Terraria.GameContent.FontAssets.DeathText.Value.MeasureString(Lang.UI("Title")).X * 0.62f;
			Put(_panel, subtitle, Inset + 14f + titleW + 16f, Inset, 400f, HeaderH);

			// Barra lateral: abas empilhadas num painel mais escuro, como as abas de categoria do jogo.
			var side = new UIBox(TerraprintStyle.Sidebar);
			side.Left.Set(Inset, 0f);
			side.Top.Set(ContentTop, 0f);
			side.Width.Set(SidebarW, 0f);
			side.Height.Set(-ContentTop - Inset, 1f);
			_panel.Append(side);

			float y = 10f;
			for (int i = 0; i < Pages.Length; i++) {
				var tab = (Tab)i;
				string key = Pages[i].key;
				var nav = new UINavButton(Pages[i].icon, () => Lang.UI(key), () => ShowTab(tab)) { IsActive = () => _tab == tab };
				nav.Left.Set(8f, 0f);
				nav.Width.Set(-16f, 1f);
				nav.Top.Set(y, 0f);
				side.Append(nav);
				y += 42f;
			}

			var badge = new UIProjectionBadge();
			badge.Left.Set(8f, 0f);
			badge.Width.Set(-16f, 1f);
			badge.Top.Set(-84f, 1f);
			badge.Height.Set(76f, 0f);
			badge.OnLeftClick += (_, _) => ShowTab(Tab.Projection);
			side.Append(badge);

			// Titulo da pagina, ao lado da barra, com o divisor do jogo embaixo.
			Put(_panel, new UILabel(() => Lang.UI(Pages[(int)_tab].key), 1.05f, TerraprintStyle.Highlight), PageLeft, ContentTop + 2f, 400f, 26f);
			Put(_panel, new UILabel(() => Lang.UI(Pages[(int)_tab].key + "Sub"), 0.75f, TerraprintStyle.Muted), PageLeft, ContentTop + 26f, 560f, 18f);
			var divider = new UIDivider();
			divider.Left.Set(PageLeft, 0f);
			divider.Top.Set(ContentTop + 46f, 0f);
			divider.Width.Set(-PageLeft - Pad, 1f);
			divider.Height.Set(4f, 0f);
			_panel.Append(divider);
		}

		// ----- Biblioteca -----

		private UIElement BuildLibrary()
		{
			var p = Page();
			_search = new UITextBox(Lang.UI("SearchHint"));
			Put(p, _search, 0f, 0f, 220f, BtnH);

			float x = 0f;
			void Right(UITextButton b, float w)
			{
				x += w;
				Put(p, b, -x, 0f, w, BtnH, rightAligned: true);
				x += Gap;
			}
			Right(new UITextButton(Lang.UI("NewBlank"), NewBlank) { Tooltip = () => Lang.UI("NewBlankTip") }, 120f);
			Right(new UITextButton(Lang.UI("OpenFolder"), () => {
				BlueprintIO.EnsureFolder();
				Utils.OpenFolder(BlueprintIO.Folder);
			}), 100f);
			Right(new UITextButton(Lang.UI("Refresh"), RefreshFiles), 90f);

			_grid = new BlueprintGrid(this) { Filter = () => _search.Text };
			_grid.Top.Set(BtnH + 10f, 0f);
			_grid.Width.Set(-20f, 1f);
			_grid.Height.Set(-BtnH - 10f - BtnH - 12f, 1f);
			p.Append(_grid);
			var scroll = new UIScrollbar();
			scroll.Left.Set(-16f, 1f);
			scroll.Top.Set(BtnH + 14f, 0f);
			scroll.Height.Set(-BtnH - 18f - BtnH - 12f, 1f);
			p.Append(scroll);
			_grid.Scrollbar = scroll;

			// Barra de acoes do selecionado.
			var info = new UILabel(SelectedInfo, 0.8f, TerraprintStyle.Muted);
			info.Top.Set(-BtnH + 7f, 1f);
			info.Width.Set(-3 * 106f - 10f, 1f);
			info.Height.Set(24f, 0f);
			p.Append(info);
			float bx = 0f;
			void Action(UITextButton b)
			{
				bx += 100f;
				b.Left.Set(-bx, 1f);
				b.Top.Set(-BtnH, 1f);
				b.Width.Set(100f, 0f);
				b.Height.Set(BtnH, 0f);
				p.Append(b);
				bx += Gap;
			}
			Action(new UITextButton(() => Lang.UI(_confirmDelete > 0 ? "DeleteConfirmShort" : "Delete"), DeleteSelected) {
				IsEnabled = () => _selected != null,
				Tint = () => _confirmDelete > 0 ? TerraprintStyle.Danger : null,
				Tooltip = () => _confirmDelete > 0 ? Lang.UI("DeleteConfirm") : null,
			});
			Action(new UITextButton(Lang.UI("Edit"), EditSelected) { IsEnabled = () => _selected != null, Tooltip = () => Lang.UI("EditTip") });
			Action(new UITextButton(Lang.UI("Load"), LoadSelected) { IsEnabled = () => _selected != null, IsActive = () => _selected != null });
			return p;
		}

		private string SelectedInfo()
		{
			if (_selected == null)
				return _grid != null && _grid.Count == 0 ? "" : Lang.UI("PickOne");
			var e = BlueprintPreview.Get(_selected);
			string name = Path.GetFileNameWithoutExtension(_selected);
			if (e == null || e.Failed)
				return name + "  ·  " + Lang.UI("PreviewError");
			string info = name + "  ·  " + Lang.UI("FileInfo", e.Width, e.Height, e.Tiles);
			if (e.Mods.Count > 0)
				info += "  ·  " + ModsText(e.Mods, e.MissingMods);
			return info;
		}

		/// <summary>"Mods: Calamity Mod, Thorium (falta: Thorium)".</summary>
		internal static string ModsText(List<string> mods, List<string> missing)
		{
			string text = Lang.UI("ModsUsed", string.Join(", ", mods.ConvertAll(Blueprint.ModDisplayName)));
			if (missing.Count > 0)
				text += " " + Lang.UI("ModsMissing", string.Join(", ", missing));
			return text;
		}

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
				ProjectionSystem.Load(bp, _selected);
				if (Cfg.CloseMenuOnLoad)
					UISystem.CloseMenu();
				else
					ShowTab(Tab.Projection);
			}
			catch (Exception e) {
				Main.NewText(Lang.UI("LoadError", Path.GetFileName(_selected), e.Message), Color.OrangeRed);
				ModContent.GetInstance<Terraprint>().Logger.Error("Failed to load blueprint " + _selected, e);
			}
		}

		private void EditSelected()
		{
			if (_selected != null)
				OpenEditor(_selected);
		}

		private void NewBlank()
		{
			var bp = new Blueprint(32, 20) { Name = Lang.UI("NewBlankName"), Author = Main.LocalPlayer?.name ?? "" };
			OpenEditor(bp, null);
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
			if (_grid == null)
				return;
			var files = BlueprintIO.ListFiles();
			BlueprintPreview.Retain(new HashSet<string>(files));
			if (_selected != null && !files.Contains(_selected))
				_selected = null;
			if (_selected == null && files.Count > 0)
				_selected = files[0];
			_grid.SetFiles(files);
		}

		// ----- Salvar area -----

		private UIElement BuildSave()
		{
			var p = Page();
			float y = 0f;

			var step1 = Put(p, new UIStepCard(1, () => Lang.UI("Step1"), SelectionText), 0f, y, 0f, 74f);
			step1.Width.Set(0f, 1f);
			Put(step1, new UITextButton(() => Lang.UI(SelectionSystem.Active ? "Selecting" : "SelectArea"), SelectionSystem.Begin) {
				IsActive = () => SelectionSystem.Active,
			}, -190f, 21f, 176f, BtnH, rightAligned: true);
			y += 84f;

			var step2 = Put(p, new UIStepCard(2, () => Lang.UI("Step2"), () => Lang.UI("Step2Sub")), 0f, y, 0f, 74f);
			step2.Width.Set(0f, 1f);
			_nameBox = new UITextBox(Lang.UI("NameHint"));
			Put(step2, _nameBox, -290f, 21f, 276f, BtnH, rightAligned: true);
			y += 84f;

			var step3 = Put(p, new UIStepCard(3, () => Lang.UI("Step3"), () => Lang.UI("SaveHint")), 0f, y, 0f, 74f);
			step3.Width.Set(0f, 1f);
			Put(step3, new UITextButton(Lang.UI("Save"), SaveSelection) {
				IsEnabled = () => SelectionSystem.HasSelection,
				IsActive = () => SelectionSystem.HasSelection,
			}, -190f, 21f, 176f, BtnH, rightAligned: true);
			y += 92f;

			Wrapped(p, Lang.UI("SaveFootnote"), y, 0.75f, TerraprintStyle.Muted);
			return p;
		}

		private static string SelectionText()
		{
			if (SelectionSystem.Active)
				return SelectionSystem.CornerA.HasValue ? Lang.UI("InfoSecondCorner") : Lang.UI("InfoFirstCorner");
			if (SelectionSystem.HasSelection) {
				var r = SelectionSystem.SelectionRect;
				return Lang.UI("InfoSelection", r.Width, r.Height);
			}
			return Lang.UI("InfoNoSelection");
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
				ShowTab(Tab.Library);
			}
			catch (Exception e) {
				Main.NewText(Lang.UI("SaveError", e.Message), Color.OrangeRed);
				ModContent.GetInstance<Terraprint>().Logger.Error("Failed to save blueprint", e);
			}
		}

		// ----- Projecao -----

		private UIElement BuildProjection()
		{
			var p = Page();
			const float right = 250f;

			// Previa grande com nome e progresso.
			var card = new UIBox(TerraprintStyle.Card);
			card.Width.Set(-right - 12f, 1f);
			card.Height.Set(0f, 1f);
			p.Append(card);
			var preview = new UIBox(TerraprintStyle.PreviewBackground);
			preview.Left.Set(10f, 0f);
			preview.Top.Set(10f, 0f);
			preview.Width.Set(-20f, 1f);
			preview.Height.Set(-84f, 1f);
			preview.SetPadding(8f);
			card.Append(preview);
			preview.Append(new UIPreviewImage(() => ProjectionSystem.HasProjection
				? BlueprintPreview.ForProjection(ProjectionSystem.Current, ProjectionSystem.Version) : null));
			var empty = new UILabel(() => ProjectionSystem.HasProjection ? "" : Lang.UI("NoProjection"), 0.8f, TerraprintStyle.Muted) { Center = true };
			empty.Width.Set(0f, 1f);
			empty.Top.Set(-12f, 0.5f);
			empty.Height.Set(24f, 0f);
			preview.Append(empty);

			var line = new UILabel(ProjectionLine, 0.85f, TerraprintStyle.Title);
			line.Left.Set(12f, 0f);
			line.Top.Set(-68f, 1f);
			line.Width.Set(-24f, 1f);
			line.Height.Set(22f, 0f);
			card.Append(line);
			var bar = new UIProgress();
			bar.Left.Set(12f, 0f);
			bar.Top.Set(-40f, 1f);
			bar.Width.Set(-24f, 1f);
			bar.Height.Set(28f, 0f);
			card.Append(bar);

			// Controles a direita.
			var col = new UIElement();
			col.Left.Set(-right, 1f);
			col.Width.Set(right, 0f);
			col.Height.Set(0f, 1f);
			p.Append(col);
			float y = 0f;
			float half = (right - Gap) / 2f;

			col.Append(Header(Lang.UI("GroupTransform"), y));
			y += 22f;
			Put(col, ProjButton("Rotate", ProjectionSystem.Rotate), 0f, y, half, BtnH);
			Put(col, ProjButton("Flip", ProjectionSystem.Flip), half + Gap, y, half, BtnH);
			y += BtnH + 10f;

			col.Append(Header(Lang.UI("GroupPosition"), y));
			y += 22f;
			Put(col, ProjButton("Move", ProjectionSystem.StartMoving), 0f, y, half, BtnH);
			// Setas (as do teclado tambem funcionam com o menu aberto).
			float a = (half - 3 * 3f) / 4f;
			int[] rot = { 3, 0, 2, 1 };
			Point[] dir = { new(-1, 0), new(0, -1), new(0, 1), new(1, 0) };
			for (int i = 0; i < 4; i++) {
				Point d = dir[i];
				Put(col, new UITextButton("Up", Lang.UI("NudgeHint"), () => ProjectionSystem.Nudge(d.X, d.Y), rot[i]) {
					IsEnabled = () => ProjectionSystem.HasProjection,
				}, half + Gap + i * (a + 3f), y, a, BtnH);
			}
			y += BtnH + 10f;

			col.Append(Header(Lang.UI("GroupView"), y));
			y += 22f;
			Put(col, new UIToggleRow(Lang.UI("ShowGhostShort"), null, () => Cfg.ShowGhost, v => TerraprintConfig.Set(c => c.ShowGhost = v)), 0f, y, right, 32f);
			y += 36f;
			Put(col, new UIToggleRow(Lang.UI("ShowMapShort"), null, () => Cfg.ShowOnMinimap || Cfg.ShowOnFullscreenMap,
				v => TerraprintConfig.Set(c => c.ShowOnMinimap = c.ShowOnFullscreenMap = v)), 0f, y, right, 32f);
			y += 36f;
			Put(col, new UIToggleRow(Lang.UI("ShowCorrectShort"), null, () => Cfg.ShowCorrect, v => TerraprintConfig.Set(c => c.ShowCorrect = v)), 0f, y, right, 32f);
			y += 32f + 10f;

			col.Append(Header(Lang.UI("GroupMovement"), y));
			y += 22f;
			Put(col, new UIToggleRow(Lang.UI("NoClipShort"), Lang.UI("NoClipTip"), () => Players.TerraprintPlayer.NoClip, Players.TerraprintPlayer.SetNoClip), 0f, y, right, 32f);

			// Acoes principais embaixo.
			_buildButton = new UITextButton(() => Lang.UI(_buildButton.Held ? "AutoBuilding" : "AutoBuild"), null) {
				WhileHeld = AutoBuildSystem.RequestHold,
				IsEnabled = () => ProjectionSystem.State == ProjectionSystem.ProjectionState.Fixed,
				IsActive = () => ProjectionSystem.State == ProjectionSystem.ProjectionState.Fixed,
				Tooltip = () => Lang.UI("AutoBuildTip"),
			};
			_buildButton.Top.Set(-2 * BtnH - Gap, 1f);
			_buildButton.Width.Set(half, 0f);
			_buildButton.Height.Set(BtnH, 0f);
			col.Append(_buildButton);
			// Constroi o blueprint inteiro com o que tiver no inventario, sem precisar estar perto.
			var buildAll = new UITextButton(() => Lang.UI(AutoBuildSystem.BuildingAll ? "BuildAllStop" : "BuildAll"), AutoBuildSystem.ToggleBuildAll) {
				IsEnabled = () => ProjectionSystem.State == ProjectionSystem.ProjectionState.Fixed || AutoBuildSystem.BuildingAll,
				IsActive = () => AutoBuildSystem.BuildingAll,
				Tooltip = () => Lang.UI("BuildAllTip"),
			};
			Put(col, buildAll, half + Gap, 0f, half, BtnH).Top.Set(-2 * BtnH - Gap, 1f);
			var edit = new UITextButton(Lang.UI("Edit"), () => {
				if (ProjectionSystem.SourcePath != null && File.Exists(ProjectionSystem.SourcePath))
					OpenEditor(ProjectionSystem.SourcePath);
				else if (ProjectionSystem.Source != null)
					OpenEditor(ProjectionSystem.Source.Clone(), null);
			}) { IsEnabled = () => ProjectionSystem.HasProjection, Tooltip = () => Lang.UI("EditTip") };
			Put(col, edit, 0f, 0f, half, BtnH).Top.Set(-BtnH, 1f);
			Put(col, ProjButton("Remove", ProjectionSystem.Clear), half + Gap, 0f, half, BtnH).Top.Set(-BtnH, 1f);
			return p;
		}

		private static UIElement Header(string text, float y)
		{
			var h = new UILabel(() => text, 0.78f, TerraprintStyle.Accent);
			h.Top.Set(y, 0f);
			h.Width.Set(0f, 1f);
			h.Height.Set(20f, 0f);
			return h;
		}

		private static UITextButton ProjButton(string key, Action onClick) => new(Lang.UI(key), onClick) {
			IsEnabled = () => ProjectionSystem.HasProjection,
		};

		private static string ProjectionLine()
		{
			if (!ProjectionSystem.HasProjection)
				return Lang.UI("NoProjectionShort");
			var bp = ProjectionSystem.Current;
			string flip = ProjectionSystem.FlippedX ? Lang.UI("Mirrored") : "";
			return Lang.UI("ProjectionLine", bp.Name, bp.Width, bp.Height, ProjectionSystem.Rotation * 90, flip);
		}

		// ----- Materiais -----

		private UIElement BuildMaterials()
		{
			var p = Page();
			var info = new UILabel(MaterialsSummary.Text, 0.85f, TerraprintStyle.Title);
			info.Width.Set(0f, 1f);
			info.Height.Set(24f, 0f);
			p.Append(info);

			var box = new UIBox(TerraprintStyle.Card);
			box.Top.Set(32f, 0f);
			box.Width.Set(0f, 1f);
			box.Height.Set(-32f, 1f);
			box.SetPadding(10f);
			p.Append(box);

			var grid = new MaterialsGrid();
			grid.Width.Set(-22f, 1f);
			grid.Height.Set(0f, 1f);
			box.Append(grid);
			var scroll = new UIScrollbar();
			scroll.Left.Set(-18f, 1f);
			scroll.Top.Set(4f, 0f);
			scroll.Height.Set(-8f, 1f);
			box.Append(scroll);
			grid.Scrollbar = scroll;
			return p;
		}

		// ----- Opcoes -----

		private UIElement BuildOptions()
		{
			var p = Page();
			var list = new UIList { ListPadding = 6f, ManualSortMethod = _ => { } };
			list.Width.Set(-22f, 1f);
			list.Height.Set(0f, 1f);
			p.Append(list);
			var scroll = new UIScrollbar();
			scroll.Left.Set(-16f, 1f);
			scroll.Top.Set(4f, 0f);
			scroll.Height.Set(-8f, 1f);
			p.Append(scroll);
			list.SetScrollbar(scroll);

			static string L(string key) => Lang.UI(key);
			static string Tip(string key) => Lang.UI(key + "Tip");
			void Toggle(string key, Func<TerraprintConfig, bool> get, Action<TerraprintConfig, bool> set) =>
				list.Add(new UIToggleRow(L(key), Tip(key), () => get(Cfg), v => TerraprintConfig.Set(c => set(c, v))));
			void Slider(string key, float min, float max, float step, Func<TerraprintConfig, float> get, Action<TerraprintConfig, float> set) =>
				list.Add(new UISliderRow(L(key), Tip(key), min, max, step, () => get(Cfg), v => TerraprintConfig.Set(c => set(c, v))));
			void Stepper(string key, int min, int max, int step, Func<TerraprintConfig, int> get, Action<TerraprintConfig, int> set) =>
				list.Add(new UIStepperRow(L(key), Tip(key), min, max, step, () => get(Cfg), v => TerraprintConfig.Set(c => set(c, v))));

			list.Add(new UISectionHeader(L("SecBlacklists")));
			list.Add(new UIItemListRow(L("OptTileBlacklist"), Tip("OptTileBlacklist"), () => Cfg.TileBlacklist,
				v => TerraprintConfig.Set(c => c.TileBlacklist = v), cb => OpenPicker(false, cb)));
			list.Add(new UIItemListRow(L("OptWallBlacklist"), Tip("OptWallBlacklist"), () => Cfg.WallBlacklist,
				v => TerraprintConfig.Set(c => c.WallBlacklist = v), cb => OpenPicker(true, cb)));

			list.Add(new UISectionHeader(L("SecRendering")));
			Slider("OptOpacity", 0.1f, 1f, 0.05f, c => c.GhostOpacity, (c, v) => c.GhostOpacity = v);
			Toggle("OptBlink", c => c.EnableBlinking, (c, v) => c.EnableBlinking = v);
			Stepper("OptTileBlink", 5, 240, 5, c => c.TileBlinkDelay, (c, v) => c.TileBlinkDelay = v);
			Stepper("OptWallBlink", 5, 240, 5, c => c.WallBlinkDelay, (c, v) => c.WallBlinkDelay = v);

			list.Add(new UISectionHeader(L("SecLayers")));
			Toggle("OptShowTiles", c => c.ShowTiles, (c, v) => c.ShowTiles = v);
			Toggle("OptShowWalls", c => c.ShowWalls, (c, v) => c.ShowWalls = v);
			Toggle("OptShowPaint", c => c.ShowPaint, (c, v) => c.ShowPaint = v);
			Toggle("OptShowLiquids", c => c.ShowLiquids, (c, v) => c.ShowLiquids = v);

			list.Add(new UISectionHeader(L("SecActuator")));
			Toggle("OptShowActuated", c => c.ShowActuated, (c, v) => c.ShowActuated = v);
			Slider("OptActuatedOpacity", 0.1f, 1f, 0.05f, c => c.ActuatedOpacity, (c, v) => c.ActuatedOpacity = v);
			Toggle("OptActuatorMismatch", c => c.HighlightActuatorMismatch, (c, v) => c.HighlightActuatorMismatch = v);
			Stepper("OptActuatorBorder", 1, 4, 1, c => c.ActuatorBorder, (c, v) => c.ActuatorBorder = v);

			list.Add(new UISectionHeader(L("SecGhost")));
			Toggle("OptShowCorrect", c => c.ShowCorrect, (c, v) => c.ShowCorrect = v);
			Toggle("OptObstructions", c => c.MarkObstructions, (c, v) => c.MarkObstructions = v);
			Toggle("OptOutline", c => c.ShowOutline, (c, v) => c.ShowOutline = v);
			Toggle("OptProgress", c => c.ShowProgress, (c, v) => c.ShowProgress = v);
			Toggle("OptMinimap", c => c.ShowOnMinimap, (c, v) => c.ShowOnMinimap = v);
			Toggle("OptFullMap", c => c.ShowOnFullscreenMap, (c, v) => c.ShowOnFullscreenMap = v);

			list.Add(new UISectionHeader(L("SecAutoBuild")));
			Stepper("OptPerTick", 1, 10, 1, c => c.AutoBuildPerTick, (c, v) => c.AutoBuildPerTick = v);
			Toggle("OptBuildWalls", c => c.AutoBuildWalls, (c, v) => c.AutoBuildWalls = v);
			Toggle("OptBuildFurniture", c => c.AutoBuildFurniture, (c, v) => c.AutoBuildFurniture = v);
			Toggle("OptBuildSlopes", c => c.AutoBuildSlopes, (c, v) => c.AutoBuildSlopes = v);

			list.Add(new UISectionHeader(L("SecIntegrations")));
			Toggle("OptMagicStorage", c => c.MagicStorageButton, (c, v) => c.MagicStorageButton = v);

			var all = new UITextButton(L("AllOptions"), () => Cfg.Open()) { Tooltip = () => L("AllOptionsTip") };
			all.Width.Set(0f, 1f);
			all.Height.Set(BtnH, 0f);
			list.Add(all);

			// Seletor de itens das listas de bloqueio (cobre a pagina enquanto aberto).
			_pickerOverlay = new UIBox(TerraprintStyle.PanelBackground);
			_pickerOverlay.Width.Set(0f, 1f);
			_pickerOverlay.Height.Set(0f, 1f);
			_pickerOverlay.SetPadding(10f);
			var ptitle = new UILabel(() => Lang.UI("PickerTitle"), 0.9f, TerraprintStyle.Title);
			ptitle.Top.Set(4f, 0f);
			ptitle.Width.Set(-120f, 1f);
			ptitle.Height.Set(24f, 0f);
			_pickerOverlay.Append(ptitle);
			Put(_pickerOverlay, new UITextButton(Lang.UI("Cancel"), ClosePicker), -110f, 0f, 110f, 28f, rightAligned: true);
			return p;
		}

		private void OpenPicker(bool walls, Action<int> onPick)
		{
			_picker?.Remove();
			_pickerCallback = onPick;
			var cats = walls ? new[] { PaletteCategory.Walls }
				: new[] { PaletteCategory.Blocks, PaletteCategory.Furniture, PaletteCategory.Decoration, PaletteCategory.Entities };
			_picker = new UIPaletteBrowser(cats, e => {
				_pickerCallback?.Invoke(e.ItemType);
				ClosePicker();
			}, () => null);
			_picker.Top.Set(36f, 0f);
			_picker.Width.Set(0f, 1f);
			_picker.Height.Set(-36f, 1f);
			_pickerOverlay.Append(_picker);
			_pages[(int)Tab.Options].Append(_pickerOverlay);
			_pickerOverlay.Recalculate();
		}

		private void ClosePicker()
		{
			_picker?.Unfocus();
			_pickerOverlay?.Remove();
			_pickerCallback = null;
		}

		// ----- Editor -----

		public void OpenEditor(string path)
		{
			try {
				var bp = BlueprintIO.Load(path);
				if (bp.UnknownTypes > 0) {
					bp.GetMods(out _, out var missing);
					Main.NewText(Lang.UI("UnknownTypesEditor", bp.UnknownTypes, string.Join(", ", missing)), Color.Orange);
				}
				OpenEditor(bp, path);
			}
			catch (Exception e) {
				Main.NewText(Lang.UI("LoadError", Path.GetFileName(path), e.Message), Color.OrangeRed);
			}
		}

		public void OpenEditor(Blueprint bp, string path)
		{
			_nameBox?.Unfocus();
			_search?.Unfocus();
			ClosePicker();
			_panel.Remove();
			if (_editor.Parent == null)
				Append(_editor);
			_editor.Activate();
			Recalculate();
			_editor.Open(bp, path);
		}

		private void CloseEditor()
		{
			_editor.Remove();
			if (_panel.Parent == null)
				Append(_panel);
			Recalculate();
			RefreshFiles();
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
			ClosePicker();
			_pages[(int)_tab].Remove();
			_tab = tab;
			_body.Append(_pages[(int)tab]);
			_nameBox?.Unfocus();
			_search?.Unfocus();
			_body.Recalculate();
		}

		public override void OnActivate()
		{
			RefreshFiles();
			if (_tab == Tab.Projection && !ProjectionSystem.HasProjection && !SelectionSystem.Active)
				ShowTab(Tab.Library);
			else if (_tab == Tab.Library && ProjectionSystem.HasProjection)
				ShowTab(Tab.Projection);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (_confirmDelete > 0)
				_confirmDelete--;
			if (_tab == Tab.Materials)
				MaterialsTracker.Update();
		}

		public bool IsMouseOverPanel =>
			_panel != null && _panel.Parent != null && _panel.ContainsPoint(Main.MouseScreen)
			|| EditorOpen && _editor.ContainsPoint(Main.MouseScreen);

		public bool IsTyping =>
			(_nameBox?.Focused ?? false) || (_search?.Focused ?? false) || (_picker != null && _picker.Parent != null && _picker.IsTyping)
			|| EditorOpen && _editor.IsTyping;
	}

	// ======================= Elementos usados pelo menu =======================

	/// <summary>Divisor horizontal do jogo ocupando a largura do elemento.</summary>
	public class UIDivider : UIElement
	{
		public UIDivider() => IgnoresMouseInteraction = true;

		protected override void DrawSelf(SpriteBatch sb)
		{
			var d = GetDimensions();
			TerraprintStyle.DrawSeparator(sb, d.X, d.Y, d.Width);
		}
	}

	/// <summary>Caixa arredondada de uma cor (fundo da barra lateral, cartoes).</summary>
	public class UIBox : UIElement
	{
		public Color Color;

		public UIBox(Color color) => Color = color;

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
		}

		protected override void DrawSelf(SpriteBatch sb) => TerraprintStyle.RoundedBox(sb, GetDimensions().ToRectangle(), Color, TerraprintStyle.PanelBorder);
	}

	/// <summary>Texto de uma linha que muda sozinho (cortado com "..." se nao couber).</summary>
	public class UILabel : UIElement
	{
		private readonly Func<string> _text;
		private readonly float _scale;
		public Color Color;
		public bool Center;
		/// <summary>Usa a fonte grande de titulos do jogo.</summary>
		public bool Big;
		/// <summary>Centraliza o texto na altura do elemento.</summary>
		public bool VCenter;

		public UILabel(Func<string> text, float scale, Color color)
		{
			_text = text;
			_scale = scale;
			Color = color;
			IgnoresMouseInteraction = true;
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			var d = GetDimensions();
			if (Big) {
				TerraprintStyle.TitleText(sb, _text(), Center ? d.X + d.Width / 2f : d.X, d.Y + d.Height / 2f, Color, _scale, d.Width, Center ? 0.5f : 0f);
				return;
			}
			string t = TerraprintStyle.Ellipsis(_text() ?? "", _scale, d.Width);
			if (VCenter)
				d.Y += d.Height / 2f - 10f * _scale;
			if (Center)
				TerraprintStyle.Text(sb, t, new Vector2(d.X + d.Width / 2f, d.Y), Color, _scale, 0f, 0.5f);
			else
				TerraprintStyle.Text(sb, t, d.Position(), Color, _scale);
		}
	}

	/// <summary>Barra de progresso da projecao (blocos certos / total), com o texto por cima.</summary>
	public class UIProgress : UIElement
	{
		protected override void DrawSelf(SpriteBatch sb)
		{
			var r = GetDimensions().ToRectangle();
			TerraprintStyle.RoundedBox(sb, r, TerraprintStyle.InputBackground, TerraprintStyle.PanelBorder);
			if (!ProjectionSystem.HasProjection)
				return;
			bool fixedState = ProjectionSystem.State == ProjectionSystem.ProjectionState.Fixed;
			float f = fixedState ? ProgressTracker.Percent : 0f;
			var fill = new Rectangle(r.X + 3, r.Y + 3, (int)((r.Width - 6) * f), r.Height - 6);
			TerraprintStyle.Fill(sb, fill, (f >= 1f ? TerraprintStyle.Good : TerraprintStyle.Accent) * 0.75f);
			string text = fixedState
				? Lang.UI("ProgressBar", (int)(f * 100f), ProgressTracker.Correct, ProgressTracker.Total, ProgressTracker.Missing, ProgressTracker.Wrong)
				: Lang.UI("StateMovingShort");
			TerraprintStyle.Text(sb, text, new Vector2(r.Center.X, r.Y + r.Height / 2f - 9f), TerraprintStyle.Title, 0.75f, r.Width - 12f, 0.5f);
		}
	}

	/// <summary>Cartao no pe da barra lateral com o nome e o progresso da projecao ativa.</summary>
	public class UIProjectionBadge : UIElement
	{
		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			var r = GetDimensions().ToRectangle();
			TerraprintStyle.RoundedBox(sb, r, IsMouseHovering ? TerraprintStyle.CardHover : TerraprintStyle.Card, TerraprintStyle.PanelBorder);
			TerraprintStyle.Text(sb, Lang.UI("BadgeTitle"), new Vector2(r.X + 10, r.Y + 8), TerraprintStyle.Accent, 0.7f);
			if (!ProjectionSystem.HasProjection) {
				TerraprintStyle.Text(sb, Lang.UI("BadgeNone"), new Vector2(r.X + 10, r.Y + 28), TerraprintStyle.Muted, 0.72f, r.Width - 20);
				return;
			}
			string name = TerraprintStyle.Ellipsis(ProjectionSystem.Current.Name ?? "", 0.8f, r.Width - 20);
			TerraprintStyle.Text(sb, name, new Vector2(r.X + 10, r.Y + 26), TerraprintStyle.Title, 0.8f);
			var bar = new Rectangle(r.X + 10, r.Bottom - 18, r.Width - 20, 10);
			TerraprintStyle.Fill(sb, new Rectangle(bar.X - 1, bar.Y - 1, bar.Width + 2, bar.Height + 2), TerraprintStyle.PanelBorder);
			TerraprintStyle.Fill(sb, bar, TerraprintStyle.InputBackground);
			bool fixedState = ProjectionSystem.State == ProjectionSystem.ProjectionState.Fixed;
			float f = fixedState ? ProgressTracker.Percent : 0f;
			TerraprintStyle.Fill(sb, new Rectangle(bar.X, bar.Y, (int)(bar.Width * f), bar.Height), f >= 1f ? TerraprintStyle.Good : TerraprintStyle.Accent);
			string pct = fixedState ? (int)(f * 100f) + "%" : Lang.UI("BadgeMoving");
			TerraprintStyle.Text(sb, pct, new Vector2(r.Right - 10, r.Y + 8), TerraprintStyle.Muted, 0.65f, 0f, 1f);
		}
	}

	/// <summary>Cartao de um passo (numero, titulo e texto de apoio); os controles ficam a direita.</summary>
	public class UIStepCard : UIElement
	{
		private readonly int _n;
		private readonly Func<string> _title, _sub;

		public UIStepCard(int n, Func<string> title, Func<string> sub)
		{
			_n = n;
			_title = title;
			_sub = sub;
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			var r = GetDimensions().ToRectangle();
			TerraprintStyle.RoundedBox(sb, r, TerraprintStyle.Card, TerraprintStyle.PanelBorder);
			var circle = new Rectangle(r.X + 14, r.Y + r.Height / 2 - 16, 32, 32);
			TerraprintStyle.DrawSlot(sb, circle, true, false, true);
			TerraprintStyle.Text(sb, _n.ToString(), new Vector2(circle.Center.X, circle.Y + 6), TerraprintStyle.Highlight, 0.95f, 0f, 0.5f);
			float textW = r.Width - 60 - 300;
			TerraprintStyle.Text(sb, _title(), new Vector2(r.X + 58, r.Y + 16), TerraprintStyle.Title, 0.9f, textW);
			TerraprintStyle.Text(sb, TerraprintStyle.Ellipsis(_sub() ?? "", 0.72f, textW), new Vector2(r.X + 58, r.Y + 42), TerraprintStyle.Muted, 0.72f);
		}
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
			IgnoresMouseInteraction = true;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			BlueprintPreview.Draw(spriteBatch, _source(), GetDimensions().ToRectangle());
		}
	}

	/// <summary>
	/// Grade de cartoes dos blueprints salvos: previa, nome e tamanho. Clique seleciona, clique duplo carrega.
	/// </summary>
	public class BlueprintGrid : UIElement
	{
		private const float CardW = 152f, CardH = 136f, GapX = 10f, GapY = 10f;

		private static readonly RasterizerState ClipRaster = new() { CullMode = CullMode.None, ScissorTestEnable = true };

		private readonly TerraprintMenu _menu;
		private List<string> _files = new();
		private List<string> _shown = new();
		private string _filter;
		private int _hovered = -1;
		public UIScrollbar Scrollbar;
		public Func<string> Filter = () => "";

		public BlueprintGrid(TerraprintMenu menu) => _menu = menu;

		public int Count => _files.Count;

		public void SetFiles(List<string> files)
		{
			_files = files;
			_filter = null;
		}

		private int Columns => Math.Max(1, (int)((GetDimensions().Width + GapX) / (CardW + GapX)));
		private const float RowH = CardH + GapY;

		private Rectangle CardRect(int i)
		{
			var d = GetDimensions();
			int cols = Columns;
			// Espalha as colunas para ocupar a largura toda.
			float w = (d.Width - (cols - 1) * GapX) / cols;
			float y = d.Y + i / cols * RowH - (Scrollbar?.ViewPosition ?? 0f);
			return new Rectangle((int)(d.X + i % cols * (w + GapX)), (int)y, (int)w, (int)CardH);
		}

		public override void ScrollWheel(UIScrollWheelEvent evt)
		{
			base.ScrollWheel(evt);
			if (Scrollbar != null)
				Scrollbar.ViewPosition -= evt.ScrollWheelValue / 2f;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			string f = (Filter() ?? "").Trim().ToLowerInvariant();
			if (f != _filter) {
				_filter = f;
				_shown = f.Length == 0 ? new List<string>(_files)
					: _files.FindAll(p => Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains(f));
			}
			var d = GetDimensions();
			int rows = (_shown.Count + Columns - 1) / Columns;
			Scrollbar?.SetView(d.Height, Math.Max(d.Height, rows * RowH - GapY));

			_hovered = -1;
			if (!IsMouseHovering)
				return;
			Main.LocalPlayer.mouseInterface = true;
			Terraria.GameInput.PlayerInput.LockVanillaMouseScroll("Terraprint/Library");
			for (int i = 0; i < _shown.Count; i++) {
				if (CardRect(i).Contains(Main.MouseScreen.ToPoint())) {
					_hovered = i;
					break;
				}
			}
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			if (_hovered < 0)
				return;
			SoundEngine.PlaySound(SoundID.MenuTick);
			_menu.Select(_shown[_hovered]);
		}

		public override void LeftDoubleClick(UIMouseEvent evt)
		{
			base.LeftDoubleClick(evt);
			if (_hovered < 0)
				return;
			_menu.Select(_shown[_hovered]);
			_menu.LoadSelected();
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			var box = GetDimensions().ToRectangle();
			if (_shown.Count == 0) {
				TerraprintStyle.Text(sb, Lang.UI(_files.Count == 0 ? "NoFiles" : "NoMatches"), new Vector2(box.X + 4, box.Y + 6), TerraprintStyle.Muted, 0.8f, box.Width - 8);
				return;
			}
			// Corta os cartoes que saem da area (rolagem).
			var device = sb.GraphicsDevice;
			Rectangle oldScissor = device.ScissorRectangle;
			RasterizerState oldRaster = device.RasterizerState;
			sb.End();
			device.ScissorRectangle = Rectangle.Intersect(GetClippingRectangle(sb), oldScissor);
			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, ClipRaster, null, Main.UIScaleMatrix);

			for (int i = 0; i < _shown.Count; i++) {
				var r = CardRect(i);
				if (r.Bottom < box.Y || r.Y > box.Bottom)
					continue;
				string path = _shown[i];
				bool sel = _menu.Selected == path;
				bool hover = i == _hovered;
				TerraprintStyle.RoundedBox(sb, r, sel || hover ? TerraprintStyle.CardHover : TerraprintStyle.Card,
					sel ? TerraprintStyle.Accent : TerraprintStyle.PanelBorder);
				var img = new Rectangle(r.X + 8, r.Y + 8, r.Width - 16, r.Height - 52);
				TerraprintStyle.RoundedBox(sb, img, TerraprintStyle.PreviewBackground, TerraprintStyle.PanelBorder);
				var e = BlueprintPreview.Get(path);
				BlueprintPreview.Draw(sb, e, new Rectangle(img.X + 3, img.Y + 3, img.Width - 6, img.Height - 6), hover || sel ? 1f : 0.85f);
				string name = TerraprintStyle.Ellipsis(Path.GetFileNameWithoutExtension(path), 0.8f, r.Width - 16);
				TerraprintStyle.Text(sb, name, new Vector2(r.X + 8, r.Bottom - 40), sel ? TerraprintStyle.Accent : TerraprintStyle.Title, 0.8f);
				string size = e == null || e.Failed ? Lang.UI("PreviewErrorShort") : $"{e.Width} x {e.Height}";
				TerraprintStyle.Text(sb, size, new Vector2(r.X + 8, r.Bottom - 21), TerraprintStyle.Muted, 0.68f);
			}

			sb.End();
			device.ScissorRectangle = oldScissor;
			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, oldRaster, null, Main.UIScaleMatrix);
		}
	}
}
