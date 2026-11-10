using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.UI;
using Terraprint.Common.Blueprints;
using Terraprint.Common.Systems;

namespace Terraprint.Common.UI
{
	public enum EditorTool { Brush, Rect, Eraser, Picker, Hammer, Actuator, Pan }

	/// <summary>Operacoes de edicao numa grade de blueprint (colocar/apagar blocos, objetos e paredes).</summary>
	public static class EditorOps
	{
		public static void ClearTile(ref BlueprintCell c)
		{
			c.HasTile = false;
			c.TileType = 0;
			c.FrameX = c.FrameY = 0;
			c.Slope = 0;
			c.HalfBlock = false;
			c.TilePaint = 0;
			c.HasActuator = false;
			c.Inactive = false;
			c.MissingTile = 0;
		}

		private static void SetTile(ref BlueprintCell c, int type, short fx, short fy)
		{
			c.HasTile = true;
			c.TileType = (ushort)type;
			c.FrameX = fx;
			c.FrameY = fy;
			c.Slope = 0;
			c.HalfBlock = false;
			c.TilePaint = 0;
			c.HasActuator = false;
			c.Inactive = false;
			c.MissingTile = 0;
		}

		/// <summary>Apaga o bloco da celula; se for parte de um movel, apaga o movel inteiro.</summary>
		public static bool EraseTile(Blueprint bp, int x, int y, Action<int> touch)
		{
			if (!bp.InBounds(x, y) || !bp[x, y].HasTile)
				return false;
			ref BlueprintCell c = ref bp[x, y];
			if (MaterialLookup.TryGetObjectCell(c, out var data, out int col, out int row) && (data.Width > 1 || data.Height > 1)) {
				int left = x - col, top = y - row;
				ushort type = c.TileType;
				for (int dy = 0; dy < data.Height; dy++) {
					for (int dx = 0; dx < data.Width; dx++) {
						int px = left + dx, py = top + dy;
						if (!bp.InBounds(px, py) || !bp[px, py].HasTile || bp[px, py].TileType != type)
							continue;
						touch(py * bp.Width + px);
						ClearTile(ref bp[px, py]);
					}
				}
				return true;
			}
			touch(y * bp.Width + x);
			ClearTile(ref c);
			return true;
		}

		/// <summary>Tamanho e ponto de origem do que este item coloca (1x1 para blocos comuns).</summary>
		public static TileObjectData Data(PaletteEntry e)
		{
			if (e == null || e.TileType < 0)
				return null;
			try {
				return TileObjectData.GetTileData(e.TileType, e.Style) ?? TileObjectData.GetTileData(e.TileType, 0);
			}
			catch {
				return null;
			}
		}

		/// <summary>Area (em celulas) que o item ocupa com o mouse em (x, y).</summary>
		public static Rectangle Footprint(PaletteEntry e, int x, int y)
		{
			var data = Data(e);
			if (data == null)
				return new Rectangle(x, y, 1, 1);
			return new Rectangle(x - data.Origin.X, y - data.Origin.Y, data.Width, data.Height);
		}

		/// <summary>
		/// Coloca o bloco/objeto do item com os quadros que o jogo usaria (mesma conta de TileObject.Place).
		/// overwrite=false nao mexe em celulas ja ocupadas.
		/// </summary>
		public static bool PlaceTile(Blueprint bp, int x, int y, PaletteEntry e, bool overwrite, Action<int> touch)
		{
			var data = Data(e);
			if (data == null) {
				if (!bp.InBounds(x, y))
					return false;
				ref BlueprintCell c = ref bp[x, y];
				if (c.HasTile && c.TileType == e.TileType)
					return false;
				if (c.HasTile && !overwrite)
					return false;
				if (c.HasTile)
					EraseTile(bp, x, y, touch);
				touch(y * bp.Width + x);
				SetTile(ref bp[x, y], e.TileType, BlueprintTransform.Reframe, 0);
				return true;
			}

			var area = Footprint(e, x, y);
			if (area.X < 0 || area.Y < 0 || area.Right > bp.Width || area.Bottom > bp.Height)
				return false;

			int n = data.CalculatePlacementStyle(e.Style, 0, -1);
			int line = 0;
			if (data.StyleWrapLimit > 0) {
				line = n / data.StyleWrapLimit * data.StyleLineSkip;
				n %= data.StyleWrapLimit;
			}
			int baseX, baseY;
			if (data.StyleHorizontal) {
				baseX = data.CoordinateFullWidth * n;
				baseY = data.CoordinateFullHeight * line;
			}
			else {
				baseX = data.CoordinateFullWidth * line;
				baseY = data.CoordinateFullHeight * n;
			}

			// Ja esta igual: nada a fazer.
			ref BlueprintCell first = ref bp[area.X, area.Y];
			if (first.HasTile && first.TileType == e.TileType && first.FrameX == baseX && first.FrameY == baseY)
				return false;

			for (int dy = 0; dy < area.Height; dy++) {
				for (int dx = 0; dx < area.Width; dx++) {
					if (!bp[area.X + dx, area.Y + dy].HasTile)
						continue;
					if (!overwrite)
						return false;
				}
			}
			for (int dy = 0; dy < area.Height; dy++) {
				for (int dx = 0; dx < area.Width; dx++)
					EraseTile(bp, area.X + dx, area.Y + dy, touch);
			}

			for (int k = 0; k < data.Width; k++) {
				int fx = baseX + k * (data.CoordinateWidth + data.CoordinatePadding);
				int fy = baseY;
				for (int l = 0; l < data.Height; l++) {
					int px = area.X + k, py = area.Y + l;
					touch(py * bp.Width + px);
					SetTile(ref bp[px, py], e.TileType, (short)fx, (short)fy);
					fy += (data.CoordinateHeights != null && l < data.CoordinateHeights.Length ? data.CoordinateHeights[l] : 16) + data.CoordinatePadding;
				}
			}
			return true;
		}

