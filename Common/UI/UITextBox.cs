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
			BackgroundColor = new Color(33, 43, 79) * 0.9f;
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
			BorderColor = Focused ? Color.Gold : Color.Black;
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
			Utils.DrawBorderString(spriteBatch, shown, pos, showHint ? Color.Gray : Color.White, 0.9f);
		}
	}

	/// <summary>Botao de texto com destaque ao passar o mouse e estado desabilitado. O texto encolhe para caber no botao.</summary>
	public class UIButton : UITextPanel<string>
	{
		public bool Enabled = true;
		private readonly System.Action _onClick;
		private readonly float _baseScale;
		private string _label;
		private bool _fitting;

		public UIButton(string text, System.Action onClick, float textScale = 0.9f) : base(text, textScale)
		{
			_onClick = onClick;
			_baseScale = textScale;
			_label = text;
			SetPadding(6);
			MinWidth.Set(0f, 0f);
		}

		/// <summary>Troca o texto (sem custo se for igual ao atual).</summary>
		public void SetLabel(string text)
		{
			if (text == _label)
				return;
			_label = text;
			Fit();
		}

		public override void Recalculate()
		{
			base.Recalculate();
			Fit();
		}

		// UITextPanel aumenta MinWidth para caber o texto, o que fazia os botoes invadirem os vizinhos.
		// Aqui o botao mantem a largura do layout e o texto diminui ate caber.
		private void Fit()
		{
			if (_fitting)
				return;
			_fitting = true;
			float available = GetInnerDimensions().Width;
			float needed = FontAssets.MouseText.Value.MeasureString(_label).X * _baseScale;
			float scale = available > 0f && needed > available ? _baseScale * available / needed : _baseScale;
			SetText(_label, scale, false);
			MinWidth.Set(0f, 0f);
			MinHeight.Set(0f, 0f);
			base.Recalculate();
			_fitting = false;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			if (!Enabled)
				return;
			Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick);
			_onClick?.Invoke();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (!Enabled) {
				BackgroundColor = new Color(40, 40, 50) * 0.8f;
				TextColor = Color.Gray;
			}
			else {
				BackgroundColor = IsMouseHovering ? new Color(73, 94, 171) : new Color(63, 82, 151) * 0.85f;
				TextColor = Color.White;
			}
		}
	}

	/// <summary>Botao que age enquanto fica pressionado (chama a acao a cada tick).</summary>
	public class UIHoldButton : UIButton
	{
		private readonly System.Action _whileHeld;

		public bool Held { get; private set; }

		public UIHoldButton(string text, System.Action whileHeld, float textScale = 0.9f) : base(text, null, textScale)
		{
			_whileHeld = whileHeld;
		}

		public override void LeftMouseDown(UIMouseEvent evt)
		{
			base.LeftMouseDown(evt);
			if (Enabled)
				Held = true;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (Held && (!Main.mouseLeft || !Enabled))
				Held = false;
			if (Held) {
				_whileHeld?.Invoke();
				BackgroundColor = new Color(90, 150, 90);
			}
		}
	}

	/// <summary>Painel que pode ser arrastado pela barra de titulo.</summary>
	public class UIDragPanel : UIPanel
	{
		public float DragHeight = 30f;
		private Vector2 _offset;
		private bool _dragging;

		public override void LeftMouseDown(UIMouseEvent evt)
		{
			base.LeftMouseDown(evt);
			if (evt.Target == this && evt.MousePosition.Y - GetDimensions().Y < DragHeight) {
				_offset = evt.MousePosition - new Vector2(Left.Pixels, Top.Pixels);
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

			if (_dragging) {
				Left.Set(Main.mouseX - _offset.X, 0f);
				Top.Set(Main.mouseY - _offset.Y, 0f);
				Recalculate();
			}

			// Mantem o painel dentro da tela.
			var parent = Parent.GetDimensions().ToRectangle();
			var me = GetDimensions().ToRectangle();
			if (!me.Intersects(parent) || !parent.Contains(me)) {
				Left.Pixels = Utils.Clamp(Left.Pixels, 0, parent.Width - me.Width);
				Top.Pixels = Utils.Clamp(Top.Pixels, 0, parent.Height - me.Height);
				Recalculate();
			}
		}
	}
}
