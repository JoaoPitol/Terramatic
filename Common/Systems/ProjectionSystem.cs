using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraprint.Common.Blueprints;

namespace Terraprint.Common.Systems
{
	/// <summary>
	/// O blueprint carregado e onde ele esta no mundo.
	/// Moving: o fantasma segue o mouse; clique esquerdo fixa. Fixed: fica parado e e salvo junto com o mundo.
	/// </summary>
	public class ProjectionSystem : ModSystem
	{
		public enum ProjectionState { None, Moving, Fixed }

		public static ProjectionState State { get; private set; }

		/// <summary>Blueprint como foi carregado do arquivo (sem transformacoes).</summary>
		public static Blueprint Source { get; private set; }

		/// <summary>Blueprint ja girado/espelhado, que e o que aparece no mundo.</summary>
		public static Blueprint Current { get; private set; }

		public static int Rotation { get; private set; }
		public static bool FlippedX { get; private set; }
		public static int DroppedTiles { get; private set; }

		/// <summary>Canto superior esquerdo do blueprint no mundo, em tiles.</summary>
		public static Point Origin { get; private set; }

		/// <summary>Incrementa sempre que posicao ou conteudo mudam (o fantasma usa para recalcular).</summary>
		public static int Version { get; private set; }

		public static bool HasProjection => State != ProjectionState.None && Current != null;

		public static Rectangle Area => HasProjection ? new Rectangle(Origin.X, Origin.Y, Current.Width, Current.Height) : Rectangle.Empty;

		public static void Load(Blueprint bp)
		{
			Source = bp;
			Current = null;
			_originBeforeMove = null;
			Rotation = 0;
			FlippedX = false;
			Rebuild();
			State = ProjectionState.Moving;
			SelectionSystem.Cancel();
			Main.NewText(Lang.UI("PlacingStart", bp.Name), Color.LightSkyBlue);
			if (bp.UnknownTypes > 0)
				Main.NewText(Lang.UI("UnknownTypes", bp.UnknownTypes), Color.Orange);
		}

		public static void Clear()
		{
			State = ProjectionState.None;
			Source = null;
			Current = null;
			Version++;
		}

		private static void Rebuild()
		{
			// Mantem o centro no mesmo lugar ao girar.
			Point center = Current != null ? new Point(Origin.X + Current.Width / 2, Origin.Y + Current.Height / 2) : Origin;
			Current = BlueprintTransform.Apply(Source, Rotation, FlippedX, out int dropped);
			DroppedTiles = dropped;
			if (State == ProjectionState.Fixed)
				Origin = new Point(center.X - Current.Width / 2, center.Y - Current.Height / 2);
			Version++;
		}

		public static void Rotate()
		{
			if (Source == null)
				return;
			Rotation = (Rotation + 1) % 4;
			Rebuild();
			if (DroppedTiles > 0)
				Main.NewText(Lang.UI("RotateDropped", DroppedTiles), Color.Orange);
		}

		public static void Flip()
		{
			if (Source == null)
				return;
			FlippedX = !FlippedX;
			Rebuild();
		}

		private static Point? _originBeforeMove;

		public static void StartMoving()
		{
			if (Source == null)
				return;
			_originBeforeMove = State == ProjectionState.Fixed ? Origin : null;
			State = ProjectionState.Moving;
			Main.NewText(Lang.UI("MovingStart"), Color.LightSkyBlue);
		}

		/// <summary>Botao direito enquanto move: volta para onde estava, ou descarta se nunca foi fixado.</summary>
		public static void CancelMoving()
		{
			if (State != ProjectionState.Moving)
				return;
			if (_originBeforeMove.HasValue) {
				Origin = _originBeforeMove.Value;
				State = ProjectionState.Fixed;
				Version++;
			}
			else
				Clear();
			_originBeforeMove = null;
			Main.NewText(Lang.UI("Cancelled"));
		}

		public static void Nudge(int dx, int dy)
		{
			if (!HasProjection)
				return;
			Origin = new Point(Origin.X + dx, Origin.Y + dy);
			Version++;
		}

		/// <summary>Posicao do fantasma quando ele segue o mouse: o mouse fica no centro da base.</summary>
		public static Point OriginAtMouse()
		{
			Point m = Main.MouseWorld.ToTileCoordinates();
			return new Point(m.X - Current.Width / 2, m.Y - Current.Height + 1);
		}

		public static void Fix()
		{
			if (State != ProjectionState.Moving)
				return;
			Origin = OriginAtMouse();
			State = ProjectionState.Fixed;
			Version++;
			Main.NewText(Lang.UI("Fixed", Origin.X, Origin.Y), Color.LightGreen);
		}

		public override void PostUpdateEverything()
		{
			if (State == ProjectionState.Moving && Current != null && !Main.dedServ) {
				Point o = OriginAtMouse();
				if (o != Origin) {
					Origin = o;
					Version++;
				}
			}
		}

		public override void ClearWorld() => Clear();

		public override void SaveWorldData(TagCompound tag)
		{
			if (!HasProjection)
				return;
			tag["terraprint"] = new TagCompound {
				["blueprint"] = Source.Save(),
				["rotation"] = Rotation,
				["flipX"] = FlippedX,
				["x"] = Origin.X,
				["y"] = Origin.Y,
			};
		}

		public override void LoadWorldData(TagCompound tag)
		{
			Clear();
			if (!tag.TryGet("terraprint", out TagCompound p))
				return;
			try {
				Source = Blueprint.Load(p.GetCompound("blueprint"));
				Rotation = p.GetInt("rotation");
				FlippedX = p.GetBool("flipX");
				Current = BlueprintTransform.Apply(Source, Rotation, FlippedX, out int dropped);
				DroppedTiles = dropped;
				Origin = new Point(p.GetInt("x"), p.GetInt("y"));
				State = ProjectionState.Fixed;
				Version++;
			}
			catch (System.Exception e) {
				Mod.Logger.Warn("Could not load the saved Terraprint projection", e);
				Clear();
			}
		}
	}
}
