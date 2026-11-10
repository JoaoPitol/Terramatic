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
	/// <summary>
	/// Cores e desenhos comuns do visual do Terraprint, imitando os paineis do proprio Terraria
	/// (inventario e tela de configuracoes): paineis arredondados do jogo, slots com relevo,
	/// abas, divisores e a fonte do jogo. As texturas sao as do jogo, carregadas em tempo de execucao.
	/// </summary>
	public static class TerraprintStyle
	{
		// ----- Paleta base -----
		public static readonly Color OuterBackground = new(16, 26, 39);    // #101A27
		public static readonly Color MainBackground = new(25, 63, 100);    // #193F64
		public static readonly Color HeaderColor = new(55, 107, 153);      // #376B99
		public static readonly Color TabColor = new(71, 127, 163);         // #477FA3
		public static readonly Color SlotColor = new(23, 70, 107);         // #17466B
		public static readonly Color BorderColor = new(16, 24, 32);        // #101820
		public static readonly Color TextColor = new(230, 224, 203);       // #E6E0CB
		public static readonly Color HighlightColor = new(230, 199, 90);   // #E6C75A

		// ----- Cores derivadas (nomes usados pelo resto da interface) -----
		public static readonly Color PanelBackground = MainBackground * 0.96f;
		public static readonly Color Sidebar = OuterBackground * 0.7f;
		public static readonly Color Card = Color.Lerp(MainBackground, HeaderColor, 0.3f) * 0.92f;
		public static readonly Color CardHover = Color.Lerp(MainBackground, HeaderColor, 0.65f);
		public static readonly Color Accent = HighlightColor;
		public static readonly Color AccentDim = HighlightColor * 0.35f;
		public static readonly Color Button = HeaderColor * 0.95f;
		public static readonly Color ButtonHover = TabColor;
		public static readonly Color ButtonActive = TabColor;
		public static readonly Color ButtonActiveHover = Color.Lerp(TabColor, Color.White, 0.12f);
		public static readonly Color ButtonDisabled = Color.Lerp(OuterBackground, MainBackground, 0.55f) * 0.9f;
		public static readonly Color TabIdle = Color.Lerp(MainBackground, TabColor, 0.35f) * 0.95f;
		public static readonly Color ToggleOn = new(196, 164, 64);
		public static readonly Color Row = Card;
		public static readonly Color RowHover = CardHover;
		public static readonly Color RowSelected = TabColor;
		public static readonly Color PreviewBackground = OuterBackground * 0.9f;
		public static readonly Color Highlight = HighlightColor;
		public static readonly Color PanelBorder = BorderColor;
		public static readonly Color Separator = Color.Lerp(Color.White, HeaderColor, 0.85f) * 0.9f;
		public static readonly Color Title = TextColor;
		public static readonly Color Muted = Color.Lerp(TextColor, HeaderColor, 0.5f);
		public static readonly Color InputBackground = OuterBackground * 0.9f;
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

		private static Texture2D Vanilla(string path) => Main.Assets.Request<Texture2D>(path, AssetRequestMode.ImmediateLoad).Value;

		public static Texture2D PanelBackgroundTexture => Vanilla("Images/UI/PanelBackground");
		public static Texture2D PanelBorderTexture => Vanilla("Images/UI/PanelBorder");
		public static Texture2D DividerTexture => Vanilla("Images/UI/Divider");

		public static void Fill(SpriteBatch sb, Rectangle r, Color c) => sb.Draw(Pixel, r, c);

		/// <summary>Texto com contorno (fonte do jogo), com a largura limitada (encolhe para caber).</summary>
		public static void Text(SpriteBatch sb, string text, Vector2 pos, Color color, float scale, float maxWidth = 0f, float originX = 0f)
		{
			if (string.IsNullOrEmpty(text))
				return;
			var size = FontAssets.MouseText.Value.MeasureString(text);
			if (maxWidth > 0f && size.X * scale > maxWidth)
				scale = maxWidth / size.X;
			Utils.DrawBorderString(sb, text, pos - new Vector2(size.X * scale * originX, 0f), color, scale);
		}

		/// <summary>
		/// Titulo grande com a fonte dos titulos do jogo (a mesma de "Configuracoes" e dos menus principais),
		/// centralizado na vertical em centerY.
		/// </summary>
		public static void TitleText(SpriteBatch sb, string text, float x, float centerY, Color color, float scale, float maxWidth = 0f, float originX = 0f)
		{
			if (string.IsNullOrEmpty(text))
				return;
			var font = FontAssets.DeathText.Value;
			var size = font.MeasureString(text);
			if (maxWidth > 0f && size.X * scale > maxWidth)
				scale = maxWidth / size.X;
			float y = centerY - size.Y * scale / 2f + 4f * scale;
			Utils.DrawBorderStringFourWay(sb, font, text, x - size.X * scale * originX, y, color, Color.Black, Vector2.Zero, scale);
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

		/// <summary>Painel arredondado do jogo (fundo + borda) desenhado direto num retangulo.</summary>
		public static void RoundedBox(SpriteBatch sb, Rectangle r, Color background, Color border)
		{
			Utils.DrawSplicedPanel(sb, PanelBackgroundTexture, r.X, r.Y, r.Width, r.Height, 12, 12, 12, 12, background);
			Utils.DrawSplicedPanel(sb, PanelBorderTexture, r.X, r.Y, r.Width, r.Height, 12, 12, 12, 12, border);
		}

		/// <summary>Faixa de cabecalho (como a barra de titulo dos menus do jogo).</summary>
		public static void DrawHeader(SpriteBatch sb, Rectangle r)
		{
			RoundedBox(sb, r, HeaderColor, BorderColor);
			// Leve brilho na metade de cima, como o relevo dos paineis do jogo.
			Fill(sb, new Rectangle(r.X + 6, r.Y + 3, r.Width - 12, 2), Color.White * 0.08f);
		}

		public static void ApplyPanel(UIPanel panel)
		{
			panel.BackgroundColor = PanelBackground;
			panel.BorderColor = PanelBorder;
		}

		/// <summary>
		/// Slot no estilo do inventario: fundo na cor de slot, o relevo da textura de slot do jogo por cima
		/// e borda escura (dourada quando selecionado). hovered = um pouco mais claro.
		/// tint (opcional) puxa o fundo para outra cor (ex.: vermelho para remover, verde para pronto).
		/// </summary>
		public static void DrawSlot(SpriteBatch sb, Rectangle r, bool active, bool hovered, bool enabled, Color? tint = null)
		{
			Color fill = SlotColor;
			if (tint.HasValue)
				fill = Color.Lerp(fill, tint.Value, 0.4f);
			if (active)
				fill = Color.Lerp(fill, TabColor, 0.55f);
			else if (hovered)
				fill = Color.Lerp(fill, TabColor, 0.3f);
			float alpha = !enabled ? 0.45f : active || hovered ? 1f : 0.88f;

			Utils.DrawSplicedPanel(sb, PanelBackgroundTexture, r.X, r.Y, r.Width, r.Height, 12, 12, 12, 12, fill * alpha);
			// Relevo do slot do jogo (borda interna e sombreado), bem suave para manter a cor da paleta.
			sb.Draw(TextureAssets.InventoryBack.Value, new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4), Color.White * (0.16f * alpha));
			Utils.DrawSplicedPanel(sb, PanelBorderTexture, r.X, r.Y, r.Width, r.Height, 12, 12, 12, 12,
				active ? HighlightColor : hovered ? Color.Lerp(BorderColor, TextColor, 0.25f) : BorderColor * alpha);
		}

		/// <summary>Opacidade do icone dentro de um slot: semitransparente em repouso, cheio ao passar o mouse.</summary>
		public static float IconAlpha(bool active, bool hovered, bool enabled) => !enabled ? 0.3f : active || hovered ? 1f : 0.85f;

		/// <summary>Divisor horizontal do jogo (o mesmo das listas de mundos/personagens).</summary>
		public static void DrawSeparator(SpriteBatch sb, float x, float y, float width)
		{
			var tex = DividerTexture;
			int edge = 2;
			int h = tex.Height;
			int w = (int)width;
			if (w < edge * 2)
				return;
			sb.Draw(tex, new Rectangle((int)x, (int)y, edge, h), new Rectangle(0, 0, edge, h), Separator);
			sb.Draw(tex, new Rectangle((int)x + edge, (int)y, w - 2 * edge, h), new Rectangle(edge, 0, tex.Width - 2 * edge, h), Separator);
			sb.Draw(tex, new Rectangle((int)x + w - edge, (int)y, edge, h), new Rectangle(tex.Width - edge, 0, edge, h), Separator);
		}

		/// <summary>Numero no canto do slot, com a fonte de quantidade de itens do jogo.</summary>
		public static void DrawStackNumber(SpriteBatch sb, string text, Rectangle slot, Color color)
		{
			var font = FontAssets.ItemStack.Value;
			Utils.DrawBorderStringFourWay(sb, font, text, slot.X + 6, slot.Bottom - 20, color, Color.Black, Vector2.Zero, 0.75f);
		}
	}

	/// <summary>Painel do Terraprint: fundo da paleta, faixa de cabecalho e uma linha separadora opcionais.</summary>
	public class UITerraprintPanel : UIDragPanel
	{
		/// <summary>Altura (a partir do topo interno) onde desenhar a linha separadora; negativo = sem linha.</summary>
		public float SeparatorY = -1f;

		/// <summary>Altura da faixa de cabecalho no topo do painel; 0 = sem faixa.</summary>
		public float HeaderHeight;

		/// <summary>Distancia da faixa de cabecalho ate a borda do painel.</summary>
		public float HeaderInset = 6f;

		public UITerraprintPanel()
		{
			TerraprintStyle.ApplyPanel(this);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			if (HeaderHeight > 0f) {
				var d = GetDimensions();
				var r = new Rectangle((int)(d.X + HeaderInset), (int)(d.Y + HeaderInset), (int)(d.Width - 2 * HeaderInset), (int)HeaderHeight);
				TerraprintStyle.DrawHeader(spriteBatch, r);
			}
			if (SeparatorY >= 0f) {
				var inner = GetInnerDimensions();
				TerraprintStyle.DrawSeparator(spriteBatch, inner.X, inner.Y + SeparatorY, inner.Width);
			}
		}
	}
}
