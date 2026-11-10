using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader.Config;
using Terraria.UI;

namespace Terraprint.Common.UI
{
	/// <summary>Aba da barra lateral: icone + nome num painel do jogo. A ativa fica clara, com borda e texto dourados.</summary>
	public class UINavButton : UIElement
	{
		private readonly string _icon;
		private readonly Func<string> _label;
		public Func<bool> IsActive = () => false;
		public Action OnClick;

		public UINavButton(string icon, Func<string> label, Action onClick)
		{
			_icon = icon;
			_label = label;
			OnClick = onClick;
			Height.Set(38f, 0f);
			Width.Set(0f, 1f);
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			SoundEngine.PlaySound(SoundID.MenuTick);
			OnClick?.Invoke();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
		}

		public override void MouseOver(UIMouseEvent evt)
		{
			base.MouseOver(evt);
			if (!IsActive())
				SoundEngine.PlaySound(SoundID.MenuTick);
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			var r = GetDimensions().ToRectangle();
			bool active = IsActive();
			bool hover = IsMouseHovering;
			Color bg = active ? TerraprintStyle.TabColor : hover ? Color.Lerp(TerraprintStyle.TabIdle, TerraprintStyle.TabColor, 0.6f) : TerraprintStyle.TabIdle;
			Color border = active ? TerraprintStyle.Highlight : TerraprintStyle.PanelBorder;
			TerraprintStyle.RoundedBox(sb, r, bg, border);
			if (active)
				TerraprintStyle.Fill(sb, new Rectangle(r.X + 6, r.Y + 3, r.Width - 12, 2), Color.White * 0.1f);

			var tex = TerraprintStyle.Icon(_icon);
			float a = active ? 1f : hover ? 0.95f : 0.75f;
			sb.Draw(tex, new Vector2(r.X + 10, r.Y + (r.Height - 22) / 2f), null, Color.White * a, 0f, Vector2.Zero, 22f / tex.Height, SpriteEffects.None, 0f);
			Color text = active ? TerraprintStyle.Highlight : hover ? Color.Lerp(TerraprintStyle.Title, TerraprintStyle.Highlight, 0.35f) : TerraprintStyle.Title * 0.9f;
			TerraprintStyle.Text(sb, _label(), new Vector2(r.X + 40, r.Y + r.Height / 2f - 9f), text, 0.85f, r.Width - 46f);
		}
	}

	/// <summary>Titulo de secao dentro de uma lista de opcoes.</summary>
	public class UISectionHeader : UIElement
	{
		private readonly string _text;

		public UISectionHeader(string text)
		{
			_text = text;
			Width.Set(0f, 1f);
			Height.Set(30f, 0f);
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			var r = GetDimensions().ToRectangle();
			TerraprintStyle.Text(sb, _text, new Vector2(r.X + 4, r.Y + 6), TerraprintStyle.Accent, 0.9f);
			TerraprintStyle.DrawSeparator(sb, r.X, r.Bottom - 5, r.Width);
		}
	}

	/// <summary>Base das linhas de opcao: fundo de cartao, nome a esquerda e dica ao passar o mouse.</summary>
	public abstract class UIOptionRow : UIElement
	{
		protected readonly string Label;
		protected readonly string Tip;

		protected UIOptionRow(string label, string tip, float height = 34f)
		{
			Label = label;
			Tip = tip;
			Width.Set(0f, 1f);
			Height.Set(height, 0f);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
		}

		protected Rectangle Box => GetDimensions().ToRectangle();

		protected override void DrawSelf(SpriteBatch sb)
		{
			var r = Box;
			TerraprintStyle.RoundedBox(sb, r, IsMouseHovering ? TerraprintStyle.CardHover : TerraprintStyle.Card, TerraprintStyle.PanelBorder);
			TerraprintStyle.Text(sb, Label, new Vector2(r.X + 10, r.Y + 9), TerraprintStyle.Title, 0.8f, r.Width * 0.55f);
			if (IsMouseHovering && !string.IsNullOrEmpty(Tip))
				Main.hoverItemName = Tip;
		}
	}

	/// <summary>Opcao liga/desliga com um interruptor desenhado a direita.</summary>
	public class UIToggleRow : UIOptionRow
	{
		private readonly Func<bool> _get;
		private readonly Action<bool> _set;

		public UIToggleRow(string label, string tip, Func<bool> get, Action<bool> set) : base(label, tip)
		{
			_get = get;
			_set = set;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			SoundEngine.PlaySound(SoundID.MenuTick);
			_set(!_get());
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			base.DrawSelf(sb);
			DrawSwitch(sb, Box, _get());
		}

