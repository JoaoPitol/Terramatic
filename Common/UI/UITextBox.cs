using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.UI;

namespace Terraprint.Common.UI
{
	/// <summary>Campo de texto simples: clique para digitar, Enter ou Esc para sair.</summary>
	public class UITextBox : UIPanel
	{
		public string Text = "";
		public string Hint = "";
		public int MaxLength = 40;
		public bool Focused { get; private set; }

		private int _blink;

		public UITextBox(string hint)
		{
			Hint = hint;
			SetPadding(6);
			BackgroundColor = TerraprintStyle.InputBackground;
		}

		public void Focus()
		{
			Focused = true;
			Main.clrInput();
		}

		public void Unfocus() => Focused = false;

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			Focus();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			// Clique fora do campo encerra a digitacao.
			if (Focused && Main.mouseLeft && Main.mouseLeftRelease && !ContainsPoint(Main.MouseScreen))
				Unfocus();
			if (Focused)
				PlayerInput.WritingText = true;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			BorderColor = Focused ? Color.Gold : TerraprintStyle.PanelBorder;
			base.DrawSelf(spriteBatch);

			if (Focused) {
				PlayerInput.WritingText = true;
				Main.instance.HandleIME();
				string newText = Main.GetInputText(Text);
				if (newText.Length > MaxLength)
					newText = newText.Substring(0, MaxLength);
				Text = newText;
				if (Main.inputTextEnter || Main.inputTextEscape)
					Unfocus();
			}

			var dims = GetInnerDimensions();
			var pos = new Vector2(dims.X, dims.Y + 2);
			bool showHint = Text.Length == 0 && !Focused;
			string shown = showHint ? Hint : Text;
			if (Focused && (++_blink / 30) % 2 == 0)
				shown += "|";
			Utils.DrawBorderString(spriteBatch, shown, pos, showHint ? TerraprintStyle.Muted * 0.8f : Color.White, 0.85f);
		}
	}

	/// <summary>Painel que pode ser arrastado pela faixa de cima. DragTarget permite mover uma janela inteira (painel + abas).</summary>
	public class UIDragPanel : UIPanel
	{
		public float DragHeight = 30f;
		public UIElement DragTarget;
		private Vector2 _offset;
		private bool _dragging;

		private UIElement Target => DragTarget ?? this;

		public override void LeftMouseDown(UIMouseEvent evt)
		{
			base.LeftMouseDown(evt);
			if (evt.Target == this && evt.MousePosition.Y - GetDimensions().Y < DragHeight) {
				_offset = evt.MousePosition - new Vector2(Target.Left.Pixels, Target.Top.Pixels);
				_dragging = true;
			}
		}

		public override void LeftMouseUp(UIMouseEvent evt)
		{
			base.LeftMouseUp(evt);
			_dragging = false;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (ContainsPoint(Main.MouseScreen))
				Main.LocalPlayer.mouseInterface = true;

			var target = Target;
			if (_dragging) {
				target.Left.Set(Main.mouseX - _offset.X, 0f);
				target.Top.Set(Main.mouseY - _offset.Y, 0f);
				target.Recalculate();
			}

			// Mantem a janela dentro da tela.
			if (target.Parent == null)
				return;
			var parent = target.Parent.GetDimensions().ToRectangle();
			var me = target.GetDimensions().ToRectangle();
			if (!parent.Contains(me)) {
				target.Left.Pixels = Utils.Clamp(target.Left.Pixels, 0, System.Math.Max(0, parent.Width - me.Width));
				target.Top.Pixels = Utils.Clamp(target.Top.Pixels, 0, System.Math.Max(0, parent.Height - me.Height));
				target.Recalculate();
			}
		}
	}
}
