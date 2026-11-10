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

		/// <summary>
		/// No multiplayer o servidor expulsa quem coloca blocos rapido demais (acima de ~18 por segundo,
		/// quando o anti-spam esta ligado). Ali o ritmo fica em 1 bloco a cada 4 ticks (15 por segundo).
		/// </summary>
		private const float MultiplayerBlocksPerTick = 0.25f;
		private static float _mpBudget;

		public static bool Holding => Main.GameUpdateCount <= _heldUntil;

		// --- Construir tudo (um clique, sem limite de alcance) ---

		/// <summary>Blocos por tick no "Construir tudo" em single player (espalha o trabalho para nao travar o jogo).</summary>
		private const int BuildAllPerTick = 60;
		/// <summary>Tentativas por tick no "Construir tudo".</summary>
		private const int BuildAllAttemptsPerTick = 600;

		private static bool _buildAll;
		private static bool _buildAllRetried;
		private static int _buildAllPlaced;

		/// <summary>O "Construir tudo" esta rodando.</summary>
		public static bool BuildingAll => _buildAll;

		/// <summary>Liga ou para o "Construir tudo".</summary>
		public static void ToggleBuildAll()
		{
			if (_buildAll) {
				FinishBuildAll(stopped: true);
				return;
			}
			if (ProjectionSystem.State != ProjectionSystem.ProjectionState.Fixed) {
				Lang.Info(Lang.UI("AutoBuildNoProjection"), Color.Orange);
				return;
			}
			_buildAll = true;
			_buildAllRetried = false;
			_buildAllPlaced = 0;
			_failed.Clear();
			Lang.Info(Lang.UI("BuildAllStarted"));
		}

		private static void FinishBuildAll(bool stopped)
		{
			_buildAll = false;
			_failed.Clear();
			ProgressTracker.Recalculate();
			if (stopped)
				Lang.Info(Lang.UI("BuildAllStopped", _buildAllPlaced), Color.Orange);
			else if (ProgressTracker.Missing > 0 || ProgressTracker.Wrong > 0)
				Lang.Info(Lang.UI("BuildAllPartial", _buildAllPlaced, ProgressTracker.Missing, ProgressTracker.Wrong), Color.Orange);
			else
				Lang.Info(Lang.UI("BuildAllDone", _buildAllPlaced), Color.LightGreen);
		}

		/// <summary>Um tick do "Construir tudo": coloca um lote em qualquer parte do blueprint.</summary>
		private static void UpdateBuildAll()
		{
			Player player = Main.LocalPlayer;
			if (ProjectionSystem.State != ProjectionSystem.ProjectionState.Fixed || player == null || !player.active || player.dead || player.noBuilding) {
				FinishBuildAll(stopped: true);
				return;
			}

			int max = BuildAllPerTick;
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				_mpBudget = System.Math.Min(1f, _mpBudget + MultiplayerBlocksPerTick);
				max = (int)_mpBudget;
				if (max == 0)
					return;
			}
			int placed = PlaceBatch(player, max, ProjectionSystem.Area, BuildAllAttemptsPerTick, out int pending);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				_mpBudget -= placed;
			if (placed > 0) {
				_buildAllPlaced += placed;
				_buildAllRetried = false;
				SoundEngine.PlaySound(SoundID.Dig, player.Center);
				ProgressTracker.RequestRefresh();
			}
			if (pending > 0)
				return;
			// Acabaram as posicoes que ainda nao falharam. Tenta as que falharam mais uma vez (moveis que
			// dependiam de um bloco colocado depois, por exemplo); se nada mudar, terminou.
			if (!_buildAllRetried && _failed.Count > 0) {
				_failed.Clear();
				_buildAllRetried = true;
				return;
			}
			FinishBuildAll(stopped: false);
		}

		/// <summary>Chamado a cada tick enquanto o botao do menu ou a tecla estao pressionados.</summary>
		public static void RequestHold() => _heldUntil = Main.GameUpdateCount + 1;

		public override void ClearWorld()
		{
			_buildAll = false;
			_heldUntil = 0;
			_wasHeld = false;
			_failed.Clear();
		}

		public override void PostUpdateEverything()
		{
			if (_buildAll) {
				_wasHeld = false;
				UpdateBuildAll();
				return;
			}
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

			int max = TerraprintConfig.Instance.AutoBuildPerTick;
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				_mpBudget = System.Math.Min(1f, _mpBudget + MultiplayerBlocksPerTick);
				max = (int)_mpBudget;
				if (max == 0)
					return;
			}
			int placed = PlaceBatch(player, max, Reach(player), MaxAttemptsPerTick, out _);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				_mpBudget -= placed;
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

		/// <summary>
		/// Coloca ate <paramref name="max"/> blocos/paredes do blueprint dentro de <paramref name="region"/>
		/// (o alcance do jogador, ou o blueprint inteiro no "Construir tudo"). <paramref name="pending"/> diz
		/// quantas posicoes ainda dava para tentar neste tick.
		/// </summary>
		private static int PlaceBatch(Player player, int max, Rectangle region, int maxAttempts, out int pending)
		{
			pending = 0;
			var bp = ProjectionSystem.Current;
			var origin = ProjectionSystem.Origin;
			var cfg = TerraprintConfig.Instance;
			Rectangle area = Rectangle.Intersect(region, ProjectionSystem.Area);
			if (area.Width <= 0 || area.Height <= 0)
				return 0;

			Vector2 center = player.Center / 16f;
			var candidates = new List<Candidate>();
			for (int wy = area.Top; wy < area.Bottom; wy++) {
				for (int wx = area.Left; wx < area.Right; wx++) {
					// No multiplayer so mexe em partes do mundo que o servidor ja enviou.
					if (!WorldGen.InWorld(wx, wy, 10) || Main.netMode == NetmodeID.MultiplayerClient && !Main.sectionManager.TileLoaded(wx, wy))
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
			pending = candidates.Count;
			if (candidates.Count == 0)
				return 0;

			candidates.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : a.Distance.CompareTo(b.Distance));

			int placed = 0, attempts = 0;
			foreach (var cand in candidates) {
				if (placed >= max || attempts >= maxAttempts)
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
			// Paredes naturais (ex.: terra das cavernas) saem na versao que o item coloca.
			int wall = MaterialLookup.PlaceableWall(c.WallType);
			WorldGen.PlaceWall(x, y, wall, mute: true);
			if (Main.tile[x, y].WallType != wall)
				return false;
			Consume(player, slot, x, y);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 3, x, y, wall);
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

			// Nao prende o jogador: sem NoClip, deixa uma folga de 1 bloco dos lados e em cima dele
			// (embaixo pode, e o chao). Esses blocos sao colocados depois, quando o jogador se afastar.
			if (Main.tileSolid[c.TileType] && !Main.tileSolidTop[c.TileType] && !Players.TerraprintPlayer.NoClip) {
				Rectangle hb = player.Hitbox;
				var keepClear = new Rectangle(hb.X - 16, hb.Y - 16, hb.Width + 32, hb.Height + 16);
				if (keepClear.Intersects(new Rectangle(x * 16, y * 16, w * 16, h * 16)))
					return false;
			}

			bool ok = hasObject && TileObjectData.CustomPlace(c.TileType, style)
				? PlaceObject(player, c, x, y, style, data)
				: PlaceSimple(player, c, x, y, style, cfg);
			if (!ok)
				return false;

			Consume(player, slot, x, y);
			return true;
		}

		/// <summary>Blocos, plataformas, tochas e outros de 1 tile que o jogo coloca com PlaceTile.</summary>
		private static bool PlaceSimple(Player player, in BlueprintCell c, int x, int y, int style, TerraprintConfig cfg)
		{
			WorldGen.PlaceTile(x, y, c.TileType, mute: true, forced: false, plr: player.whoAmI, style: style);
			Tile t = Main.tile[x, y];
			if (!t.HasTile || t.TileType != c.TileType)
				return false;
			bool mp = Main.netMode == NetmodeID.MultiplayerClient;
			// Mesmas mensagens que o jogo manda quando o jogador coloca um bloco e usa o martelo.
			if (mp)
				NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 1, x, y, c.TileType, style);

			// Atuador, se o blueprint tiver e o jogador tiver atuadores no inventario.
			if (c.HasActuator && !t.HasActuator) {
				int act = FindItem(player, ItemID.Actuator);
				if (act >= 0 && WorldGen.PlaceActuator(x, y)) {
					Consume(player, act, x, y);
					if (mp)
						NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 8, x, y);
				}
			}

			if (cfg.AutoBuildSlopes && Main.tileSolid[c.TileType] && !BlueprintTransform.IsMultitile(c)) {
				if (c.HalfBlock) {
					WorldGen.PoundTile(x, y);
					if (mp)
						NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 7, x, y);
				}
				else if (c.Slope != 0) {
					WorldGen.SlopeTile(x, y, c.Slope, noEffects: true);
					if (mp)
						NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 14, x, y, c.Slope);
				}
			}
			return true;
		}

		/// <summary>
		/// Moveis e objetos: coloca pelo canto superior esquerdo, virado para o mesmo lado do blueprint,
		/// do mesmo jeito que o jogo faz quando o jogador coloca o item.
		/// </summary>
		private static bool PlaceObject(Player player, in BlueprintCell c, int x, int y, int style, TileObjectData data)
		{
			int cx = x + data.Origin.X, cy = y + data.Origin.Y;
			int wanted = data.Direction switch {
				Terraria.Enums.TileObjectDirection.PlaceLeft => -1,
				Terraria.Enums.TileObjectDirection.PlaceRight => 1,
				_ => player.direction,
			};
			foreach (int dir in new[] { wanted, -wanted }) {
				if (!TileObject.CanPlace(cx, cy, c.TileType, style, dir, out TileObject obj) || obj.xCoord != x || obj.yCoord != y)
					continue;
				if (!TileObject.Place(obj))
					continue;
				WorldGen.SquareTileFrame(cx, cy);
				TileObjectData.CallPostPlacementPlayerHook(cx, cy, c.TileType, style, dir, obj.alternate, obj);
				if (Main.netMode == NetmodeID.MultiplayerClient) {
					// Bau, caixa e afins ja avisam o servidor pelo gancho acima; o sensor logico tem mensagem propria (igual ao jogo).
					if (!Main.tileContainer[c.TileType] && c.TileType != TileID.LogicSensor)
						NetMessage.SendObjectPlacement(-1, cx, cy, obj.type, obj.style, obj.alternate, obj.random, dir);
				}
				else if (data.Width > 1 || data.Height > 1)
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
