using System;
using System.Collections.Generic;
using System.Linq;
using MagicStorage;
using MagicStorage.Common.Systems;
using MagicStorage.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terraprint.Common.Configs;
using Terraprint.Common.Systems;
using Terraprint.Common.UI;

namespace Terraprint.Common.CrossMod
{
	/// <summary>
	/// Integracao opcional com o Magic Storage. So e carregada quando o Magic Storage esta ativo
	/// (ExtendsFromMod); sem ele, nada daqui roda e o Terraprint funciona normalmente.
	/// Com o terminal do storage aberto, aparece um botao "Terraprint" ao lado dele que abre um painel com
	/// o resumo do storage, o blueprint fixado no mapa e os materiais que faltam (inventario x storage).
	/// </summary>
	[ExtendsFromMod("MagicStorage")]
	[Autoload(Side = ModSide.Client)]
	public class MagicStorageIntegration : ModSystem
	{
		private static UserInterface _interface;
		private static MagicStorageState _state;
		private static GameTime _lastTime;

		/// <summary>O painel fica aberto (ou fechado) entre uma abertura e outra do storage.</summary>
		internal static bool PanelOpen;

		/// <summary>Terminal de itens do Magic Storage aberto (coracao, acesso ou acesso remoto).</summary>
		internal static bool StorageOpen => MagicUI.storageUI != null && MagicUI.IsStorageUIOpen();

		public override void Load()
		{
			_interface = new UserInterface();
			_state = new MagicStorageState();
		}

		public override void Unload()
		{
			_interface = null;
			_state = null;
		}

		public override void OnWorldUnload()
		{
			PanelOpen = false;
			_interface?.SetState(null);
		}

		public override void UpdateUI(GameTime gameTime)
		{
			_lastTime = gameTime;
			bool show = StorageOpen && TerraprintConfig.Instance.MagicStorageButton;
			if (!show) {
				if (_interface.CurrentState != null)
					_interface.SetState(null);
				return;
			}
			if (_interface.CurrentState == null)
				_interface.SetState(_state);
			_interface.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			if (index < 0)
				return;
			layers.Insert(index, new LegacyGameInterfaceLayer("Terraprint: Magic Storage", () => {
				if (_interface?.CurrentState != null)
					_interface.Draw(Main.spriteBatch, _lastTime);
				return true;
			}, InterfaceScaleType.UI));
		}

		// ----- Dados do storage -----

		/// <summary>Quantos de cada item o storage aberto tem.</summary>
		internal static readonly Dictionary<int, long> Stored = new();
		internal static int Units, UsedSlots, Capacity;
		internal static long TotalItems;
		internal static bool HasHeart;

		/// <summary>Le os itens do storage aberto. Barato o bastante para rodar algumas vezes por segundo.</summary>
		internal static void CountStorage()
		{
			Stored.Clear();
			Units = UsedSlots = Capacity = 0;
			TotalItems = 0;
			TEStorageHeart heart = StorageGUI.GetHeart();
			HasHeart = heart != null;
			if (heart == null)
				return;
			try {
				foreach (Item item in heart.GetStoredItems()) {
					Stored.TryGetValue(item.type, out long n);
					Stored[item.type] = n + item.stack;
					TotalItems += item.stack;
				}
				foreach (var unit in heart.GetStorageUnits().OfType<TEStorageUnit>()) {
					Units++;
					UsedSlots += unit.NumItems;
					Capacity += unit.Capacity;
				}
			}
			catch (InvalidOperationException) {
				// O Magic Storage mexeu na lista enquanto liamos (atualizacao em outra thread); tenta no proximo ciclo.
			}
		}

		internal static long InStorage(int itemType) => itemType > 0 && Stored.TryGetValue(itemType, out long n) ? n : 0;

		/// <summary>Quanto ainda falta de cada material depois do que esta no inventario.</summary>
		internal static int Lacking(MaterialEntry e) => Math.Max(0, e.Remaining - e.Have);