		public static void DrawSwitch(SpriteBatch sb, Rectangle row, bool on)
		{
			var track = new Rectangle(row.Right - 52, row.Y + row.Height / 2 - 9, 40, 18);
			TerraprintStyle.RoundedBox(sb, track, on ? TerraprintStyle.ToggleOn : TerraprintStyle.InputBackground, TerraprintStyle.PanelBorder);
			var knob = new Rectangle(on ? track.Right - 16 : track.X + 2, track.Y + 2, 14, 14);
			TerraprintStyle.Fill(sb, knob, on ? TerraprintStyle.Title : TerraprintStyle.Muted);
			TerraprintStyle.Fill(sb, new Rectangle(knob.X, knob.Bottom - 2, knob.Width, 2), Color.Black * 0.3f);
		}
	}

	/// <summary>Opcao numerica com barra deslizante. Grava quando o botao do mouse e solto.</summary>
	public class UISliderRow : UIOptionRow
	{
		private readonly Func<float> _get;
		private readonly Action<float> _set;
		private readonly float _min, _max, _step;
		private bool _dragging;
		private float _value;

		public UISliderRow(string label, string tip, float min, float max, float step, Func<float> get, Action<float> set) : base(label, tip)
		{
			_min = min;
			_max = max;
			_step = step;
			_get = get;
			_set = set;
		}

		private Rectangle Track
		{
			get {
				var r = Box;
				return new Rectangle(r.Right - 200, r.Y + r.Height / 2 - 3, 140, 6);
			}
		}

		public override void LeftMouseDown(UIMouseEvent evt)
		{
			base.LeftMouseDown(evt);
			var t = Track;
			if (Main.MouseScreen.X >= t.X - 8 && Main.MouseScreen.X <= t.Right + 8) {
				_dragging = true;
				_value = _get();
			}
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (!_dragging)
				return;
			var t = Track;
			float f = MathHelper.Clamp((Main.MouseScreen.X - t.X) / t.Width, 0f, 1f);
			float v = _min + f * (_max - _min);
			v = (float)Math.Round(v / _step) * _step;
			_value = MathHelper.Clamp(v, _min, _max);
			if (!Main.mouseLeft) {
				_dragging = false;
				if (Math.Abs(_value - _get()) > 0.0001f)
					_set(_value);
			}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			base.DrawSelf(sb);
			float v = _dragging ? _value : _get();
			var t = Track;
			TerraprintStyle.Fill(sb, new Rectangle(t.X - 1, t.Y - 1, t.Width + 2, t.Height + 2), TerraprintStyle.PanelBorder);
			TerraprintStyle.Fill(sb, t, TerraprintStyle.InputBackground);
			float f = (_max - _min) <= 0f ? 0f : (v - _min) / (_max - _min);
			TerraprintStyle.Fill(sb, new Rectangle(t.X, t.Y, (int)(t.Width * f), t.Height), TerraprintStyle.Accent);
			var knob = new Rectangle((int)(t.X + t.Width * f) - 5, t.Y - 6, 10, 18);
			TerraprintStyle.RoundedBox(sb, knob, TerraprintStyle.Title, TerraprintStyle.PanelBorder);
			TerraprintStyle.Text(sb, v.ToString(_step < 1f ? "0.00" : "0"), new Vector2(Box.Right - 14, Box.Y + 9), TerraprintStyle.Highlight, 0.8f, 0f, 1f);
		}
	}

	/// <summary>Opcao inteira com botoes - e +.</summary>
	public class UIStepperRow : UIOptionRow
	{
		private readonly Func<int> _get;
		private readonly Action<int> _set;
		private readonly int _min, _max, _step;

		public UIStepperRow(string label, string tip, int min, int max, int step, Func<int> get, Action<int> set) : base(label, tip)
		{
			_min = min;
			_max = max;
			_step = step;
			_get = get;
			_set = set;
		}

