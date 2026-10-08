using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using Terraprint.Common.Blueprints;
using Terraprint.Common.Systems;

namespace Terraprint.Common.UI
{
	/// <summary>Menu principal do Terraprint (abre com a tecla do mod, padrao K).</summary>
	public class TerraprintMenu : UIState
	{
		private const float PanelWidth = 440f;
		private const float Pad = 10f;

		private UIDragPanel _panel;
		private UITextBox _nameBox;
		private UIText _selectionInfo;
		private UIButton _saveButton;
		private UIList _fileList;

		public override void OnInitialize()
		{
			_panel = new UIDragPanel();
			_panel.Width.Set(PanelWidth, 0f);
			_panel.Height.Set(520f, 0f);
			_panel.Left.Set(Main.screenWidth / 2f - PanelWidth / 2f, 0f);
			_panel.Top.Set(120f, 0f);
			_panel.SetPadding(Pad);
			Append(_panel);

			float y = 0f;
			var title = new UIText(Lang.UI("Title"), 0.6f, large: true);
			_panel.Append(title);

			var close = new UIButton("X", UISystem.CloseMenu);
			close.Width.Set(30f, 0f);
			close.Height.Set(30f, 0f);
			close.Left.Set(-30f, 1f);
			_panel.Append(close);
			y += 40f;

			// --- Salvar construcao ---
			y = AddHeader(Lang.UI("SaveHeader"), y);

			_nameBox = new UITextBox(Lang.UI("NameHint"));
			_nameBox.Width.Set(0f, 1f);
			_nameBox.Height.Set(32f, 0f);
			_nameBox.Top.Set(y, 0f);
			_panel.Append(_nameBox);
			y += 38f;

			var select = new UIButton(Lang.UI("SelectArea"), SelectionSystem.Begin);
			Place(select, y, 0f, 0.5f, -4f);
			_saveButton = new UIButton(Lang.UI("Save"), SaveSelection);
			Place(_saveButton, y, 0.5f, 0.5f, -4f, leftOffset: 4f);
			y += 38f;

			_selectionInfo = new UIText("", 0.85f);
			_selectionInfo.Top.Set(y, 0f);
			_panel.Append(_selectionInfo);
			y += 26f;

			// --- Blueprints salvos ---
			y = AddHeader(Lang.UI("FilesHeader"), y);

			var refresh = new UIButton(Lang.UI("Refresh"), RefreshFiles);
			Place(refresh, y, 0f, 0.5f, -4f);
			var openFolder = new UIButton(Lang.UI("OpenFolder"), () => {
				BlueprintIO.EnsureFolder();
				Utils.OpenFolder(BlueprintIO.Folder);
			});
			Place(openFolder, y, 0.5f, 0.5f, -4f, leftOffset: 4f);
			y += 38f;

			var listPanel = new UIPanel();
			listPanel.Top.Set(y, 0f);
			listPanel.Width.Set(0f, 1f);
			listPanel.Height.Set(-y, 1f);
			listPanel.SetPadding(6f);
			listPanel.BackgroundColor = new Color(33, 43, 79) * 0.6f;
			_panel.Append(listPanel);

			_fileList = new UIList();
			_fileList.Width.Set(-24f, 1f);
			_fileList.Height.Set(0f, 1f);
			_fileList.ListPadding = 4f;
			listPanel.Append(_fileList);

			var scroll = new UIScrollbar();
			scroll.Height.Set(0f, 1f);
			scroll.Left.Set(-18f, 1f);
			listPanel.Append(scroll);
			_fileList.SetScrollbar(scroll);
		}

		private float AddHeader(string text, float y)
		{
			var header = new UIText(text, 1f);
			header.Top.Set(y, 0f);
			header.TextColor = Color.LightSkyBlue;
			_panel.Append(header);
			return y + 26f;
		}

		private void Place(UIElement el, float y, float left, float width, float widthPixels, float leftOffset = 0f, float height = 32f)
		{
			el.Top.Set(y, 0f);
			el.Left.Set(leftOffset, left);
			el.Width.Set(widthPixels, width);
			el.Height.Set(height, 0f);
			_panel.Append(el);
		}

		public override void OnActivate()
		{
			RefreshFiles();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			_saveButton.Enabled = SelectionSystem.HasSelection;
			if (SelectionSystem.Active)
				_selectionInfo.SetText(SelectionSystem.CornerA.HasValue ? Lang.UI("InfoSecondCorner") : Lang.UI("InfoFirstCorner"));
			else if (SelectionSystem.HasSelection) {
				var r = SelectionSystem.SelectionRect;
				_selectionInfo.SetText(Lang.UI("InfoSelection", r.Width, r.Height));
			}
			else
				_selectionInfo.SetText(Lang.UI("InfoNoSelection"));
		}

		public bool IsMouseOverPanel => _panel != null && _panel.ContainsPoint(Main.MouseScreen);

		private void SaveSelection()
		{
			if (!SelectionSystem.HasSelection)
				return;
			try {
				var bp = SelectionSystem.CaptureSelection(_nameBox.Text);
				string path = BlueprintIO.Save(bp);
				Main.NewText(Lang.UI("Saved", Path.GetFileName(path), bp.Width, bp.Height), Color.LightGreen);
				_nameBox.Text = "";
				SelectionSystem.Cancel();
				RefreshFiles();
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
			if (files.Count == 0) {
				_fileList.Add(new UIText(Lang.UI("NoFiles"), 0.85f));
				return;
			}
			foreach (string file in files)
				_fileList.Add(new BlueprintFileRow(file));
		}
	}

	/// <summary>Uma linha da lista de arquivos .tprint.</summary>
	public class BlueprintFileRow : UIPanel
	{
		public readonly string FilePath;

		public BlueprintFileRow(string path)
		{
			FilePath = path;
			Width.Set(0f, 1f);
			Height.Set(40f, 0f);
			SetPadding(6f);

			var name = new UIText(Path.GetFileNameWithoutExtension(path), 0.9f);
			name.VAlign = 0.5f;
			Append(name);
		}

		public override int CompareTo(object obj) => 0; // mantem a ordem da pasta (mais recente primeiro)
	}
}