		/// <summary>
		/// Tira do storage o que falta para terminar o blueprint (descontando o inventario) e poe no inventario.
		/// No multiplayer o pedido vai para o servidor pelo proprio Magic Storage, que entrega os itens.
		/// </summary>
		internal static void WithdrawMissing()
		{
			TEStorageHeart heart = StorageGUI.GetHeart();
			Player player = Main.LocalPlayer;
			if (heart == null || player == null || !ProjectionSystem.HasProjection)
				return;
			bool mp = Main.netMode == NetmodeID.MultiplayerClient;
			int taken = 0;
			bool full = false;
			var types = new List<int>();
			foreach (var e in MaterialsTracker.Entries.ToList()) {
				if (full || e.ItemType <= 0)
					continue;
				long want = Math.Min(Lacking(e), InStorage(e.ItemType));
				var sample = ContentSamples.ItemsByType.TryGetValue(e.ItemType, out Item s) ? s : null;
				int maxStack = Math.Max(1, sample?.maxStack ?? 9999);
				while (want > 0) {
					int chunk = (int)Math.Min(want, maxStack);
					want -= chunk;
					Item got = StorageGUI.DoWithdraw(new Item(e.ItemType, chunk), toInventory: true);
					if (mp) {
						taken += chunk;
						types.Add(e.ItemType);
						continue;
					}
					if (got.IsAir)
						break;
					int amount = got.stack;
					types.Add(got.type);
					Item rest = player.GetItem(player.whoAmI, got, GetItemSettings.InventoryEntityToPlayerInventorySettings);
					if (!rest.IsAir) {
						// Inventario cheio: o que nao coube volta para o storage.
						amount -= rest.stack;
						heart.TryDeposit(rest, player);
						full = true;
					}
					taken += amount;
					if (full)
						break;
				}
			}

			if (types.Count > 0) {
				MagicUI.SetNextCollectionsToRefresh(types.Distinct());
				SoundEngine.PlaySound(SoundID.Grab);
			}
			if (taken == 0)
				Main.NewText(Lang.UI(full ? "MSWithdrawFull" : "MSWithdrawNothing"), Color.Orange);
			else if (mp)
				Lang.Info(Lang.UI("MSWithdrawRequested", taken), Color.LightGreen);
			else
				Lang.Info(Lang.UI(full ? "MSWithdrewPartial" : "MSWithdrew", taken), full ? Color.Orange : Color.LightGreen);
			CountStorage();
		}
	}

	/// <summary>Botao ao lado do terminal do Magic Storage e o painel do Terraprint que ele abre.</summary>
	[ExtendsFromMod("MagicStorage")]
	public class MagicStorageState : UIState
	{
		private const float ButtonW = 160f, ButtonH = 32f, PanelW = 400f, Gap = 8f, Pad = 12f;
		private const int RefreshTicks = 20;

		private UITextButton _toggle;
		private UIBox _panel;
		private StorageMaterialsList _list;
		private int _timer;
		private Vector4 _layout = new(-1f);

