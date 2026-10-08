using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;
using Terraprint.Common.Blueprints;
using Terraprint.Common.Configs;

namespace Terraprint.Common.Systems
{
	/// <summary>
	/// Mostra o blueprint no minimapa, no mapa sobreposto e no mapa grande.
	/// Cada celula vira um pixel de uma textura (cor pelo estado), esticada na escala do mapa.
	/// </summary>
	[Autoload(Side = ModSide.Client)]
	public class BlueprintMapLayer : ModMapLayer
	{
		private static Texture2D _texture;
		private static int _builtRevision = -1;
		private static int _builtVersion = -1;
		private static int _builtConfig = -1;

		public override void Unload()
		{
			var tex = _texture;
			_texture = null;
			if (tex != null)
				Main.QueueMainThreadAction(tex.Dispose);
		}

		public override void Draw(ref MapOverlayDrawContext context, ref string text)
		{
			var cfg = TerraprintConfig.Instance;
			if (!ProjectionSystem.HasProjection)
				return;
			if (Main.mapFullscreen ? !cfg.ShowOnFullscreenMap : !cfg.ShowOnMinimap)
				return;

			EnsureTexture(cfg);
			if (_texture == null)
				return;

			var bp = ProjectionSystem.Current;
			var o = ProjectionSystem.Origin;
			float scale = context.MapScale;
			Vector2 topLeft = (new Vector2(o.X, o.Y) - context.MapPosition) * scale + context.MapOffset;
			var dest = new RectangleF(topLeft.X, topLeft.Y, bp.Width * scale, bp.Height * scale);

			// No minimapa, recorta o que passa da moldura.
			var src = new RectangleF(0, 0, bp.Width, bp.Height);
			if (context.ClippingRectangle is Rectangle clip) {
				float l = System.Math.Max(dest.X, clip.Left), t = System.Math.Max(dest.Y, clip.Top);
				float r = System.Math.Min(dest.X + dest.W, clip.Right), b = System.Math.Min(dest.Y + dest.H, clip.Bottom);
				if (r <= l || b <= t)
					return;
				src = new RectangleF((l - dest.X) / scale, (t - dest.Y) / scale, (r - l) / scale, (b - t) / scale);
				dest = new RectangleF(l, t, r - l, b - t);
			}

			var sb = Main.spriteBatch;
			var srcRect = new Rectangle((int)src.X, (int)src.Y, System.Math.Max(1, (int)System.Math.Ceiling(src.W)), System.Math.Max(1, (int)System.Math.Ceiling(src.H)));
			sb.Draw(_texture, new Rectangle((int)dest.X, (int)dest.Y, (int)System.Math.Ceiling(dest.W), (int)System.Math.Ceiling(dest.H)), srcRect, Color.White * cfg.MapOpacity);

			// Contorno (so quando o retangulo inteiro esta visivel ou no mapa grande).
			if (context.ClippingRectangle == null) {
				var outline = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)(bp.Width * scale), (int)(bp.Height * scale));
				SelectionSystem.DrawRect(sb, outline, Color.Transparent, Color.Gold * cfg.MapOpacity, 2);

				if (outline.Contains(Main.MouseScreen.ToPoint()))
					text = GhostSystem.ProgressText();
			}
		}

		private static void EnsureTexture(TerraprintConfig cfg)
		{
			var bp = ProjectionSystem.Current;
			if (_texture != null && _builtRevision == ProgressTracker.Revision && _builtVersion == ProjectionSystem.Version && _builtConfig == TerraprintConfig.ChangeCount
				&& _texture.Width == bp.Width && _texture.Height == bp.Height)
				return;

			var tiles = ProgressTracker.TileStatus;
			var walls = ProgressTracker.WallStatus;
			bool haveStatus = tiles.Length == bp.Cells.Length;
			var data = new Color[bp.Cells.Length];
			for (int i = 0; i < data.Length; i++) {
				ref BlueprintCell c = ref bp.Cells[i];
				CellStatus ts = haveStatus ? tiles[i] : (c.HasTile ? CellStatus.Missing : CellStatus.None);
				CellStatus ws = haveStatus ? walls[i] : (c.WallType != WallID.None ? CellStatus.Missing : CellStatus.None);
				data[i] = CellColor(ts, ws, cfg);
			}

			if (_texture == null || _texture.Width != bp.Width || _texture.Height != bp.Height) {
				_texture?.Dispose();
				_texture = new Texture2D(Main.graphics.GraphicsDevice, bp.Width, bp.Height);
			}
			_texture.SetData(data);
			_builtRevision = ProgressTracker.Revision;
			_builtVersion = ProjectionSystem.Version;
			_builtConfig = TerraprintConfig.ChangeCount;
		}

		/// <summary>Bloco tem prioridade sobre parede; paredes aparecem mais apagadas.</summary>
		private static Color CellColor(CellStatus tile, CellStatus wall, TerraprintConfig cfg)
		{
			switch (tile) {
				case CellStatus.Wrong: return cfg.ColorWrong;
				case CellStatus.Missing: return cfg.ColorMissing;
				case CellStatus.Correct: return cfg.ShowCorrect ? cfg.ColorCorrect * 0.7f : Color.Transparent;
			}
			switch (wall) {
				case CellStatus.Wrong: return cfg.ColorWrong * 0.5f;
				case CellStatus.Missing: return cfg.ColorMissing * 0.4f;
				case CellStatus.Correct: return cfg.ShowCorrect ? cfg.ColorCorrect * 0.35f : Color.Transparent;
			}
			return Color.Transparent;
		}

		private struct RectangleF
		{
			public float X, Y, W, H;
			public RectangleF(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
		}
	}
}
