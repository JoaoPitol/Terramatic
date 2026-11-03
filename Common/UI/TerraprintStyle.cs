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
		public static readonly Color PanelBackground = new Color(44, 57, 112) * 0.94f;
		public static readonly Color Button = new Color(63, 82, 151) * 0.9f;
		public static readonly Color ButtonHover = new(88, 110, 190);
		public static readonly Color ButtonActive = new(200, 150, 35);
		public static readonly Color ButtonActiveHover = new(225, 175, 55);
		public static readonly Color ButtonDisabled = new Color(45, 52, 85) * 0.85f;
		public static readonly Color Row = new Color(63, 82, 151) * 0.75f;
		public static readonly Color RowHover = new Color(78, 100, 178) * 0.85f;
		public static readonly Color RowSelected = new(110, 135, 220);
		public static readonly Color PreviewBackground = new Color(30, 48, 100) * 0.85f;
		public static readonly Color Highlight = new(255, 235, 110);
		public static readonly Color PanelBorder = Color.Black;
		public static readonly Color Separator = new(82, 146, 217);
		public static readonly Color Title = Color.White;
		public static readonly Color Muted = new(170, 190, 220);
		public static readonly Color InputBackground = new Color(22, 42, 88) * 0.85f;

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