		public static bool PlaceWall(Blueprint bp, int x, int y, int wall, Action<int> touch)
		{
			if (!bp.InBounds(x, y) || bp[x, y].WallType == wall)
				return false;
			touch(y * bp.Width + x);
			bp[x, y].WallType = (ushort)wall;
			bp[x, y].WallPaint = 0;
			bp[x, y].MissingWall = 0;
			return true;
		}

		public static bool EraseWall(Blueprint bp, int x, int y, Action<int> touch)
		{
			if (!bp.InBounds(x, y) || bp[x, y].WallType == WallID.None)
				return false;
			touch(y * bp.Width + x);
			bp[x, y].WallType = WallID.None;
			bp[x, y].WallPaint = 0;
			return true;
		}

		/// <summary>Martelo: inteiro, meio-bloco, rampas 1 a 4, inteiro de novo (so blocos solidos).</summary>
		public static bool Hammer(Blueprint bp, int x, int y, Action<int> touch)
		{
			if (!bp.InBounds(x, y))
				return false;
			ref BlueprintCell c = ref bp[x, y];
			if (!c.HasTile || !Main.tileSolid[c.TileType] || Main.tileSolidTop[c.TileType] || BlueprintTransform.IsMultitile(c))
				return false;
			touch(y * bp.Width + x);
			if (c.HalfBlock) {
				c.HalfBlock = false;
				c.Slope = 1;
			}
			else if (c.Slope == 0)
				c.HalfBlock = true;
			else
				c.Slope = (byte)(c.Slope >= 4 ? 0 : c.Slope + 1);
			return true;
		}

		/// <summary>Esquerdo: poe/tira atuador. Direito: liga/desliga o bloco (desativado pelo atuador).</summary>
		public static bool Actuator(Blueprint bp, int x, int y, bool toggleInactive, Action<int> touch)
		{
			if (!bp.InBounds(x, y) || !bp[x, y].HasTile)
				return false;
			ref BlueprintCell c = ref bp[x, y];
			touch(y * bp.Width + x);
			if (toggleInactive) {
				c.Inactive = !c.Inactive;
				if (c.Inactive)
					c.HasActuator = true;
			}
			else {
				c.HasActuator = !c.HasActuator;
				if (!c.HasActuator)
					c.Inactive = false;
			}
			return true;
		}
	}

	/// <summary>Um passo de desfazer: as celulas antes/depois, ou o blueprint inteiro (quando muda de tamanho).</summary>
	internal class EditStep
	{
		public readonly Dictionary<int, BlueprintCell> Before = new();
		public Dictionary<int, BlueprintCell> After;
		public Blueprint WholeBefore, WholeAfter;
	}

	/// <summary>
	/// Janela do editor de schematic: ferramentas a esquerda, a grade no meio (zoom com a roda do mouse,
	/// arrastar com o botao do meio ou a Mao) e a paleta com todos os blocos, paredes, moveis, decoracoes e entidades.
	/// </summary>
	public class SchematicEditor : UITerraprintPanel
	{
		private const float Pad = 12f;
		private const float HeaderH = 32f;
		private const float ToolW = 110f;
		private const float PaletteW = 270f;
		private const float BtnH = 28f;
		private const float Gap = 5f;

		public Blueprint Bp { get; private set; }
		public string FilePath { get; private set; }
		public bool Dirty { get; private set; }
		public int Revision { get; private set; }

		public EditorTool Tool = EditorTool.Brush;
		/// <summary>True: a borracha e o botao direito apagam paredes; false: blocos.</summary>
		public bool WallLayer;
		public PaletteEntry Selected;

		private readonly Stack<EditStep> _undo = new();
		private readonly Stack<EditStep> _redo = new();
		private EditStep _stroke;

