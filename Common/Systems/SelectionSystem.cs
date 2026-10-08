using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraprint.Common.Blueprints;
using Terraprint.Common.Configs;

namespace Terraprint.Common.Systems
{
	/// <summary>Ferramenta de selecao de area: dois cliques definem os cantos do retangulo a salvar.</summary>
	public class SelectionSystem : ModSystem
	{
		public static bool Active { get; private set; }
		public static Point? CornerA { get; private set; }
		public static Point? CornerB { get; private set; }

		public static bool HasSelection => CornerA.HasValue && CornerB.HasValue;

		public static Rectangle SelectionRect => Normalize(CornerA.Value, CornerB ?? Main.MouseWorld.ToTileCoordinates());

		public static Rectangle Normalize(Point a, Point b)
		{
			int left = System.Math.Min(a.X, b.X), top = System.Math.Min(a.Y, b.Y);
			return new Rectangle(left, top, System.Math.Abs(a.X - b.X) + 1, System.Math.Abs(a.Y - b.Y) + 1);
		}

		public static void Begin()
		{
			ProjectionSystem.CancelMoving();
			Players.TerraprintPlayer.SwallowClick();
			Active = true;
			CornerA = null;
			CornerB = null;
			Lang.Info(Lang.UI("SelectStart"), Color.LightSkyBlue);
		}

		public static void Cancel()
		{
			Active = false;
			CornerA = null;
			CornerB = null;
		}

		/// <summary>Chamado pelo jogador local quando ha um clique esquerdo no mundo.</summary>
		public static void Click(Point tile)
		{
			if (!Active)
				return;

			if (!CornerA.HasValue) {
				CornerA = tile;
				Lang.Info(Lang.UI("SelectSecond"), Color.LightSkyBlue);
				return;
			}

			var r = Normalize(CornerA.Value, tile);
			int max = TerraprintConfig.Instance.MaxBlueprintSize;
			if (r.Width > max || r.Height > max) {
				Main.NewText(Lang.UI("SelectTooBig", r.Width, r.Height, max), Color.OrangeRed);
				return;
			}

			CornerB = tile;
			Active = false;
			Lang.Info(Lang.UI("SelectDone", r.Width, r.Height), Color.LightGreen);
		}

		public static Blueprint CaptureSelection(string name)
		{
			var r = SelectionRect;
			var cfg = TerraprintConfig.Instance;
			var bp = Blueprint.Capture(r.Left, r.Top, r.Right - 1, r.Bottom - 1, cfg.CaptureLiquids, cfg.CapturePaint);
			bp.Name = name;
			bp.Author = Main.LocalPlayer?.name ?? "";
			return bp;
		}

		public override void OnWorldUnload() => Cancel();

		public override void PostDrawTiles()
		{
			if (!CornerA.HasValue)
				return;

			var r = SelectionRect;
			var sb = Main.spriteBatch;
			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None,
				Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			var screen = new Rectangle(r.X * 16 - (int)Main.screenPosition.X, r.Y * 16 - (int)Main.screenPosition.Y, r.Width * 16, r.Height * 16);
			DrawRect(sb, screen, Color.DeepSkyBlue * 0.15f, Color.DeepSkyBlue, 2);

			string label = $"{r.Width} x {r.Height}";
			Utils.DrawBorderString(sb, label, new Vector2(screen.X, screen.Y - 22), Color.White);
			sb.End();
		}

		public static void DrawRect(SpriteBatch sb, Rectangle r, Color fill, Color border, int thickness)
		{
			var px = TextureAssets.MagicPixel.Value;
			if (fill.A > 0)
				sb.Draw(px, r, fill);
			sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, thickness), border);
			sb.Draw(px, new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), border);
			sb.Draw(px, new Rectangle(r.X, r.Y, thickness, r.Height), border);
			sb.Draw(px, new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), border);
		}
	}
}
