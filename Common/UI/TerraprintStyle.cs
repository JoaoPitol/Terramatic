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
		public static readonly Color PanelBackground = new Color(33, 70, 125) * 0.95f;
		public static readonly Color PanelBorder = new(13, 25, 48);
		public static readonly Color Separator = new(82, 146, 217);
		public static readonly Color Title = Color.White;
		public static readonly Color Muted = new(170, 190, 220);
		public static readonly Color InputBackground = new Color(18, 40, 76) * 0.95f;

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

		/// <summary>Fundo de slot do inventario. active = borda dourada (como o item selecionado na hotbar).</summary>
		public static void DrawSlot(SpriteBatch sb, Rectangle r, bool active, bool hovered, bool enabled, Color? tint = null)
		{
			Texture2D tex = active ? TextureAssets.InventoryBack14.Value : TextureAssets.InventoryBack.Value;
			Color color = tint ?? Color.White;
			if (!enabled)
				color = new Color(110, 110, 120) * 0.8f;
			else if (hovered)
				color = Color.Lerp(color, Color.White, 0.35f);
			else
				color *= 0.9f;
			sb.Draw(tex, r, color);
		}

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