		private UITextBox _name;
		private UIPaletteBrowser _palette;
		private EditorCanvas _canvas;
		private int _confirmClose;
		private readonly Action _onClose;

		public SchematicEditor(Action onClose)
		{
			_onClose = onClose;
			DragHeight = HeaderH + Pad;
			HeaderHeight = HeaderH + Pad;
			HeaderInset = 6f;
			SetPadding(Pad);
		}

		public bool IsTyping => (_name?.Focused ?? false) || (_palette?.IsTyping ?? false);

		/// <summary>Abre um arquivo (ou um blueprint novo em branco, com path null).</summary>
		public void Open(Blueprint bp, string path)
		{
			Bp = bp;
			FilePath = path;
			Dirty = false;
			_undo.Clear();
			_redo.Clear();
			_stroke = null;
			_confirmClose = 0;
			Revision++;
			if (_name != null)
				_name.Text = bp.Name ?? "";
			_canvas?.FitView();
		}

		public override void OnInitialize()
		{
			float sw = Main.screenWidth / Main.UIScale, sh = Main.screenHeight / Main.UIScale;
			float w = Math.Min(1080f, sw - 40f), h = Math.Min(640f, sh - 120f);
			Width.Set(w, 0f);
			Height.Set(h, 0f);
			Left.Set((sw - w) / 2f, 0f);
			Top.Set(Math.Max(20f, (sh - h) / 2f - 20f), 0f);

			// Cabecalho: titulo, nome, salvar, projetar e fechar.
			var title = new UILabel(() => Lang.UI("EditorTitle"), 0.5f, TerraprintStyle.Title) { Big = true };
			Put(this, title, 4f, 0f, 180f, 30f);
			_name = new UITextBox(Lang.UI("NameHint")) { Text = Bp?.Name ?? "" };
			Put(this, _name, 190f, 0f, 240f, 30f);

			var close = new UITextButton(() => _confirmClose > 0 ? Lang.UI("DiscardConfirm") : "X", TryClose) {
				Tooltip = () => Lang.UI("Close"),
				Tint = () => _confirmClose > 0 ? TerraprintStyle.Danger : null,
			};
			PutRight(this, close, 0f, 0f, () => _confirmClose > 0 ? 170f : 32f);
			PutRight(this, new UITextButton(Lang.UI("EditorProject"), SaveAndProject) {
				Tooltip = () => Lang.UI("EditorProjectTip"),
			}, 32f + Gap, 0f, () => 100f);
			PutRight(this, new UITextButton(() => Dirty ? Lang.UI("Save") + " *" : Lang.UI("Save"), () => Save()) {
				IsActive = () => Dirty,
			}, 32f + 100f + 2 * Gap, 0f, () => 100f);

			float top = HeaderH + 14f;

			// Coluna de ferramentas.
			var tools = new (EditorTool tool, string key)[] {
				(EditorTool.Brush, "ToolBrush"), (EditorTool.Rect, "ToolRect"), (EditorTool.Eraser, "ToolEraser"),
				(EditorTool.Picker, "ToolPicker"), (EditorTool.Hammer, "ToolHammer"), (EditorTool.Actuator, "ToolActuator"),
				(EditorTool.Pan, "ToolPan"),
			};
			float y = top;
			foreach (var (tool, key) in tools) {
				var t = tool;
				Put(this, new UITextButton(Lang.UI(key), () => Tool = t) {
					IsActive = () => Tool == t,
					Tooltip = () => Lang.UI(key + "Tip"),
					TextScale = 0.8f,
				}, 0f, y, ToolW, BtnH);
				y += BtnH + Gap;
			}
			y += 8f;
			var layerLabel = new Terraria.GameContent.UI.Elements.UIText(Lang.UI("EditorLayer"), 0.75f) { TextColor = TerraprintStyle.Muted };
			layerLabel.Top.Set(y, 0f);
			Append(layerLabel);
			y += 20f;
			Put(this, new UITextButton(Lang.UI("LayerTiles"), () => WallLayer = false) { IsActive = () => !WallLayer, TextScale = 0.8f, Tooltip = () => Lang.UI("LayerTip") }, 0f, y, ToolW, BtnH);
			y += BtnH + Gap;
			Put(this, new UITextButton(Lang.UI("LayerWalls"), () => WallLayer = true) { IsActive = () => WallLayer, TextScale = 0.8f, Tooltip = () => Lang.UI("LayerTip") }, 0f, y, ToolW, BtnH);

			// Barra acima da grade.
			float cx = ToolW + 10f;
			float bx = cx;
			void Bar(UITextButton b, float width)
			{
				b.TextScale = 0.78f;
				Put(this, b, bx, top, width, BtnH);
				bx += width + Gap;
			}
			Bar(new UITextButton(Lang.UI("Undo"), Undo) { IsEnabled = () => _undo.Count > 0, Tooltip = () => "Ctrl+Z" }, 84f);
			Bar(new UITextButton(Lang.UI("Redo"), Redo) { IsEnabled = () => _redo.Count > 0, Tooltip = () => "Ctrl+Y" }, 84f);
			Bar(new UITextButton("-", () => _canvas.ZoomStep(-1)) { Tooltip = () => Lang.UI("ZoomTip") }, 30f);
			Bar(new UITextButton("+", () => _canvas.ZoomStep(1)) { Tooltip = () => Lang.UI("ZoomTip") }, 30f);
			Bar(new UITextButton(Lang.UI("ZoomFit"), () => _canvas.FitView()), 74f);
			Bar(new UITextButton(Lang.UI("GrowBorders"), () => Resize(4, 4, 4, 4)) { Tooltip = () => Lang.UI("GrowBordersTip") }, 96f);
			Bar(new UITextButton(Lang.UI("TrimEmpty"), Trim) { Tooltip = () => Lang.UI("TrimEmptyTip") }, 96f);

			// Grade.
			_canvas = new EditorCanvas(this);
			_canvas.Left.Set(cx, 0f);
			_canvas.Top.Set(top + BtnH + 8f, 0f);
			_canvas.Width.Set(-cx - PaletteW - 10f, 1f);
			_canvas.Height.Set(-(top + BtnH + 8f) - 26f, 1f);
			Append(_canvas);

			// Paleta.
			_palette = new UIPaletteBrowser(
				new[] { PaletteCategory.Blocks, PaletteCategory.Walls, PaletteCategory.Furniture, PaletteCategory.Decoration, PaletteCategory.Entities },
				Pick, () => Selected);
			_palette.Left.Set(-PaletteW, 1f);
			_palette.Top.Set(top, 0f);
			_palette.Width.Set(PaletteW, 0f);
			_palette.Height.Set(-top - 26f, 1f);
			Append(_palette);

			if (Bp != null)
				_canvas.FitView();
		}

