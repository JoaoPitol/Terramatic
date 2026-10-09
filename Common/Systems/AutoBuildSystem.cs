using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraprint.Common.Blueprints;
using Terraprint.Common.Configs;

namespace Terraprint.Common.Systems
{
	/// <summary>
	/// Construcao automatica: enquanto o botao (ou a tecla) fica pressionado, coloca alguns blocos por tick
	/// do blueprint fixado que estejam ao alcance normal do jogador, gastando os itens do inventario.
	/// Pula posicoes ja ocupadas e materiais que voce nao tem.
	/// </summary>
	[Autoload(Side = ModSide.Client)]
	public class AutoBuildSystem : ModSystem
	{
		/// <summary>Inventario principal, moedas e municao.</summary>
		private const int InventorySlots = 58;
		/// <summary>Limite de tentativas por tick, para nao pesar com muitas posicoes impossiveis.</summary>
		private const int MaxAttemptsPerTick = 40;
		/// <summary>Posicoes que falharam sao tentadas de novo depois deste tempo (voce pode ter pego itens ou mudado algo).</summary>
		private const int RetryTicks = 60;

		private static uint _heldUntil;
		private static bool _wasHeld;
		private static bool _placedThisHold;
		private static int _retryTimer;
		private static readonly HashSet<long> _failed = new();

		public static bool Holding => Main.GameUpdateCount <= _heldUntil;

		/// <summary>Chamado a cada tick enquanto o botao do menu ou a tecla estao pressionados.</summary>
		public static void RequestHold() => _heldUntil = Main.GameUpdateCount + 1;

		public override void ClearWorld()
		{
			_heldUntil = 0;
			_wasHeld = false;
			_failed.Clear();
		}

		public override void PostUpdateEverything()
		{
			bool held = Holding;
			bool justPressed = held && !_wasHeld;
			bool justReleased = !held && _wasHeld;
			_wasHeld = held;

			if (justReleased && !_placedThisHold)
				Lang.Info(Lang.UI(ProjectionSystem.State == ProjectionSystem.ProjectionState.Fixed ? "AutoBuildNothing" : "AutoBuildNoProjection"), Color.Orange);
			if (!held) {
				_failed.Clear();
				return;
			}
			if (justPressed) {
				_placedThisHold = false;
				_retryTimer = 0;
			}
			if (++_retryTimer >= RetryTicks) {
				_retryTimer = 0;
				_failed.Clear();
			}

			Player player = Main.LocalPlayer;
			if (ProjectionSystem.State != ProjectionSystem.ProjectionState.Fixed || player == null || !player.active || player.dead || player.noBuilding)
				return;

			int placed = PlaceBatch(player, TerraprintConfig.Instance.AutoBuildPerTick);
			if (placed > 0) {
				_placedThisHold = true;
				SoundEngine.PlaySound(SoundID.Dig, player.Center);
				ProgressTracker.RequestRefresh();
			}
		}

		private struct Candidate
		{
			public int Index;
			public bool Wall;
			public int Priority;
			public float Distance;
		}

		/// <summary>Area (em tiles) onde o jogador alcanca colocar blocos, igual a conta do jogo.</summary>
		public static Rectangle Reach(Player p)
		{
			int range = p.blockRange;
			int left = (int)System.Math.Ceiling(p.position.X / 16f - Player.tileRangeX - range);
			int right = (int)((p.position.X + p.width) / 16f + Player.tileRangeX - 1f + range);
			int top = (int)System.Math.Ceiling(p.position.Y / 16f - Player.tileRangeY - range);
			int bottom = (int)((p.position.Y + p.height) / 16f + Player.tileRangeY - 2f + range);
			return new Rectangle(left, top, right - left + 1, bottom - top + 1);
		}

