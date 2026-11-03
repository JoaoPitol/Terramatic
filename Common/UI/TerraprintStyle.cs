using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;

namespace Terraprint.Common.UI
{
	/// <summary>Cores e desenhos comuns do visual do Terraprint (painel azul e slots iguais ao inventario do Terraria).</summary>
	public static class TerraprintStyle
	{
		// Tema escuro "vidro": fundo quase preto azulado, cartoes um pouco mais claros, destaque dourado.
		public static readonly Color PanelBackground = new Color(16, 20, 36) * 0.94f;
		public static readonly Color Sidebar = new Color(10, 13, 25) * 0.85f;
		public static readonly Color Card = new Color(30, 36, 60) * 0.85f;
		public static readonly Color CardHover = new Color(42, 50, 82) * 0.9f;
		public static readonly Color Accent = new(236, 184, 64);
		public static readonly Color AccentDim = new Color(236, 184, 64) * 0.35f;
		public static readonly Color Button = new Color(44, 54, 92) * 0.9f;
		public static readonly Color ButtonHover = new(66, 80, 132);
		public static readonly Color ButtonActive = new(190, 140, 40);
		public static readonly Color ButtonActiveHover = new(220, 168, 58);
		public static readonly Color ButtonDisabled = new Color(32, 36, 56) * 0.85f;
		public static readonly Color Row = new Color(30, 36, 60) * 0.75f;
		public static readonly Color RowHover = new Color(44, 52, 86) * 0.85f;
		public static readonly Color RowSelected = new(80, 92, 150);
		public static readonly Color PreviewBackground = new Color(8, 10, 20) * 0.8f;
		public static readonly Color Highlight = new(255, 222, 120);
		public static readonly Color PanelBorder = Color.Black;
		public static readonly Color Separator = new Color(120, 130, 170) * 0.35f;
		public static readonly Color Title = Color.White;
		public static readonly Color Muted = new(150, 160, 192);
		public static readonly Color InputBackground = new Color(8, 10, 22) * 0.85f;
		public static readonly Color Danger = new(180, 56, 56);
		public static readonly Color Good = new(110, 210, 130);

		public const float SlotSize = 40f;

		private static readonly Dictionary<string, Asset<Texture2D>> _icons = new();

		/// <summary>Icone 26x26 da pasta Assets/UI do mod.</summary>
		public static Texture2D Icon(string name)
		{
			if (!_icons.TryGetValue(name, out var asset))
				_icons[name] = asset = ModContent.Request<Texture2D>("Terraprint/Assets/UI/" + name, AssetRequestMode.ImmediateLoad);
			return asset.Value;
		}

		public static void Unload() => _icons.Clear();

		public static Texture2D Pixel => TextureAssets.MagicPixel.Value;

		public static void Fill(SpriteBatch sb, Rectangle r, Color c) => sb.Draw(Pixel, r, c);

		/// <summary>Texto com contorno, com a largura limitada (encolhe para caber).</summary>
		public static void Text(SpriteBatch sb, string text, Vector2 pos, Color color, float scale, float maxWidth = 0f, float originX = 0f)
		{
			if (string.IsNullOrEmpty(text))
				return;
			var size = FontAssets.MouseText.Value.MeasureString(text);
			if (maxWidth > 0f && size.X * scale > maxWidth)
				scale = maxWidth / size.X;
			Utils.DrawBorderString(sb, text, pos - new Vector2(size.X * scale * originX, 0f), color, scale);
		}

		/// <summary>Corta o texto com "..." para caber na largura.</summary>
		public static string Ellipsis(string text, float scale, float maxWidth)
		{
			var font = FontAssets.MouseText.Value;
			if (font.MeasureString(text).X * scale <= maxWidth)
				return text;
			while (text.Length > 1 && font.MeasureString(text + "…").X * scale > maxWidth)
				text = text.Substring(0, text.Length - 1);
			return text + "…";
		}

		/// <summary>Painel arredondado do jogo (fundo + borda preta) desenhado direto num retangulo.</summary>
		public static void RoundedBox(SpriteBatch sb, Rectangle r, Color background, Color border)
		{
			Utils.DrawSplicedPanel(sb, Main.Assets.Request<Texture2D>("Images/UI/PanelBackground").Value, r.X, r.Y, r.Width, r.Height, 12, 12, 12, 12, background);
			Utils.DrawSplicedPanel(sb, Main.Assets.Request<Texture2D>("Images/UI/PanelBorder").Value, r.X, r.Y, r.Width, r.Height, 12, 12, 12, 12, border);
		}

		public static void ApplyPanel(UIPanel panel)
		{
			panel.BackgroundColor = PanelBackground;
			panel.BorderColor = PanelBorder;
		}

		/// <summary>
		/// Fundo de slot do inventario, semitransparente como no jogo.
		/// active = slot selecionado (borda dourada), hovered = um pouco mais claro.
		/// </summary>
		public static void DrawSlot(SpriteBatch sb, Rectangle r, bool active, bool hovered, bool enabled, Color? tint = null)
		{
			Texture2D tex = active ? TextureAssets.InventoryBack14.Value : TextureAssets.InventoryBack.Value;
			Color color = tint ?? Color.White;
			float alpha = !enabled ? 0.4f : active ? 0.95f : hovered ? 0.9f : 0.7f;
			if (!enabled)
				color = new Color(160, 160, 180);
			sb.Draw(tex, r, color * alpha);
		}

		/// <summary>Opacidade do icone dentro de um slot: semitransparente em repouso, cheio ao passar o mouse.</summary>
		public static float IconAlpha(bool active, bool hovered, bool enabled) => !enabled ? 0.3f : active || hovered ? 1f : 0.75f;

		/// <summary>Linha horizontal fina, como a que separa as abas do conteudo na tela de crafting.</summary>
		public static void DrawSeparator(SpriteBatch sb, float x, float y, float width)
		{
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)x, (int)y, (int)width, 2), Separator);
		}

		/// <summary>Numero no canto do slot, com a fonte de quantidade de itens do jogo.</summary>
		public static void DrawStackNumber(SpriteBatch sb, string text, Rectangle slot, Color color)
		{
			var font = FontAssets.ItemStack.Value;
			Utils.DrawBorderStringFourWay(sb, font, text, slot.X + 6, slot.Bottom - 20, color, Color.Black, Vector2.Zero, 0.75f);
		}
	}

	/// <summary>Painel do Terraprint: fundo azul e uma linha separadora opcional.</summary>
	public class UITerraprintPanel : UIDragPanel
	{
		/// <summary>Altura (a partir do topo interno) onde desenhar a linha separadora; negativo = sem linha.</summary>
		public float SeparatorY = -1f;

		public UITerraprintPanel()
		{
			TerraprintStyle.ApplyPanel(this);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			if (SeparatorY >= 0f) {
				var inner = GetInnerDimensions();
				TerraprintStyle.DrawSeparator(spriteBatch, inner.X, inner.Y + SeparatorY, inner.Width);
			}
		}
	}
}