		private static void Put(UIElement parent, UIElement el, float x, float y, float w, float h)
		{
			el.Left.Set(x, 0f);
			el.Top.Set(y, 0f);
			el.Width.Set(w, 0f);
			el.Height.Set(h, 0f);
			parent.Append(el);
		}

		/// <summary>Botao alinhado a direita; a largura pode mudar (ex.: o X vira "Descartar?").</summary>
		private static void PutRight(UIElement parent, UITextButton el, float fromRight, float y, Func<float> width)
		{
			el.Top.Set(y, 0f);
			el.Height.Set(30f, 0f);
			void Layout()
			{
				float w = width();
				el.Width.Set(w, 0f);
				el.Left.Set(-fromRight - w, 1f);
			}
			Layout();
			parent.Append(el);
			el.OnUpdate += _ => {
				float before = el.Width.Pixels;
				Layout();
				if (before != el.Width.Pixels)
					el.Recalculate();
			};
		}

		public void Pick(PaletteEntry e)
		{
			Selected = e;
			WallLayer = e.IsWall;
			if (Tool is EditorTool.Eraser or EditorTool.Picker or EditorTool.Hammer or EditorTool.Actuator or EditorTool.Pan)
				Tool = EditorTool.Brush;
		}

		/// <summary>Conta-gotas: pega o item da celula (bloco na camada de blocos, parede na de paredes).</summary>
		public void PickFromCell(int x, int y)
		{
			if (!Bp.InBounds(x, y))
				return;
			ref BlueprintCell c = ref Bp[x, y];
			PaletteEntry e = null;
			if (c.HasTile && (!WallLayer || c.WallType == 0)) {
				MaterialLookup.GetStyle(c, out int style, out _);
				int item = MaterialLookup.ItemForTile(c);
				e = (item > 0 ? PaletteData.FindByItem(item) : null) ?? PaletteData.FindTile(c.TileType, style);
			}
			else if (c.WallType != 0)
				e = PaletteData.FindWall(c.WallType);
			if (e == null)
				return;
			Pick(e);
			_palette.SetCategory(e.Category);
		}

		// ----- Edicao com desfazer -----

		public void BeginStroke() => _stroke = new EditStep();

		public void Touch(int index)
		{
			if (_stroke != null && !_stroke.Before.ContainsKey(index))
				_stroke.Before[index] = Bp.Cells[index];
		}

		public void Changed() => Revision++;

		public void EndStroke()
		{
			var s = _stroke;
			_stroke = null;
			if (s == null || s.Before.Count == 0)
				return;
			s.After = new Dictionary<int, BlueprintCell>();
			foreach (int i in s.Before.Keys)
				s.After[i] = Bp.Cells[i];
			Push(s);
		}

		private void Push(EditStep s)
		{
			_undo.Push(s);
			_redo.Clear();
			Dirty = true;
			_confirmClose = 0;
			Revision++;
		}

