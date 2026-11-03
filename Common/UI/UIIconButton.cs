using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.UI;

namespace Terraprint.Common.UI
{
	/// <summary>
	/// Botao quadrado no formato de slot do inventario, com um icone (do mod ou de um item do jogo)
	/// e uma dica ao passar o mouse. Pode agir no clique ou enquanto fica pressionado.
	/// </summary>
	public class UIIconButton : UIElement
	{
		private readonly string _icon;
		private readonly Item _item;
		private readonly float _rotation;

		public Func<string> Tooltip;
		public Action OnClick;
		/// <summary>Chamado a cada tick enquanto o botao esta pressionado (no lugar do clique).</summary>
		public Action WhileHeld;
		public Func<bool> IsActive = () => false;
		public Func<bool> IsEnabled = () => true;
		/// <summary>Cor do fundo do slot (ex.: vermelho para um botao de confirmacao).</summary>
		public Func<Color?> Tint = () => null;

		/// <summary>Sem fundo de slot (so o icone), como o X de fechar do jogo.</summary>
		public bool Bare;

		public bool Held { get; private set; }

		/// <param name="icon">Nome do icone em Assets/UI.</param>
		/// <param name="rotation">Em quartos de volta, para reaproveitar a seta para cima nas outras direcoes.</param>
		public UIIconButton(string icon, string tooltip, Action onClick, int rotation = 0) : this(tooltip, onClick)
		{
			_icon = icon;
			_rotation = rotation * MathHelper.PiOver2;
		}

		public UIIconButton(int itemType, string tooltip, Action onClick) : this(tooltip, onClick)
		{
			_item = new Item(itemType);
		}

		private UIIconButton(string tooltip, Action onClick)
		{
			Tooltip = () => tooltip;
			OnClick = onClick;
			Width.Set(TerraprintStyle.SlotSize, 0f);
			Height.Set(TerraprintStyle.SlotSize, 0f);
		}

		public override void LeftMouseDown(UIMouseEvent evt)
		{
			base.LeftMouseDown(evt);
			if (WhileHeld != null && IsEnabled())
				Held = true;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			if (!IsEnabled() || WhileHeld != null)
				return;
			SoundEngine.PlaySound(SoundID.MenuTick);
			OnClick?.Invoke();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (Held && (!Main.mouseLeft || !IsEnabled()))
				Held = false;
			if (Held)
				WhileHeld?.Invoke();
			if (IsMouseHovering)
				Main.LocalPlayer.mouseInterface = true;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			bool enabled = IsEnabled();
			var r = GetDimensions().ToRectangle();
			// A dica e definida no desenho (o jogo limpa o texto do mouse antes de desenhar a interface).
			if (IsMouseHovering) {
				string tip = Tooltip?.Invoke();
				if (!string.IsNullOrEmpty(tip))
					Main.hoverItemName = tip;
			}
			if (!Bare)
				TerraprintStyle.DrawSlot(spriteBatch, r, IsActive() || Held, IsMouseHovering, enabled, Tint());

			Color iconColor = !enabled ? Color.White * 0.35f : Bare && !IsMouseHovering ? new Color(210, 220, 235) : Color.White;
			Vector2 center = r.Center.ToVector2();
			if (_item != null) {
				ItemSlot.DrawItemIcon(_item, ItemSlot.Context.InventoryItem, spriteBatch, center, _item.scale, System.Math.Min(28f, r.Width - 10f), iconColor);
			}
			else if (_icon != null) {
				var tex = TerraprintStyle.Icon(_icon);
				float scale = System.Math.Min(1f, (r.Width - 6f) / tex.Width);
				spriteBatch.Draw(tex, center, null, iconColor, _rotation, tex.Size() / 2f, scale, SpriteEffects.None, 0f);
			}
		}
	}
}
