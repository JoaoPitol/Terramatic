using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Terraprint.Common.Systems
{
	/// <summary>Desenha o fantasma do blueprint no mundo (versao simples: contorno e celulas).</summary>
	[Autoload(Side = ModSide.Client)]
	public class GhostSystem : ModSystem
	{
		public override void PostDrawTiles()
		{
			if (!ProjectionSystem.HasProjection)
				return;

			var bp = ProjectionSystem.Current;
			var o = ProjectionSystem.Origin;
			var sb = Main.spriteBatch;
			var px = TextureAssets.MagicPixel.Value;
			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None,
				Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			for (int y = 0; y < bp.Height; y++) {
				for (int x = 0; x < bp.Width; x++) {
					ref var c = ref bp[x, y];
					if (c.IsEmpty)
						continue;
					var pos = new Vector2((o.X + x) * 16, (o.Y + y) * 16) - Main.screenPosition;
					var color = c.HasTile ? Color.DeepSkyBlue * 0.45f : Color.SteelBlue * 0.25f;
					sb.Draw(px, new Rectangle((int)pos.X, (int)pos.Y, 16, 16), color);
				}
			}

			var r = new Rectangle(o.X * 16 - (int)Main.screenPosition.X, o.Y * 16 - (int)Main.screenPosition.Y, bp.Width * 16, bp.Height * 16);
			SelectionSystem.DrawRect(sb, r, Color.Transparent, Color.Gold * 0.8f, 2);
			sb.End();
		}
	}
}