		private void Apply(EditStep s, bool undo)
		{
			if (s.WholeBefore != null) {
				Bp = undo ? s.WholeBefore : s.WholeAfter;
			}
			else {
				foreach (var kv in undo ? s.Before : s.After)
					Bp.Cells[kv.Key] = kv.Value;
			}
			Dirty = true;
			Revision++;
		}

		public void Undo()
		{
			if (_undo.Count == 0)
				return;
			var s = _undo.Pop();
			Apply(s, true);
			_redo.Push(s);
		}

		public void Redo()
		{
			if (_redo.Count == 0)
				return;
			var s = _redo.Pop();
			Apply(s, false);
			_undo.Push(s);
		}

		private void Resize(int left, int top, int right, int bottom)
		{
			var before = Bp;
			var after = Bp.Resized(left, top, right, bottom);
			if (after.Width == before.Width && after.Height == before.Height && left == 0 && top == 0)
				return;
			Bp = after;
			Push(new EditStep { WholeBefore = before, WholeAfter = after });
			_canvas.Shift(left, top);
		}

		private void Trim()
		{
			var r = Bp.ContentBounds();
			Resize(-r.X, -r.Y, -(Bp.Width - r.Right), -(Bp.Height - r.Bottom));
		}

		// ----- Salvar e fechar -----

		private bool Save()
		{
			string name = string.IsNullOrWhiteSpace(_name.Text) ? (Bp.Name ?? "") : _name.Text.Trim();
			try {
				var copy = Bp.Clone();
				copy.Name = name;
				string path = FilePath ?? BlueprintIO.FreePath(name);
				BlueprintIO.Overwrite(copy, path);
				Bp.Name = name;
				FilePath = path;
				Dirty = false;
				_confirmClose = 0;
				ProjectionSystem.ReplaceSource(path, copy);
				UISystem.Menu?.RefreshFiles();
				UISystem.Menu?.Select(path);
				Lang.Info(Lang.UI("EditorSaved", Path.GetFileName(path)), Color.LightGreen);
				return true;
			}
			catch (Exception e) {
				Main.NewText(Lang.UI("SaveError", e.Message), Color.OrangeRed);
				ModContent.GetInstance<Terraprint>().Logger.Error("Failed to save edited blueprint", e);
				return false;
			}
		}

		private void SaveAndProject()
		{
			if ((Dirty || FilePath == null) && !Save())
				return;
			Dirty = false;
			_onClose?.Invoke();
			UISystem.Menu?.Select(FilePath);
			UISystem.Menu?.LoadSelected();
		}

		private void TryClose()
		{
			if (Dirty && _confirmClose == 0) {
				_confirmClose = 240;
				return;
			}
			_confirmClose = 0;
			_onClose?.Invoke();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (_confirmClose > 0)
				_confirmClose--;

			// Atalhos (fora dos campos de texto).
			if (!IsTyping && !Main.drawingPlayerChat) {
				bool ctrl = Main.keyState.IsKeyDown(Keys.LeftControl) || Main.keyState.IsKeyDown(Keys.RightControl);
				if (ctrl && JustPressed(Keys.Z))
					Undo();
				if (ctrl && JustPressed(Keys.Y))
					Redo();
			}
		}

		private static bool JustPressed(Keys k) => Main.keyState.IsKeyDown(k) && !Main.oldKeyState.IsKeyDown(k);

		protected override void DrawSelf(SpriteBatch sb)
		{
			base.DrawSelf(sb);
			// Linha de estado embaixo.
			var d = GetInnerDimensions();
			string status = Lang.UI("EditorStatus", Bp.Width, Bp.Height, _canvas.HoverText(), Selected?.Name ?? Lang.UI("EditorNothing"));
			TerraprintStyle.Text(sb, status, new Vector2(d.X + ToolW + 10f, d.Y + d.Height - 20f), TerraprintStyle.Muted, 0.75f, d.Width - ToolW - PaletteW - 20f);
			TerraprintStyle.Text(sb, Lang.UI("EditorHelp"), new Vector2(d.X + d.Width, d.Y + d.Height - 20f), TerraprintStyle.Muted * 0.8f, 0.7f, PaletteW, 1f);
		}
	}

	/// <summary>A grade do editor: desenha o blueprint com as texturas do jogo e recebe o mouse.</summary>
	public class EditorCanvas : UIElement
	{
		private static readonly int[] Zooms = { 2, 3, 4, 6, 8, 12, 16, 24, 32, 48 };

		private readonly SchematicEditor _ed;
		private int _zoom = 16;
		private Vector2 _pan;
		private bool _left, _right, _panning, _middleWas;
		private Point _lastCell = new(-9999, -9999);
		private Point _dragStart;
		private Vector2 _panMouse, _panStart;