		private static int PlaceBatch(Player player, int max)
		{
			var bp = ProjectionSystem.Current;
			var origin = ProjectionSystem.Origin;
			var cfg = TerraprintConfig.Instance;
			Rectangle area = Rectangle.Intersect(Reach(player), ProjectionSystem.Area);
			if (area.Width <= 0 || area.Height <= 0)
				return 0;

			Vector2 center = player.Center / 16f;
			var candidates = new List<Candidate>();
			for (int wy = area.Top; wy < area.Bottom; wy++) {
				for (int wx = area.Left; wx < area.Right; wx++) {
					if (!WorldGen.InWorld(wx, wy, 10))
						continue;
					int i = (wy - origin.Y) * bp.Width + (wx - origin.X);
					ref BlueprintCell c = ref bp.Cells[i];
					Tile t = Main.tile[wx, wy];
					float dist = Vector2.DistanceSquared(center, new Vector2(wx + 0.5f, wy + 0.5f));

					if (c.HasTile && !t.HasTile && MaterialLookup.CountsAsItem(c) && !_failed.Contains(Key(i, false))) {
						bool isObject = MaterialLookup.TryGetObjectCell(c, out var data, out _, out _) && (data.Width > 1 || data.Height > 1);
						if (!isObject || cfg.AutoBuildFurniture) {
							// Blocos solidos primeiro (sao o apoio do resto), depois paredes, depois moveis e decoracao.
							int priority = Main.tileSolid[c.TileType] && !Main.tileSolidTop[c.TileType] ? 0 : 2;
							candidates.Add(new Candidate { Index = i, Wall = false, Priority = priority, Distance = dist });
						}
					}
					if (cfg.AutoBuildWalls && c.WallType != WallID.None && t.WallType == WallID.None && !_failed.Contains(Key(i, true)))
						candidates.Add(new Candidate { Index = i, Wall = true, Priority = 1, Distance = dist });
				}
			}
			if (candidates.Count == 0)
				return 0;

			candidates.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : a.Distance.CompareTo(b.Distance));

			int placed = 0, attempts = 0;
			foreach (var cand in candidates) {
				if (placed >= max || attempts >= MaxAttemptsPerTick)
					break;
				attempts++;
				int x = origin.X + cand.Index % bp.Width;
				int y = origin.Y + cand.Index / bp.Width;
				bool ok = cand.Wall ? TryPlaceWall(player, bp.Cells[cand.Index], x, y) : TryPlaceTile(player, bp.Cells[cand.Index], x, y, cfg);
				if (ok)
					placed++;
				else
					_failed.Add(Key(cand.Index, cand.Wall));
			}
			return placed;
		}

		private static long Key(int index, bool wall) => (long)index * 2 + (wall ? 1 : 0);

		/// <summary>Primeiro slot do inventario com este item, ou -1.</summary>
		private static int FindItem(Player player, int itemType)
		{
			if (itemType <= ItemID.None)
				return -1;
			for (int i = 0; i < InventorySlots; i++) {
				Item it = player.inventory[i];
				if (it.type == itemType && it.stack > 0)
					return i;
			}
			return -1;
		}

		private static void Consume(Player player, int slot, int x, int y)
		{
			Item item = player.inventory[slot];
			TileLoader.PlaceInWorld(x, y, item);
			if (ItemLoader.ConsumeItem(item, player)) {
				item.stack--;
				if (item.stack <= 0)
					item.TurnToAir();
			}
		}

		private static bool TryPlaceWall(Player player, in BlueprintCell c, int x, int y)
		{
			int slot = FindItem(player, MaterialLookup.ItemForWall(c.WallType));
			if (slot < 0)
				return false;
			WorldGen.PlaceWall(x, y, c.WallType, mute: true);
			if (Main.tile[x, y].WallType != c.WallType)
				return false;
			Consume(player, slot, x, y);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				NetMessage.SendTileSquare(-1, x, y, 1, 1);
			return true;
		}

