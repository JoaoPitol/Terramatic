using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Terraprint.Common.Blueprints
{
	/// <summary>
	/// Uma construcao salva: uma grade Width x Height de celulas.
	/// No arquivo, os tipos de bloco e parede sao gravados por nome (ex.: "Terraria/Dirt", "OutroMod/Tijolo"),
	/// assim o blueprint continua valido mesmo se os IDs numericos mudarem entre versoes ou mods.
	/// </summary>
	public class Blueprint
	{
		public const int FormatVersion = 1;
		public const string Extension = ".tprint";

		public string Name = "";
		public string Author = "";
		public DateTime CreatedUtc = DateTime.UtcNow;
		public int Width { get; private set; }
		public int Height { get; private set; }
		public BlueprintCell[] Cells { get; private set; }

		/// <summary>Quantos tipos de bloco/parede do arquivo nao existem neste jogo (mod ausente).</summary>
		public int UnknownTypes { get; private set; }

		public Blueprint(int width, int height)
		{
			Width = width;
			Height = height;
			Cells = new BlueprintCell[width * height];
		}

		public ref BlueprintCell this[int x, int y] => ref Cells[y * Width + x];

		public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

		/// <summary>Copia uma area retangular do mundo (coordenadas em tiles, inclusivas).</summary>
		public static Blueprint Capture(int left, int top, int right, int bottom, bool includeLiquids = true, bool includePaint = true)
		{
			var bp = new Blueprint(right - left + 1, bottom - top + 1);
			for (int y = 0; y < bp.Height; y++) {
				for (int x = 0; x < bp.Width; x++) {
					int wx = left + x, wy = top + y;
					if (!WorldGen.InWorld(wx, wy))
						continue;

					Tile t = Main.tile[wx, wy];
					ref BlueprintCell c = ref bp[x, y];
					if (t.HasTile) {
						c.HasTile = true;
						c.TileType = t.TileType;
						c.FrameX = t.TileFrameX;
						c.FrameY = t.TileFrameY;
						c.Slope = (byte)t.Slope;
						c.HalfBlock = t.IsHalfBlock;
						c.TilePaint = includePaint ? t.TileColor : (byte)0;
					}
					c.WallType = t.WallType;
					c.WallPaint = includePaint && t.WallType != WallID.None ? t.WallColor : (byte)0;
					if (includeLiquids && t.LiquidAmount > 0) {
						c.LiquidAmount = t.LiquidAmount;
						c.LiquidType = (byte)t.LiquidType;
					}
				}
			}
			return bp;
		}

		public int CountTiles()
		{
			int n = 0;
			foreach (var c in Cells)
				if (c.HasTile) n++;
			return n;
		}

		public int CountWalls()
		{
			int n = 0;
			foreach (var c in Cells)
				if (c.WallType != 0) n++;
			return n;
		}

		#region Nomes de tipos (vanilla e modded)

		public static string TileKey(int type)
		{
			if (type < TileID.Count)
				return "Terraria/" + TileID.Search.GetName(type);
			return TileLoader.GetTile(type)?.FullName ?? "Unknown/Tile" + type;
		}

		public static string WallKey(int type)
		{
			if (type < WallID.Count)
				return "Terraria/" + WallID.Search.GetName(type);
			return WallLoader.GetWall(type)?.FullName ?? "Unknown/Wall" + type;
		}

		public static bool TryResolveTile(string key, out int type)
		{
			type = -1;
			if (key.StartsWith("Terraria/"))
				return TileID.Search.TryGetId(key.Substring("Terraria/".Length), out type);
			if (ModContent.TryFind(key, out ModTile modTile)) {
				type = modTile.Type;
				return true;
			}
			return false;
		}

		public static bool TryResolveWall(string key, out int type)
		{
			type = -1;
			if (key.StartsWith("Terraria/"))
				return WallID.Search.TryGetId(key.Substring("Terraria/".Length), out type);
			if (ModContent.TryFind(key, out ModWall modWall)) {
				type = modWall.Type;
				return true;
			}
			return false;
		}

		#endregion

		#region Serializacao

		public TagCompound Save()
		{
			// Paleta: indice local -> nome do tipo. 0 fica reservado para "nenhum".
			var tileKeys = new List<string>();
			var wallKeys = new List<string>();
			var tileIndex = new Dictionary<int, int>();
			var wallIndex = new Dictionary<int, int>();

			using var ms = new MemoryStream();
			using (var w = new BinaryWriter(ms)) {
				foreach (var c in Cells) {
					int ti = 0, wi = 0;
					if (c.HasTile) {
						if (!tileIndex.TryGetValue(c.TileType, out ti)) {
							tileKeys.Add(TileKey(c.TileType));
							ti = tileKeys.Count;
							tileIndex[c.TileType] = ti;
						}
					}
					if (c.WallType != 0) {
						if (!wallIndex.TryGetValue(c.WallType, out wi)) {
							wallKeys.Add(WallKey(c.WallType));
							wi = wallKeys.Count;
							wallIndex[c.WallType] = wi;
						}
					}

					byte flags = 0;
					if (c.HalfBlock) flags |= 1;
					w.Write((ushort)ti);
					w.Write(c.FrameX);
					w.Write(c.FrameY);
					w.Write(c.Slope);
					w.Write(flags);
					w.Write(c.TilePaint);
					w.Write((ushort)wi);
					w.Write(c.WallPaint);
					w.Write(c.LiquidAmount);
					w.Write(c.LiquidType);
				}
			}

			return new TagCompound {
				["version"] = FormatVersion,
				["name"] = Name,
				["author"] = Author,
				["created"] = CreatedUtc.ToBinary(),
				["width"] = Width,
				["height"] = Height,
				["tileKeys"] = tileKeys,
				["wallKeys"] = wallKeys,
				["cells"] = ms.ToArray(),
			};
		}

		public static Blueprint Load(TagCompound tag)
		{
			int version = tag.GetInt("version");
			if (version > FormatVersion)
				throw new InvalidDataException($"Blueprint format v{version} is newer than this Terraprint (v{FormatVersion}).");

			var bp = new Blueprint(tag.GetInt("width"), tag.GetInt("height")) {
				Name = tag.GetString("name"),
				Author = tag.GetString("author"),
				CreatedUtc = tag.ContainsKey("created") ? DateTime.FromBinary(tag.GetLong("created")) : DateTime.UtcNow,
			};

			var tileKeys = tag.GetList<string>("tileKeys");
			var wallKeys = tag.GetList<string>("wallKeys");
			var tileMap = new int[tileKeys.Count + 1];
			var wallMap = new int[wallKeys.Count + 1];
			int unknown = 0;
			for (int i = 0; i < tileKeys.Count; i++) {
				if (!TryResolveTile(tileKeys[i], out tileMap[i + 1])) { tileMap[i + 1] = -1; unknown++; }
			}
			for (int i = 0; i < wallKeys.Count; i++) {
				if (!TryResolveWall(wallKeys[i], out wallMap[i + 1])) { wallMap[i + 1] = -1; unknown++; }
			}
			bp.UnknownTypes = unknown;

			byte[] data = tag.GetByteArray("cells");
			using var r = new BinaryReader(new MemoryStream(data));
			for (int i = 0; i < bp.Cells.Length; i++) {
				ref BlueprintCell c = ref bp.Cells[i];
				int ti = r.ReadUInt16();
				c.FrameX = r.ReadInt16();
				c.FrameY = r.ReadInt16();
				c.Slope = r.ReadByte();
				byte flags = r.ReadByte();
				c.TilePaint = r.ReadByte();
				int wi = r.ReadUInt16();
				c.WallPaint = r.ReadByte();
				c.LiquidAmount = r.ReadByte();
				c.LiquidType = r.ReadByte();
				c.HalfBlock = (flags & 1) != 0;

				if (ti > 0 && ti < tileMap.Length && tileMap[ti] >= 0) {
					c.HasTile = true;
					c.TileType = (ushort)tileMap[ti];
				}
				if (wi > 0 && wi < wallMap.Length && wallMap[wi] >= 0)
					c.WallType = (ushort)wallMap[wi];
			}
			return bp;
		}

		#endregion
	}
}