		public override void OnInitialize()
		{
			_toggle = new UITextButton(() => Lang.UI(MagicStorageIntegration.PanelOpen ? "MSButtonOpen" : "MSButton"), Toggle) {
				IsActive = () => MagicStorageIntegration.PanelOpen,
				Tooltip = () => Lang.UI("MSButtonTip"),
			};
			_toggle.Width.Set(ButtonW, 0f);
			_toggle.Height.Set(ButtonH, 0f);
			Append(_toggle);

			_panel = new UIBox(TerraprintStyle.PanelBackground);
			_panel.Width.Set(PanelW, 0f);
			_panel.SetPadding(Pad);

			float y = 0f;
			Line(() => Lang.UI("MSTitle"), 0.95f, TerraprintStyle.Highlight, ref y, 26f);
			Line(() => Lang.UI("MSStorage"), 0.75f, TerraprintStyle.Accent, ref y, 20f);
			Line(StorageLine, 0.75f, TerraprintStyle.Title, ref y, 22f);
			y += 4f;
			Line(() => Lang.UI("MSBlueprint"), 0.75f, TerraprintStyle.Accent, ref y, 20f);
			Line(ProjectionLine, 0.75f, TerraprintStyle.Title, ref y, 22f);
			var progress = new UIProgress();
			progress.Top.Set(y, 0f);
			progress.Width.Set(0f, 1f);
			progress.Height.Set(24f, 0f);
			_panel.Append(progress);
			y += 32f;
			Line(SummaryLine, 0.72f, TerraprintStyle.Muted, ref y, 22f);

			_list = new StorageMaterialsList();
			_list.Top.Set(y, 0f);
			_list.Width.Set(-20f, 1f);
			_list.Height.Set(-y - ButtonH - 8f, 1f);
			_panel.Append(_list);
			var scroll = new UIScrollbar();
			scroll.Left.Set(-16f, 1f);
			scroll.Top.Set(y + 4f, 0f);
			scroll.Height.Set(-y - ButtonH - 16f, 1f);
			_panel.Append(scroll);
			_list.Scrollbar = scroll;

			var withdraw = new UITextButton(() => Lang.UI("MSWithdraw"), MagicStorageIntegration.WithdrawMissing) {
				IsEnabled = CanWithdraw,
				Tooltip = () => Lang.UI("MSWithdrawTip"),
			};
			withdraw.Top.Set(-ButtonH, 1f);
			withdraw.Width.Set(0f, 1f);
			withdraw.Height.Set(ButtonH, 0f);
			_panel.Append(withdraw);
		}

		private void Line(Func<string> text, float scale, Color color, ref float y, float height)
		{
			var label = new UILabel(text, scale, color);
			label.Top.Set(y, 0f);
			label.Width.Set(0f, 1f);
			label.Height.Set(height, 0f);
			_panel.Append(label);
			y += height;
		}

		private void Toggle()
		{
			MagicStorageIntegration.PanelOpen = !MagicStorageIntegration.PanelOpen;
			_timer = RefreshTicks;
		}

		private static bool CanWithdraw()
		{
			if (!ProjectionSystem.HasProjection || !MagicStorageIntegration.HasHeart)
				return false;
			foreach (var e in MaterialsTracker.Entries) {
				if (MagicStorageIntegration.Lacking(e) > 0 && MagicStorageIntegration.InStorage(e.ItemType) > 0)
					return true;
			}
			return false;
		}

		private static string StorageLine()
		{
			if (!MagicStorageIntegration.HasHeart)
				return Lang.UI("MSNoHeart");
			return Lang.UI("MSStorageLine", MagicStorageIntegration.Units, MagicStorageIntegration.UsedSlots, MagicStorageIntegration.Capacity,
				MagicStorageIntegration.TotalItems, MagicStorageIntegration.Stored.Count);
		}

		private static string ProjectionLine()
		{
			if (!ProjectionSystem.HasProjection)
				return Lang.UI("MSNoProjection");
			var bp = ProjectionSystem.Current;
			if (ProjectionSystem.State != ProjectionSystem.ProjectionState.Fixed)
				return Lang.UI("MSProjectionMoving", bp.Name, bp.Width, bp.Height);
			var area = ProjectionSystem.Area;
			Vector2 center = new(area.Center.X, area.Center.Y);
			int distance = (int)Vector2.Distance(center, Main.LocalPlayer.Center / 16f);
			return Lang.UI("MSProjectionLine", bp.Name, bp.Width, bp.Height, distance);
		}

