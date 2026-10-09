using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraprint.Common.Blueprints;

namespace Terraprint.Common.Systems
{
	/// <summary>Um material da lista: o item, quantos o blueprint usa, quantos faltam colocar e quantos o jogador tem.</summary>
	public class MaterialEntry
	{
		public int ItemType;   // 0 = nenhum item coloca isso (ex.: grama, arvore)
		public string Name;
		public int Needed;
		public int Remaining;
		public int Have;

		public bool Done => Remaining == 0;
		public bool Lacking => Remaining > Have;
	}

	/// <summary>
	/// Lista de materiais da projecao ativa. Recalcula quando a projecao ou o progresso mudam,
	/// e confere o inventario de tempos em tempos (so enquanto alguem esta olhando a lista).
	/// </summary>
	[Autoload(Side = ModSide.Client)]
	public class MaterialsTracker : ModSystem
	{
		private const int InventoryRefreshTicks = 20;
		/// <summary>Inventario principal, moedas e municao (o slot 58 e o item no mouse).</summary>
		private const int InventorySlots = 58;

		public static List<MaterialEntry> Entries { get; private set; } = new();
		public static int TotalRemaining { get; private set; }
		public static int TotalLacking { get; private set; }

		/// <summary>Muda quando a lista muda de itens (a tela remonta as linhas).</summary>
		public static int ListVersion { get; private set; }

		private static int _seenVersion = -1;
		private static int _seenRevision = -1;
		private static int _timer;

		public override void Unload() => MaterialLookup.Unload();

		public override void ClearWorld()
		{
			Entries = new List<MaterialEntry>();
			_materials = new List<MaterialEntry>();
			_tileMat = _wallMat = System.Array.Empty<int>();
			_mapped = null;
			TotalRemaining = TotalLacking = 0;
			_seenVersion = _seenRevision = -1;
			ListVersion++;
		}

		/// <summary>Chamado pela tela de materiais a cada quadro.</summary>
		public static void Update()
		{
			if (_seenVersion != ProjectionSystem.Version || _seenRevision != ProgressTracker.Revision) {
				_seenVersion = ProjectionSystem.Version;
				_seenRevision = ProgressTracker.Revision;
				Rebuild();
				_timer = 0;
			}
			else if (++_timer >= InventoryRefreshTicks) {
				_timer = 0;
				CountInventory();
			}
		}

		// Para cada celula, o indice do material do bloco e da parede (-1 = nada). Refeito so quando a projecao muda.
		private static int[] _tileMat = System.Array.Empty<int>();
		private static int[] _wallMat = System.Array.Empty<int>();
		private static List<MaterialEntry> _materials = new();
		private static Blueprint _mapped;

		private static void MapCells()
		{
			_mapped = ProjectionSystem.HasProjection ? ProjectionSystem.Current : null;
			_materials = new List<MaterialEntry>();
			if (!ProjectionSystem.HasProjection) {
				_tileMat = _wallMat = System.Array.Empty<int>();
				return;
			}
			var bp = ProjectionSystem.Current;
			int n = bp.Width * bp.Height;
			_tileMat = new int[n];
			_wallMat = new int[n];
			var byKey = new Dictionary<string, int>();
			for (int i = 0; i < n; i++) {
				ref BlueprintCell c = ref bp.Cells[i];
				_tileMat[i] = _wallMat[i] = -1;
				if (MaterialLookup.CountsAsItem(c)) {
					int item = MaterialLookup.ItemForTile(c);
					_tileMat[i] = IndexOf(item, item > 0 ? null : MaterialLookup.TileName(c.TileType));
				}
				if (c.WallType != WallID.None) {
					int item = MaterialLookup.ItemForWall(c.WallType);
					_wallMat[i] = IndexOf(item, item > 0 ? null : MaterialLookup.WallName(c.WallType));
				}
			}

			int IndexOf(int item, string rawName)
			{
				string key = item > 0 ? "i" + item : rawName;
				if (!byKey.TryGetValue(key, out int index)) {
					index = _materials.Count;
					byKey[key] = index;
					_materials.Add(new MaterialEntry {
						ItemType = item,
						Name = item > 0 ? Terraria.Lang.GetItemNameValue(item) : rawName,
					});
				}
				return index;
			}
		}

		private static void Rebuild()
		{
			// Mover nao muda os materiais; so trocar, girar ou espelhar o blueprint.
			if (_mapped != (ProjectionSystem.HasProjection ? ProjectionSystem.Current : null))
				MapCells();

			foreach (var m in _materials)
				m.Needed = m.Remaining = 0;
			int n = _tileMat.Length;
			// Enquanto o fantasma segue o mouse nao ha progresso: tudo conta como faltando.
			bool useStatus = ProjectionSystem.State == ProjectionSystem.ProjectionState.Fixed && ProgressTracker.TileStatus.Length == n;
			for (int i = 0; i < n; i++) {
				if (_tileMat[i] >= 0) {
					var m = _materials[_tileMat[i]];
					m.Needed++;
					if (!useStatus || ProgressTracker.TileStatus[i] != CellStatus.Correct)
						m.Remaining++;
				}
				if (_wallMat[i] >= 0) {
					var m = _materials[_wallMat[i]];
					m.Needed++;
					if (!useStatus || ProgressTracker.WallStatus[i] != CellStatus.Correct)
						m.Remaining++;
				}
			}

			var list = new List<MaterialEntry>(_materials);
			list.Sort((a, b) => {
				if (a.Done != b.Done)
					return a.Done ? 1 : -1;
				int r = b.Remaining.CompareTo(a.Remaining);
				return r != 0 ? r : string.Compare(a.Name, b.Name, System.StringComparison.CurrentCulture);
			});

			bool changed = list.Count != Entries.Count;
			for (int i = 0; !changed && i < list.Count; i++)
				changed = list[i] != Entries[i];
			Entries = list;
			CountInventory();
			if (changed)
				ListVersion++;
		}

		/// <summary>Quantos itens deste tipo o jogador tem no inventario.</summary>
		public static int CountInInventory(Player player, int itemType)
		{
			int count = 0;
			for (int i = 0; i < InventorySlots; i++) {
				Item it = player.inventory[i];
				if (it.type == itemType && it.stack > 0)
					count += it.stack;
			}
			return count;
		}

		private static void CountInventory()
		{
			Player p = Main.LocalPlayer;
			int remaining = 0, lacking = 0;
			foreach (var e in Entries) {
				e.Have = e.ItemType > 0 && p != null && p.active ? CountInInventory(p, e.ItemType) : 0;
				remaining += e.Remaining;
				lacking += System.Math.Max(0, e.Remaining - e.Have);
			}
			TotalRemaining = remaining;
			TotalLacking = lacking;
		}
	}
}