		private Rectangle Minus => new(Box.Right - 118, Box.Y + Box.Height / 2 - 11, 26, 22);
		private Rectangle Plus => new(Box.Right - 38, Box.Y + Box.Height / 2 - 11, 26, 22);

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			var m = Main.MouseScreen.ToPoint();
			int v = _get();
			int nv = v;
			if (Minus.Contains(m))
				nv = Math.Max(_min, v - _step);
			else if (Plus.Contains(m))
				nv = Math.Min(_max, v + _step);
			if (nv != v) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				_set(nv);
			}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			base.DrawSelf(sb);
			var m = Main.MouseScreen.ToPoint();
			int v = _get();
			DrawButton(sb, Minus, "-", Minus.Contains(m), v > _min);
			DrawButton(sb, Plus, "+", Plus.Contains(m), v < _max);
			TerraprintStyle.Text(sb, v.ToString(), new Vector2((Minus.Right + Plus.X) / 2f, Box.Y + 9), TerraprintStyle.Highlight, 0.85f, 0f, 0.5f);
		}

		public static void DrawButton(SpriteBatch sb, Rectangle r, string text, bool hover, bool enabled)
		{
			TerraprintStyle.RoundedBox(sb, r, !enabled ? TerraprintStyle.ButtonDisabled : hover ? TerraprintStyle.ButtonHover : TerraprintStyle.Button, TerraprintStyle.PanelBorder);
			TerraprintStyle.Text(sb, text, new Vector2(r.Center.X, r.Y + 3), !enabled ? TerraprintStyle.Muted : hover ? TerraprintStyle.Highlight : TerraprintStyle.Title, 0.9f, 0f, 0.5f);
		}
	}

	/// <summary>
	/// Lista de itens (listas de bloqueio): um slot por item, clique no item remove, o slot "+" abre o seletor.
	/// A altura cresce com a quantidade de itens.
	/// </summary>
	public class UIItemListRow : UIOptionRow
	{
		private const float Slot = 34f;
		private const float Step = Slot + 4f;
		private const float RowTop = 32f;

		private readonly Func<List<ItemDefinition>> _get;
		private readonly Action<List<ItemDefinition>> _set;
		private readonly Action<Action<int>> _openPicker;
		private readonly Item _item = new();
		private int _lastCount = -1;

		public UIItemListRow(string label, string tip, Func<List<ItemDefinition>> get, Action<List<ItemDefinition>> set, Action<Action<int>> openPicker)
			: base(label, tip, RowTop + Step + 6f)
		{
			_get = get;
			_set = set;
			_openPicker = openPicker;
		}

		private int Columns => Math.Max(1, (int)((Box.Width - 16f + 4f) / Step));

		private Rectangle SlotRect(int i)
		{
			int cols = Columns;
			return new Rectangle((int)(Box.X + 10 + i % cols * Step), (int)(Box.Y + RowTop + i / cols * Step), (int)Slot, (int)Slot);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			int count = _get()?.Count ?? 0;
			if (count != _lastCount && Box.Width > 0) {
				_lastCount = count;
				int rows = (count + 1 + Columns - 1) / Columns;
				Height.Set(RowTop + rows * Step + 6f, 0f);
				// A lista que contem esta linha precisa reposicionar as de baixo.
				UIElement p = Parent;
				while (p != null && p is not UIList)
					p = p.Parent;
				(p ?? Parent)?.Recalculate();
			}
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			var list = _get() ?? new List<ItemDefinition>();
			var m = Main.MouseScreen.ToPoint();
			for (int i = 0; i < list.Count; i++) {
				if (SlotRect(i).Contains(m)) {
					SoundEngine.PlaySound(SoundID.MenuTick);
					var copy = new List<ItemDefinition>(list);
					copy.RemoveAt(i);
					_set(copy);
					return;
				}
			}
			if (SlotRect(list.Count).Contains(m)) {
				SoundEngine.PlaySound(SoundID.MenuOpen);
				_openPicker(type => {
					var current = _get() ?? new List<ItemDefinition>();
					if (current.Exists(d => d.Type == type))
						return;
					var copy = new List<ItemDefinition>(current) { new ItemDefinition(type) };
					_set(copy);
				});
			}
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			base.DrawSelf(sb);
			var list = _get() ?? new List<ItemDefinition>();
			var m = Main.MouseScreen.ToPoint();
			for (int i = 0; i < list.Count; i++) {
				var r = SlotRect(i);
				bool hover = r.Contains(m);
				TerraprintStyle.DrawSlot(sb, r, false, hover, true, hover ? new Color(255, 150, 150) : null);
				var def = list[i];
				if (!def.IsUnloaded && def.Type > 0) {
					if (_item.type != def.Type)
						_item.SetDefaults(def.Type);
					ItemSlot.DrawItemIcon(_item, ItemSlot.Context.InventoryItem, sb, r.Center.ToVector2(), _item.scale, 24f, Color.White);
				}
				if (hover)
					Main.hoverItemName = (def.IsUnloaded ? def.ToString() : Lang.UI("ItemRemoveTip", Terraria.Lang.GetItemNameValue(def.Type)));
			}
			var plus = SlotRect(list.Count);
			bool ph = plus.Contains(m);
			TerraprintStyle.DrawSlot(sb, plus, ph, ph, true);
			TerraprintStyle.Text(sb, "+", new Vector2(plus.Center.X, plus.Y + 6), ph ? TerraprintStyle.Highlight : TerraprintStyle.Title, 1.1f, 0f, 0.5f);
			if (ph)
				Main.hoverItemName = Lang.UI("ItemAddTip");
			if (list.Count == 0)
				TerraprintStyle.Text(sb, Lang.UI("ListEmpty"), new Vector2(plus.Right + 10, plus.Y + 9), TerraprintStyle.Muted, 0.75f);
		}
	}
}