		private static string SummaryLine()
		{
			if (!ProjectionSystem.HasProjection)
				return "";
			if (MaterialsTracker.TotalRemaining == 0)
				return Lang.UI("MaterialsDone");
			long fromStorage = 0, none = 0;
			foreach (var e in MaterialsTracker.Entries) {
				int lacking = MagicStorageIntegration.Lacking(e);
				long take = Math.Min(lacking, MagicStorageIntegration.InStorage(e.ItemType));
				fromStorage += take;
				none += lacking - take;
			}
			return Lang.UI("MSSummary", MaterialsTracker.TotalRemaining, MaterialsTracker.TotalRemaining - MaterialsTracker.TotalLacking, fromStorage, none);
		}

		public override void Update(GameTime gameTime)
		{
			Layout();
			if (MagicStorageIntegration.PanelOpen) {
				if (_panel.Parent == null)
					Append(_panel);
				MaterialsTracker.Update();
				if (++_timer >= RefreshTicks) {
					_timer = 0;
					MagicStorageIntegration.CountStorage();
				}
			}
			else if (_panel.Parent != null)
				_panel.Remove();
			base.Update(gameTime);
			if (_toggle.IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
		}

		/// <summary>Acompanha o painel do Magic Storage (ele pode ser arrastado e mudar de altura).</summary>
		private void Layout()
		{
			var ui = MagicUI.storageUI;
			if (ui == null)
				return;
			float screenW = Main.screenWidth / Main.UIScale, screenH = Main.screenHeight / Main.UIScale;
			float left = ui.PanelRight + Gap, top = ui.PanelTop;
			// Se nao couber a direita do storage, vai para a esquerda dele.
			if (left + PanelW > screenW - Gap)
				left = Math.Max(Gap, ui.PanelLeft - PanelW - Gap);
			float height = Math.Clamp(Math.Min(ui.PanelHeight, screenH - top - Gap) - ButtonH - Gap, 300f, 620f);
			var layout = new Vector4(left, top, height, screenW);
			if (layout == _layout)
				return;
			_layout = layout;
			_toggle.Left.Set(left, 0f);
			_toggle.Top.Set(top, 0f);
			_panel.Left.Set(left, 0f);
			_panel.Top.Set(top + ButtonH + Gap, 0f);
			_panel.Height.Set(height, 0f);
			Recalculate();
		}
	}

	/// <summary>Lista dos materiais que faltam: icone, nome e quanto falta, tem no inventario e tem no storage.</summary>
	[ExtendsFromMod("MagicStorage")]
	public class StorageMaterialsList : UIElement
	{
		private const float RowH = 34f;
		private static readonly Color Ok = new(130, 210, 140), Partial = new(240, 200, 90), Lacking = new(255, 120, 120);

		public UIScrollbar Scrollbar;
		private readonly Item _item = new();

		private int VisibleRows => Math.Max(1, (int)(GetDimensions().Height / RowH));
		private int FirstRow => Scrollbar == null ? 0 : (int)Math.Round(Scrollbar.ViewPosition);

