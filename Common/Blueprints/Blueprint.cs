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
		/// <summary>2: atuadores e blocos desativados (bits 1 e 2 de flags).</summary>
		public const int FormatVersion = 2;
		public const string Extension = ".tprint";

		public string Name = "";
		public string Author = "";
		public DateTime CreatedUtc = DateTime.UtcNow;
		public int Width { get; private set; }
		public int Height { get; private set; }
		public BlueprintCell[] Cells { get; private set; }

		/// <summary>Quantos tipos de bloco/parede do arquivo nao existem neste jogo (mod ausente).</summary>
		public int UnknownTypes => MissingKeys.Count;

		/// <summary>
		/// Nomes ("Mod/Tipo") dos blocos e paredes que vieram de mods que nao estao ativos. As celulas apontam
		/// para esta lista (BlueprintCell.MissingTile/MissingWall), entao eles voltam para o arquivo ao salvar.
		/// </summary>
		public List<string> MissingKeys = new();

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
						c.Inactive = t.IsActuated;
						c.HasActuator = t.HasActuator;
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

		public Blueprint Clone()
		{
			var bp = new Blueprint(Width, Height) { Name = Name, Author = Author, CreatedUtc = CreatedUtc, MissingKeys = new List<string>(MissingKeys) };
			Array.Copy(Cells, bp.Cells, Cells.Length);
			return bp;
		}

		/// <summary>
		/// Copia com bordas diferentes: valores positivos acrescentam linhas/colunas vazias, negativos cortam.
		/// </summary>
		public Blueprint Resized(int left, int top, int right, int bottom)
		{
			int w = Math.Max(1, Width + left + right);
			int h = Math.Max(1, Height + top + bottom);
			var bp = new Blueprint(w, h) { Name = Name, Author = Author, CreatedUtc = CreatedUtc, MissingKeys = new List<string>(MissingKeys) };
			for (int y = 0; y < h; y++) {
				int sy = y - top;
				if (sy < 0 || sy >= Height)
					continue;
				for (int x = 0; x < w; x++) {
					int sx = x - left;
					if (sx >= 0 && sx < Width)
						bp.Cells[y * w + x] = Cells[sy * Width + sx];
				}
			}
			return bp;
		}

		/// <summary>Menor retangulo com alguma coisa dentro (ou o blueprint todo, se estiver vazio).</summary>
		public Microsoft.Xna.Framework.Rectangle ContentBounds()
		{
			int minX = Width, minY = Height, maxX = -1, maxY = -1;
			for (int y = 0; y < Height; y++) {
				for (int x = 0; x < Width; x++) {
					ref BlueprintCell c = ref Cells[y * Width + x];
					if (c.IsEmpty && !HasMissing(c))
						continue;
					if (x < minX) minX = x;
					if (x > maxX) maxX = x;
					if (y < minY) minY = y;
					if (y > maxY) maxY = y;
				}
			}
			if (maxX < 0)
				return new Microsoft.Xna.Framework.Rectangle(0, 0, Width, Height);
			return new Microsoft.Xna.Framework.Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
		}

		public int CountTiles()
		{
			int n = 0;
			foreach (var c in Cells)
				if (c.HasTile) n++;
			return n;
		}

		/// <summary>True quando a celula guarda um bloco ou parede de um mod que nao esta ativo.</summary>
		public static bool HasMissing(in BlueprintCell c) => c.MissingTile > 0 && !c.HasTile || c.MissingWall > 0 && c.WallType == 0;

		/// <summary>
		/// Mods cujos blocos ou paredes o blueprint usa (nome interno, sem o Terraria), e quais deles nao estao ativos.
		/// </summary>
		public void GetMods(out List<string> used, out List<string> missing)
		{
			var usedSet = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
			var missingSet = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
			var seenTiles = new HashSet<int>();
			var seenWalls = new HashSet<int>();
			foreach (var c in Cells) {
				if (c.HasTile && c.TileType >= TileID.Count && seenTiles.Add(c.TileType) && TileLoader.GetTile(c.TileType) is ModTile mt)
					usedSet.Add(mt.Mod.Name);
				if (c.WallType >= WallID.Count && seenWalls.Add(c.WallType) && WallLoader.GetWall(c.WallType) is ModWall mw)
					usedSet.Add(mw.Mod.Name);
			}
			foreach (string key in MissingKeys) {
				string mod = ModOfKey(key);
				usedSet.Add(mod);
				missingSet.Add(mod);
			}
			used = new List<string>(usedSet);
			missing = new List<string>(missingSet);
		}

		/// <summary>"CalamityMod/Tijolo" -> "CalamityMod".</summary>
		public static string ModOfKey(string key)
		{
			int slash = key.IndexOf('/');
			return slash > 0 ? key.Substring(0, slash) : key;
		}

		/// <summary>Nome do mod para mostrar (o de exibicao, se o mod estiver ativo).</summary>
		public static string ModDisplayName(string name)
		{
			return ModLoader.TryGetMod(name, out Mod mod) ? mod.DisplayNameClean : name;
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
			// Blocos de mods ausentes entram na paleta com o nome original, igual aos outros.
			var tileKeys = new List<string>();
			var wallKeys = new List<string>();
			var tileIndex = new Dictionary<string, int>();
			var wallIndex = new Dictionary<string, int>();
			int Index(string key, List<string> keys, Dictionary<string, int> index)
			{
				if (!index.TryGetValue(key, out int i)) {
					keys.Add(key);
					i = keys.Count;
					index[key] = i;
				}
				return i;
			}
			string Missing(ushort i) => i > 0 && i <= MissingKeys.Count ? MissingKeys[i - 1] : null;

			using var ms = new MemoryStream();
			using (var w = new BinaryWriter(ms)) {
				foreach (var c in Cells) {
					int ti = 0, wi = 0;
					string tileKey = c.HasTile ? TileKey(c.TileType) : Missing(c.MissingTile);
					string wallKey = c.WallType != 0 ? WallKey(c.WallType) : Missing(c.MissingWall);
					if (tileKey != null)
						ti = Index(tileKey, tileKeys, tileIndex);
					if (wallKey != null)
						wi = Index(wallKey, wallKeys, wallIndex);

					byte flags = 0;
					if (c.HalfBlock) flags |= 1;
					if (c.HasActuator) flags |= 2;
					if (c.Inactive && tileKey != null) flags |= 4;
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
			// Tipo resolvido (>= 0) ou, para mods ausentes, -(indice em MissingKeys).
			var tileMap = new int[tileKeys.Count + 1];
			var wallMap = new int[wallKeys.Count + 1];
			for (int i = 0; i < tileKeys.Count; i++) {
				if (!TryResolveTile(tileKeys[i], out tileMap[i + 1]))
					tileMap[i + 1] = -bp.AddMissing(tileKeys[i]);
			}
			for (int i = 0; i < wallKeys.Count; i++) {
				if (!TryResolveWall(wallKeys[i], out wallMap[i + 1]))
					wallMap[i + 1] = -bp.AddMissing(wallKeys[i]);
			}

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
				c.HasActuator = (flags & 2) != 0;

				if (ti > 0 && ti < tileMap.Length) {
					c.Inactive = (flags & 4) != 0;
					if (tileMap[ti] >= 0) {
						c.HasTile = true;
						c.TileType = (ushort)tileMap[ti];
					}
					else
						c.MissingTile = (ushort)-tileMap[ti];
				}
				if (wi > 0 && wi < wallMap.Length) {
					if (wallMap[wi] >= 0)
						c.WallType = (ushort)wallMap[wi];
					else
						c.MissingWall = (ushort)-wallMap[wi];
				}
			}
			return bp;
		}

		private int AddMissing(string key)
		{
			int i = MissingKeys.IndexOf(key);
			if (i < 0) {
				MissingKeys.Add(key);
				i = MissingKeys.Count - 1;
			}
			return i + 1;
		}

		#endregion
	}
}