		private static readonly RasterizerState Scissor = new() { CullMode = CullMode.None, ScissorTestEnable = true };

		public EditorCanvas(SchematicEditor editor) => _ed = editor;

		private float Scale => _zoom / 16f;

		public void FitView()
		{
			var d = GetDimensions();
			if (d.Width <= 0 || _ed.Bp == null)
				return;
			_zoom = Zooms[0];
			foreach (int z in Zooms) {
				if (_ed.Bp.Width * z <= d.Width - 20 && _ed.Bp.Height * z <= d.Height - 20)
					_zoom = z;
			}
			_pan = new Vector2((d.Width - _ed.Bp.Width * _zoom) / 2f, (d.Height - _ed.Bp.Height * _zoom) / 2f);
		}

		/// <summary>Depois de acrescentar/cortar bordas: mantem o desenho no mesmo lugar da tela.</summary>
		public void Shift(int left, int top) => _pan -= new Vector2(left, top) * _zoom;

		public void ZoomStep(int dir, Vector2? around = null)
		{
			int i = Array.IndexOf(Zooms, _zoom);
			int ni = Math.Clamp(i + dir, 0, Zooms.Length - 1);
			if (ni == i)
				return;
			var d = GetDimensions();
			Vector2 m = around ?? new Vector2(d.Width / 2f, d.Height / 2f);
			Vector2 cell = (m - _pan) / _zoom;
			_zoom = Zooms[ni];
			_pan = m - cell * _zoom;
		}

		private Vector2 LocalMouse => Main.MouseScreen - GetDimensions().Position();

		private Point MouseCell
		{
			get {
				Vector2 c = (LocalMouse - _pan) / _zoom;
				return new Point((int)Math.Floor(c.X), (int)Math.Floor(c.Y));
			}
		}

		public string HoverText()
		{
			if (!IsMouseHovering)
				return "-";
			var c = MouseCell;
			return _ed.Bp.InBounds(c.X, c.Y) ? $"{c.X}, {c.Y}" : "-";
		}

		public override void ScrollWheel(UIScrollWheelEvent evt)
		{
			base.ScrollWheel(evt);
			ZoomStep(evt.ScrollWheelValue > 0 ? 1 : -1, LocalMouse);
		}

		public override void LeftMouseDown(UIMouseEvent evt)
		{
			base.LeftMouseDown(evt);
			if (_right)
				return;
			_left = true;
			Begin(primary: true);
		}

		public override void RightMouseDown(UIMouseEvent evt)
		{
			base.RightMouseDown(evt);
			if (_left)
				return;
			_right = true;
			Begin(primary: false);
		}

		private void Begin(bool primary)
		{
			_dragStart = MouseCell;
			_lastCell = new Point(-9999, -9999);
			if (_ed.Tool == EditorTool.Pan) {
				StartPan();
				return;
			}
			_ed.BeginStroke();
			if (_ed.Tool != EditorTool.Rect)
				ApplyAt(MouseCell, primary, first: true);
		}

		private void StartPan()
		{
			_panning = true;
			_panMouse = Main.MouseScreen;
			_panStart = _pan;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering) {
				Main.LocalPlayer.mouseInterface = true;
				PlayerInput.LockVanillaMouseScroll("Terraprint/Editor");
				// Botao do meio arrasta a vista em qualquer ferramenta.
				if (Main.mouseMiddle && !_middleWas && !_left && !_right)
					StartPan();
			}
			_middleWas = Main.mouseMiddle;

			if (_panning) {
				_pan = _panStart + (Main.MouseScreen - _panMouse);
				bool held = _ed.Tool == EditorTool.Pan && (_left || _right) ? (_left ? Main.mouseLeft : Main.mouseRight) : Main.mouseMiddle;
				if (!held) {
					_panning = false;
					_left = _right = false;
				}
				return;
			}

			if (_left || _right) {
				bool primary = _left;
				bool held = primary ? Main.mouseLeft : Main.mouseRight;
				var cell = MouseCell;
				if (held) {
					if (_ed.Tool is EditorTool.Brush or EditorTool.Eraser && cell != _lastCell)
						ApplyAt(cell, primary, first: false);
					return;
				}
				if (_ed.Tool == EditorTool.Rect)
					FillRect(_dragStart, cell, primary);
				_ed.EndStroke();
				_left = _right = false;
			}
		}

		private void ApplyAt(Point cell, bool primary, bool first)
		{
			// Arrastando, preenche o caminho entre a ultima celula e esta (o mouse pode pular celulas).
			if (!first && _lastCell.X > -9999) {
				int steps = Math.Max(Math.Abs(cell.X - _lastCell.X), Math.Abs(cell.Y - _lastCell.Y));
				for (int s = 1; s <= steps; s++) {
					var p = new Point(_lastCell.X + (cell.X - _lastCell.X) * s / steps, _lastCell.Y + (cell.Y - _lastCell.Y) * s / steps);
					ApplyOne(p, primary, false);
				}
			}
			else
				ApplyOne(cell, primary, first);
			_lastCell = cell;
		}

