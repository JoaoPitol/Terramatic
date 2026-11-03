using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Map;
using Terraprint.Common.Blueprints;

namespace Terraprint.Common.UI
{
	/// <summary>
	/// Imagem de previa de um blueprint salvo, com as cores do mapa do jogo (blocos, paredes e liquidos).
	/// Fica em cache por arquivo e e refeita quando o arquivo muda.
	/// </summary>
	public static class BlueprintPreview
	{
		/// <summary>Maior lado da imagem, em pixels. Blueprints pequenos usam ate 4 pixels por bloco.</summary>
		private const int MaxSide = 512;
		private const float MaxPixelsPerTile = 4f;

		public class Entry
		{
			public DateTime WriteTime;
			public Texture2D Texture;
			public int Width, Height;
			/// <summary>Blocos e objetos (celulas com bloco).</summary>
			public int Tiles;
			public bool Failed;
		}

		private static readonly Dictionary<string, Entry> _cache = new();

		public static Entry Get(string path)
		{
			DateTime time;
			try {
				time = File.GetLastWriteTimeUtc(path);
			}
			catch {
				return null;
			}
			if (_cache.TryGetValue(path, out var e) && e.WriteTime == time)
				return e;

			e?.Texture?.Dispose();
			e = new Entry { WriteTime = time };
			try {
				var bp = BlueprintIO.Load(path);
				Fill(e, bp);
			}
			catch (Exception ex) {
				e.Failed = true;
				ModContentLogger.Warn("Could not build preview for " + path, ex);
			}
			_cache[path] = e;
			return e;
		}

		private static Entry _projection;
		private static Blueprint _projectionSource;
		private static int _projectionVersion = -1;

		/// <summary>Previa da projecao ativa (ja girada/espelhada); refeita quando ela muda.</summary>
		public static Entry ForProjection(Blueprint bp, int version)
		{
			if (bp == null)
				return null;
			if (_projection != null && ReferenceEquals(bp, _projectionSource) && version == _projectionVersion)
				return _projection;
			_projection?.Texture?.Dispose();
			_projection = new Entry();
			_projectionSource = bp;
			_projectionVersion = version;
			try {
				Fill(_projection, bp);
			}
			catch (Exception ex) {
				_projection.Failed = true;
				ModContentLogger.Warn("Could not build projection preview", ex);
			}
			return _projection;
		}

		private static void Fill(Entry e, Blueprint bp)
		{
			e.Width = bp.Width;
			e.Height = bp.Height;
			int tiles = 0;
			foreach (var c in bp.Cells) {
				if (c.HasTile)
					tiles++;
			}
			e.Tiles = tiles;
			e.Texture = Build(bp);
		}

		/// <summary>Esquece arquivos que nao estao mais na lista.</summary>
		public static void Retain(ICollection<string> paths)
		{
			var gone = new List<string>();
			foreach (var kv in _cache) {
				if (!paths.Contains(kv.Key))
					gone.Add(kv.Key);
			}
			foreach (string p in gone) {
				_cache[p].Texture?.Dispose();
				_cache.Remove(p);
			}
		}

		public static void Clear()
		{
			foreach (var e in _cache.Values)
				e.Texture?.Dispose();
			_cache.Clear();
			_projection?.Texture?.Dispose();
			_projection = null;
			_projectionSource = null;
		}

		private static Texture2D Build(Blueprint bp)
		{
			if (bp.Width <= 0 || bp.Height <= 0)
				return null;
			float scale = Math.Min(MaxPixelsPerTile, (float)MaxSide / Math.Max(bp.Width, bp.Height));
			int tw = Math.Max(1, (int)(bp.Width * scale));
			int th = Math.Max(1, (int)(bp.Height * scale));
			var data = new Color[tw * th];
			for (int py = 0; py < th; py++) {
				int cy = Math.Min(bp.Height - 1, (int)(py / scale));
				for (int px = 0; px < tw; px++) {
					int cx = Math.Min(bp.Width - 1, (int)(px / scale));
					data[py * tw + px] = CellColor(in bp[cx, cy]);
				}
			}
			var tex = new Texture2D(Main.instance.GraphicsDevice, tw, th);
			tex.SetData(data);
			return tex;
		}

		internal static Color CellColor(in BlueprintCell c)
		{
			if (c.HasTile && c.TileType < MapHelper.tileLookup.Length && MapHelper.tileLookup[c.TileType] != 0)
				return MapColor(MapHelper.tileLookup[c.TileType], c.TilePaint);
			if (c.LiquidAmount > 0) {
				Color liquid = c.LiquidType switch {
					1 => new Color(253, 90, 3),
					2 => new Color(254, 194, 20),
					3 => new Color(200, 160, 240),
					_ => new Color(30, 90, 220),
				};
				return liquid * 0.85f;
			}
			if (c.WallType > 0 && c.WallType < MapHelper.wallLookup.Length && MapHelper.wallLookup[c.WallType] != 0)
				return MapColor(MapHelper.wallLookup[c.WallType], c.WallPaint);
			if (c.HasTile)
				return new Color(150, 150, 150);
			if (c.WallType > 0)
				return new Color(70, 70, 70);
			return Color.Transparent;
		}

		private static Color MapColor(ushort lookup, byte paint)
		{
			var tile = MapTile.Create(lookup, 255, paint);
			return MapHelper.GetMapTileXnaColor(ref tile);
		}

		/// <summary>Desenha a previa centralizada dentro de um retangulo, sem distorcer.</summary>
		public static void Draw(SpriteBatch sb, Entry e, Rectangle box, float opacity = 1f)
		{
			if (e?.Texture == null)
				return;
			float s = Math.Min((float)box.Width / e.Texture.Width, (float)box.Height / e.Texture.Height);
			int w = Math.Max(1, (int)(e.Texture.Width * s));
			int h = Math.Max(1, (int)(e.Texture.Height * s));
			var dest = new Rectangle(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
			sb.Draw(e.Texture, dest, Color.White * opacity);
		}
	}

	internal static class ModContentLogger
	{
		public static void Warn(string msg, Exception e) =>
			Terraria.ModLoader.ModContent.GetInstance<Terraprint>().Logger.Warn(msg, e);
	}
}