		private static bool TryPlaceTile(Player player, in BlueprintCell c, int x, int y, TerraprintConfig cfg)
		{
			int slot = FindItem(player, MaterialLookup.ItemForTile(c));
			if (slot < 0)
				return false;

			MaterialLookup.GetStyle(c, out int style, out _);
			bool hasObject = MaterialLookup.TryGetObjectCell(c, out var data, out _, out _);
			int w = hasObject ? data.Width : 1;
			int h = hasObject ? data.Height : 1;

			// Nao prende o jogador dentro de um bloco solido.
			if (Main.tileSolid[c.TileType] && !Main.tileSolidTop[c.TileType]
				&& player.Hitbox.Intersects(new Rectangle(x * 16, y * 16, w * 16, h * 16)))
				return false;

			bool ok = hasObject && TileObjectData.CustomPlace(c.TileType, style)
				? PlaceObject(player, c, x, y, style, data)
				: PlaceSimple(player, c, x, y, style, cfg);
			if (!ok)
				return false;

			Consume(player, slot, x, y);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				NetMessage.SendTileSquare(-1, x, y, w, h);
			return true;
		}

		/// <summary>Blocos, plataformas, tochas e outros de 1 tile que o jogo coloca com PlaceTile.</summary>
		private static bool PlaceSimple(Player player, in BlueprintCell c, int x, int y, int style, TerraprintConfig cfg)
		{
			WorldGen.PlaceTile(x, y, c.TileType, mute: true, forced: false, plr: player.whoAmI, style: style);
			Tile t = Main.tile[x, y];
			if (!t.HasTile || t.TileType != c.TileType)
				return false;

			if (cfg.AutoBuildSlopes && Main.tileSolid[c.TileType] && !BlueprintTransform.IsMultitile(c)) {
				if (c.HalfBlock)
					t.IsHalfBlock = true;
				else if (c.Slope != 0)
					t.Slope = (SlopeType)c.Slope;
				WorldGen.SquareTileFrame(x, y);
			}
			return true;
		}

		/// <summary>Moveis e objetos: coloca pelo canto superior esquerdo e copia os quadros do blueprint (lado, estado).</summary>
		private static bool PlaceObject(Player player, in BlueprintCell c, int x, int y, int style, TileObjectData data)
		{
			int cx = x + data.Origin.X, cy = y + data.Origin.Y;
			foreach (int dir in new[] { player.direction, -player.direction }) {
				if (!TileObject.CanPlace(cx, cy, c.TileType, style, dir, out TileObject obj) || obj.xCoord != x || obj.yCoord != y)
					continue;
				if (!TileObject.Place(obj))
					continue;
				WorldGen.SquareTileFrame(cx, cy);
				TileObjectData.CallPostPlacementPlayerHook(cx, cy, c.TileType, style, dir, obj.alternate, obj);
				if (data.Width > 1 || data.Height > 1)
					CopyFrames(x, y, data.Width, data.Height, c.TileType);
				return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == c.TileType;
			}
			return false;
		}

		/// <summary>Deixa o objeto igual ao do blueprint (virado para o mesmo lado, porta aberta/fechada...).</summary>
		private static void CopyFrames(int x, int y, int w, int h, ushort type)
		{
			var bp = ProjectionSystem.Current;
			var o = ProjectionSystem.Origin;
			for (int dy = 0; dy < h; dy++) {
				for (int dx = 0; dx < w; dx++) {
					int bx = x + dx - o.X, by = y + dy - o.Y;
					if (bx < 0 || by < 0 || bx >= bp.Width || by >= bp.Height)
						return;
					ref BlueprintCell src = ref bp[bx, by];
					Tile t = Main.tile[x + dx, y + dy];
					if (!src.HasTile || src.TileType != type || !t.HasTile || t.TileType != type)
						return;
				}
			}
			for (int dy = 0; dy < h; dy++) {
				for (int dx = 0; dx < w; dx++) {
					ref BlueprintCell src = ref bp[x + dx - o.X, y + dy - o.Y];
					Tile t = Main.tile[x + dx, y + dy];
					t.TileFrameX = src.FrameX;
					t.TileFrameY = src.FrameY;
				}
			}
		}
	}
}