		private void ApplyOne(Point p, bool primary, bool first)
		{
			var bp = _ed.Bp;
			bool changed = false;
			Action<int> touch = _ed.Touch;
			switch (_ed.Tool) {
				case EditorTool.Brush:
					changed = primary ? Place(p.X, p.Y, overwrite: first) : Erase(p.X, p.Y);
					break;
				case EditorTool.Eraser:
					changed = Erase(p.X, p.Y);
					break;
				case EditorTool.Picker:
					_ed.PickFromCell(p.X, p.Y);
					break;
				case EditorTool.Hammer:
					changed = EditorOps.Hammer(bp, p.X, p.Y, touch);
					break;
				case EditorTool.Actuator:
					changed = EditorOps.Actuator(bp, p.X, p.Y, !primary, touch);
					break;
			}
			if (changed)
				_ed.Changed();
		}

		private bool Place(int x, int y, bool overwrite)
		{
			var e = _ed.Selected;
			if (e == null)
				return false;
			if (e.IsWall)
				return EditorOps.PlaceWall(_ed.Bp, x, y, e.WallType, _ed.Touch);
			// Moveis: arrastando, so coloca onde couber sem passar por cima do que ja existe.
			var data = EditorOps.Data(e);
			bool multi = data != null && (data.Width > 1 || data.Height > 1);
			return EditorOps.PlaceTile(_ed.Bp, x, y, e, overwrite || !multi, _ed.Touch);
		}

		private bool Erase(int x, int y) => _ed.WallLayer
			? EditorOps.EraseWall(_ed.Bp, x, y, _ed.Touch)
			: EditorOps.EraseTile(_ed.Bp, x, y, _ed.Touch);

