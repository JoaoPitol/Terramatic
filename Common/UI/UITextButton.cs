using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;

namespace Terraprint.Common.UI
{
	/// <summary>
	/// Botao de texto no painel arredondado do jogo (borda preta). Fica dourado quando ativo,
	/// mais claro ao passar o mouse e apagado quando desabilitado. O texto encolhe para caber.
	/// Tambem pode mostrar um icone de Assets/UI no lugar do texto.
	/// </summary>
	public class UITextButton : UIPanel
	{
		public Func<string> Label;
		public Func<string> Tooltip = () => null;
		public Action OnClick;
		/// <summary>Chamado a cada tick enquanto o botao esta pressionado (no lugar do clique).</summary>
		public Action WhileHeld;
		public Func<bool> IsActive = () => false;
		public Func<bool> IsEnabled = () => true;
		/// <summary>Cor de destaque (ex.: vermelho para confirmar uma exclusao).</summary>
		public Func<Color?> Tint = () => null;
		public float TextScale = 0.85f;

		private readonly string _icon;
		private readonly float _rotation;

		public bool Held { get; private set; }

		public UITextButton(string label, Action onClick) : this(() => label, onClick) { }

		public UITextButton(Func<string> label, Action onClick)
		{
			Label = label;
			OnClick = onClick;
			SetPadding(0f);
			BorderColor = TerraprintStyle.PanelBorder;
		}

		/// <param name="rotation">Em quartos de volta (para reaproveitar a seta para cima).</param>
		public UITextButton(string icon, string tooltip, Action onClick, int rotation) : this(() => "", onClick)
		{
			_icon = icon;
			_rotation = rotation * MathHelper.PiOver2;
			Tooltip = () => tooltip;
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

		public override void MouseOver(UIMouseEvent evt)
		{
			base.MouseOver(evt);
			if (IsEnabled())
				SoundEngine.PlaySound(SoundID.MenuTick);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			bool enabled = IsEnabled();
			bool active = IsActive() || Held;
			Color? tint = Tint();
			// Como os botoes dos menus do jogo: o painel clareia ao passar o mouse;
			// o selecionado ganha borda e texto dourados.
			Color textColor = TerraprintStyle.Title;
			BorderColor = TerraprintStyle.PanelBorder;
			if (!enabled) {
				BackgroundColor = TerraprintStyle.ButtonDisabled;
				textColor = TerraprintStyle.Muted * 0.8f;
			}
			else if (tint.HasValue) {
				BackgroundColor = IsMouseHovering ? Color.Lerp(tint.Value, Color.White, 0.2f) : tint.Value;
			}
			else if (active) {
				BackgroundColor = IsMouseHovering ? TerraprintStyle.ButtonActiveHover : TerraprintStyle.ButtonActive;
				BorderColor = TerraprintStyle.Highlight;
				textColor = TerraprintStyle.Highlight;
			}
			else {
				BackgroundColor = IsMouseHovering ? TerraprintStyle.ButtonHover : TerraprintStyle.Button;
				if (IsMouseHovering)
					textColor = Color.Lerp(TerraprintStyle.Title, TerraprintStyle.Highlight, 0.35f);
			}
			base.DrawSelf(spriteBatch);

			var d = GetDimensions();
			Vector2 center = new(d.X + d.Width / 2f, d.Y + d.Height / 2f);
			Color color = textColor;

			if (_icon != null) {
				var tex = TerraprintStyle.Icon(_icon);
				float s = Math.Min(1f, (d.Height - 8f) / tex.Height);
				spriteBatch.Draw(tex, center, null, enabled ? Color.White : Color.White * 0.45f, _rotation, tex.Size() / 2f, s, SpriteEffects.None, 0f);
			}
			else {
				string text = Label?.Invoke() ?? "";
				var font = FontAssets.MouseText.Value;
				Vector2 size = font.MeasureString(text);
				float scale = TextScale;
				float available = d.Width - 12f;
				if (size.X * scale > available && size.X > 0f)
					scale = available / size.X;
				Vector2 pos = center - new Vector2(size.X * scale / 2f, size.Y * scale / 2f - 3f * scale);
				Utils.DrawBorderString(spriteBatch, text, pos, color, scale);
			}

			if (IsMouseHovering) {
				string tip = Tooltip?.Invoke();
				if (!string.IsNullOrEmpty(tip))
					Main.hoverItemName = tip;
			}
		}
	}
}