		public override void ScrollWheel(UIScrollWheelEvent evt)
		{
			base.ScrollWheel(evt);
			if (Scrollbar != null)
				Scrollbar.ViewPosition -= evt.ScrollWheelValue / 120f;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			int count = MaterialsTracker.Entries.Count + 1;
			Scrollbar?.SetView(VisibleRows, Math.Max(count, VisibleRows));
			if (IsMouseHovering) {
				Main.LocalPlayer.mouseInterface = true;
				Terraria.GameInput.PlayerInput.LockVanillaMouseScroll("Terraprint/MagicStorage");
			}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			var d = GetDimensions();
			var entries = MaterialsTracker.Entries;
			float numW = 58f;
			float colStorage = d.X + d.Width - numW, colHave = colStorage - numW, colRemaining = colHave - numW;
			const float scale = 0.72f;

			// Cabecalho das colunas.
			TerraprintStyle.Text(sb, Lang.UI("MSColItem"), new Vector2(d.X + 4f, d.Y + 8f), TerraprintStyle.Muted, scale);
			TerraprintStyle.Text(sb, Lang.UI("ColRemaining"), new Vector2(colRemaining + numW / 2f, d.Y + 8f), TerraprintStyle.Muted, scale, numW, 0.5f);
			TerraprintStyle.Text(sb, Lang.UI("MSColInventory"), new Vector2(colHave + numW / 2f, d.Y + 8f), TerraprintStyle.Muted, scale, numW, 0.5f);
			TerraprintStyle.Text(sb, Lang.UI("MSColStorage"), new Vector2(colStorage + numW / 2f, d.Y + 8f), TerraprintStyle.Muted, scale, numW, 0.5f);

			if (!ProjectionSystem.HasProjection || entries.Count == 0)
				return;

			int first = FirstRow;
			int rows = VisibleRows - 1;
			for (int k = 0; k < rows && first + k < entries.Count; k++) {
				var e = entries[first + k];
				float y = d.Y + RowH * (k + 1);
				var row = new Rectangle((int)d.X, (int)y, (int)d.Width, (int)RowH - 4);
				bool hover = row.Contains(Main.MouseScreen.ToPoint()) && IsMouseHovering;
				TerraprintStyle.RoundedBox(sb, row, hover ? TerraprintStyle.RowHover : TerraprintStyle.Row, TerraprintStyle.PanelBorder);

				long stored = MagicStorageIntegration.InStorage(e.ItemType);
				int lacking = MagicStorageIntegration.Lacking(e);
				Color status = e.Done || lacking == 0 ? Ok : stored >= lacking ? Partial : Lacking;

				var slot = new Rectangle(row.X + 3, row.Y + 1, row.Height - 2, row.Height - 2);
				if (e.ItemType > 0) {
					if (_item.type != e.ItemType)
						_item.SetDefaults(e.ItemType);
					ItemSlot.DrawItemIcon(_item, ItemSlot.Context.InventoryItem, sb, slot.Center.ToVector2(), _item.scale, 24f, e.Done ? Color.White * 0.5f : Color.White);
				}
				else
					TerraprintStyle.Text(sb, "?", new Vector2(slot.Center.X, slot.Y + 4f), TerraprintStyle.Muted, 0.8f, 0f, 0.5f);

				string name = e.ItemType > 0 ? e.Name : Lang.UI("NoItem", e.Name);
				float nameX = slot.Right + 6f;
				TerraprintStyle.Text(sb, name, new Vector2(nameX, row.Y + 7f), e.Done ? TerraprintStyle.Muted : TerraprintStyle.Title, scale, colRemaining - nameX - 4f);
				TerraprintStyle.Text(sb, e.Done ? "-" : e.Remaining.ToString(), new Vector2(colRemaining + numW / 2f, row.Y + 7f), status, scale, numW, 0.5f);
				TerraprintStyle.Text(sb, e.ItemType > 0 ? e.Have.ToString() : "-", new Vector2(colHave + numW / 2f, row.Y + 7f), TerraprintStyle.Title, scale, numW, 0.5f);
				TerraprintStyle.Text(sb, e.ItemType > 0 ? Short(stored) : "-", new Vector2(colStorage + numW / 2f, row.Y + 7f), TerraprintStyle.Title, scale, numW, 0.5f);

				if (hover) {
					Main.hoverItemName = name + "\n" + (e.Done ? Lang.UI("MSRowDone")
						: lacking == 0 ? Lang.UI("MSRowHave")
						: stored >= lacking ? Lang.UI("MSRowStorage", lacking)
						: Lang.UI("MSRowLacking", lacking - stored));
				}
			}
		}

		/// <summary>12345 -> "12.3k", para caber na coluna.</summary>
		private static string Short(long n) => n >= 100000 ? (n / 1000) + "k" : n >= 10000 ? (n / 1000f).ToString("0.0") + "k" : n.ToString();
	}
}