		private static Rectangle RectOf(Point a, Point b) =>
			new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1);

		private void FillRect(Point a, Point b, bool primary)
		{
			var r = Rectangle.Intersect(RectOf(a, b), new Rectangle(0, 0, _ed.Bp.Width, _ed.Bp.Height));
			bool changed = false;
			var e = _ed.Selected;
			var data = primary ? EditorOps.Data(e) : null;
			int sx = data != null ? Math.Max(1, data.Width) : 1, sy = data != null ? Math.Max(1, data.Height) : 1;
			for (int y = r.Top; y < r.Bottom; y += primary ? sy : 1) {
				for (int x = r.Left; x < r.Right; x += primary ? sx : 1) {
					if (primary) {
						if (e == null)
							return;
						int px = data != null ? x + data.Origin.X : x, py = data != null ? y + data.Origin.Y : y;
						if (e.IsWall)
							changed |= EditorOps.PlaceWall(_ed.Bp, x, y, e.WallType, _ed.Touch);
						else if (EditorOps.Footprint(e, px, py).Right <= r.Right && EditorOps.Footprint(e, px, py).Bottom <= r.Bottom)
							changed |= EditorOps.PlaceTile(_ed.Bp, px, py, e, true, _ed.Touch);
					}
					else
						changed |= Erase(x, y);
				}
			}
			if (changed)
				_ed.Changed();
		}

		// ----- Desenho -----

		private Color[] _colors;
		private int _colorsRevision = -1;
		private Blueprint _colorsFor;

		private Color MapColor(int index)
		{
			if (_colorsRevision != _ed.Revision || _colorsFor != _ed.Bp || _colors == null || _colors.Length != _ed.Bp.Cells.Length) {
				_colorsRevision = _ed.Revision;
				_colorsFor = _ed.Bp;
				_colors = new Color[_ed.Bp.Cells.Length];
				for (int i = 0; i < _colors.Length; i++)
					_colors[i] = BlueprintPreview.CellColor(in _ed.Bp.Cells[i]);
			}
			return _colors[index];
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			var d = GetDimensions();
			var box = d.ToRectangle();
			TerraprintStyle.RoundedBox(sb, box, TerraprintStyle.PreviewBackground, TerraprintStyle.PanelBorder);

			var bp = _ed.Bp;
			if (bp == null)
				return;

			// Recorta o desenho na area da grade.
			var device = sb.GraphicsDevice;
			Rectangle oldScissor = device.ScissorRectangle;
			RasterizerState oldRaster = device.RasterizerState;
			sb.End();
			device.ScissorRectangle = Rectangle.Intersect(GetClippingRectangle(sb), oldScissor);
			// GetClippingRectangle usa a propria caixa; encolhe um pouco para nao cobrir a borda arredondada.
			var clip = device.ScissorRectangle;
			clip.Inflate(-(int)(3 * Main.UIScale), -(int)(3 * Main.UIScale));
			if (clip.Width > 0 && clip.Height > 0)
				device.ScissorRectangle = clip;
			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Scissor, null, Main.UIScaleMatrix);

			Vector2 origin = d.Position() + _pan;
			float z = _zoom;
			int x0 = Math.Max(0, (int)Math.Floor(-_pan.X / z));
			int y0 = Math.Max(0, (int)Math.Floor(-_pan.Y / z));
			int x1 = Math.Min(bp.Width - 1, (int)Math.Floor((d.Width - _pan.X) / z));
			int y1 = Math.Min(bp.Height - 1, (int)Math.Floor((d.Height - _pan.Y) / z));

			// Fundo da area do blueprint.
			var area = new Rectangle((int)origin.X, (int)origin.Y, (int)(bp.Width * z), (int)(bp.Height * z));
			TerraprintStyle.Fill(sb, area, TerraprintStyle.SlotColor * 0.55f);

			if (_zoom < 8) {
				// Longe: cores do mapa (mais leve e mais legivel).
				for (int y = y0; y <= y1; y++) {
					for (int x = x0; x <= x1; x++) {
						Color c = MapColor(y * bp.Width + x);
						if (c.A > 0)
							TerraprintStyle.Fill(sb, new Rectangle((int)(origin.X + x * z), (int)(origin.Y + y * z), _zoom, _zoom), c);
					}
				}
			}
			else {
				float s = Scale;
				for (int y = y0; y <= y1; y++) {
					for (int x = x0; x <= x1; x++) {
						ref BlueprintCell c = ref bp.Cells[y * bp.Width + x];
						if (c.WallType != 0)
							CellRenderer.DrawWall(sb, c, origin + new Vector2(x, y) * z, Color.White * 0.85f, s);
					}
				}
				for (int y = y0; y <= y1; y++) {
					for (int x = x0; x <= x1; x++) {
						ref BlueprintCell c = ref bp.Cells[y * bp.Width + x];
						var pos = origin + new Vector2(x, y) * z;
						if (c.LiquidAmount > 0 && !c.HasTile)
							CellRenderer.DrawLiquid(sb, c, pos, 1f, s);
						if (!c.HasTile)
							continue;
						CellRenderer.DrawTile(sb, c, pos, c.Inactive ? new Color(120, 120, 120) * 0.7f : Color.White, s);
						if (c.HasActuator) {
							int a = Math.Max(2, _zoom / 6);
							TerraprintStyle.Fill(sb, new Rectangle((int)pos.X + _zoom / 2 - a / 2, (int)pos.Y + _zoom / 2 - a / 2, a, a), new Color(255, 80, 80));
						}
					}
				}
				// Linhas da grade quando bem perto.
				if (_zoom >= 16) {
					var line = Color.White * 0.06f;
					for (int x = x0; x <= x1 + 1; x++)
						TerraprintStyle.Fill(sb, new Rectangle((int)(origin.X + x * z), (int)(origin.Y + y0 * z), 1, (int)((y1 - y0 + 1) * z)), line);
					for (int y = y0; y <= y1 + 1; y++)
						TerraprintStyle.Fill(sb, new Rectangle((int)(origin.X + x0 * z), (int)(origin.Y + y * z), (int)((x1 - x0 + 1) * z), 1), line);
				}
			}

			CellRenderer.DrawBorder(sb, new Rectangle(area.X - 2, area.Y - 2, area.Width + 4, area.Height + 4), TerraprintStyle.Accent * 0.6f, 2);

			// Previa do que o mouse vai fazer.
			if (IsMouseHovering || _left || _right) {
				var cell = MouseCell;
				Rectangle cells;
				Color color;
				if ((_left || _right) && _ed.Tool == EditorTool.Rect) {
					cells = RectOf(_dragStart, cell);
					color = _right ? TerraprintStyle.Danger : TerraprintStyle.Accent;
				}
				else if (_ed.Tool == EditorTool.Brush && _ed.Selected != null && !_right && !_ed.Selected.IsWall) {
					cells = EditorOps.Footprint(_ed.Selected, cell.X, cell.Y);
					color = TerraprintStyle.Accent;
				}
				else {
					cells = new Rectangle(cell.X, cell.Y, 1, 1);
					color = _ed.Tool == EditorTool.Eraser || _right ? TerraprintStyle.Danger : Color.White;
				}
				var r = new Rectangle((int)(origin.X + cells.X * z), (int)(origin.Y + cells.Y * z), (int)(cells.Width * z), (int)(cells.Height * z));
				TerraprintStyle.Fill(sb, r, color * 0.18f);
				CellRenderer.DrawBorder(sb, r, color * 0.9f, Math.Max(1, Math.Min(2, _zoom / 8)));
			}

			sb.End();
			device.ScissorRectangle = oldScissor;
			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, oldRaster, null, Main.UIScaleMatrix);
		}
	}
}
