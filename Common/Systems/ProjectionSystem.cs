using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraprint.Common.Blueprints;

namespace Terraprint.Common.Systems
{
	/// <summary>
	/// O blueprint carregado e onde ele esta no mundo. Cada jogador tem a sua projecao (vale tambem no multiplayer).
	/// Moving: o fantasma segue o mouse; clique esquerdo fixa. Fixed: fica parado e e salvo num arquivo
	/// do jogador, um por mundo (pasta Terraprint/Projections), para voltar quando ele entrar de novo.
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
			Players.TerraprintPlayer.SwallowClick();
			Lang.Info(Lang.UI("PlacingStart", bp.Name), Color.LightSkyBlue);
			if (bp.UnknownTypes > 0)
				Main.NewText(Lang.UI("UnknownTypes", bp.UnknownTypes), Color.Orange);
		}

		public static void Clear()
		{
			ClearMemory();
			MarkDirty();
		}

		private static void ClearMemory()
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
			if (State == ProjectionState.Fixed) {
				Origin = new Point(center.X - Current.Width / 2, center.Y - Current.Height / 2);
				MarkDirty();
			}
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
			Players.TerraprintPlayer.SwallowClick();
			Lang.Info(Lang.UI("MovingStart"), Color.LightSkyBlue);
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
				MarkDirty();
			}
			else
				Clear();
			_originBeforeMove = null;
			Lang.Info(Lang.UI("Cancelled"));
		}

		public static void Nudge(int dx, int dy)
		{
			if (!HasProjection)
				return;
			Origin = new Point(Origin.X + dx, Origin.Y + dy);
			Version++;
			if (State == ProjectionState.Fixed)
				MarkDirty();
		}

		/// <summary>Posicao do fantasma quando ele segue o mouse (ponto de ancoragem escolhido nas opcoes).</summary>
		public static Point OriginAtMouse()
		{
			Point m = Main.MouseWorld.ToTileCoordinates();
			return Configs.TerraprintConfig.Instance.PlacementAnchor switch {
				Configs.TerraprintConfig.Anchor.Center => new Point(m.X - Current.Width / 2, m.Y - Current.Height / 2),
				Configs.TerraprintConfig.Anchor.TopLeft => m,
				_ => new Point(m.X - Current.Width / 2, m.Y - Current.Height + 1),
			};
		}

		public static void Fix()
		{
			if (State != ProjectionState.Moving)
				return;
			Origin = OriginAtMouse();
			State = ProjectionState.Fixed;
			Version++;
			MarkDirty();
			Lang.Info(Lang.UI("Fixed", Origin.X, Origin.Y), Color.LightGreen);
		}

		public override void PostUpdateEverything()
		{
			if (Main.dedServ)
				return;
			if (State == ProjectionState.Moving && Current != null) {
				Point o = OriginAtMouse();
				if (o != Origin) {
					Origin = o;
					Version++;
				}
			}
			// Grava a projecao um pouco depois da ultima mudanca (mover com as setas nao grava a cada passo).
			if (_saveTimer > 0 && --_saveTimer == 0)
				SaveToFile();
		}

		// ---------- Gravacao por jogador e por mundo ----------

		private const int SaveDelayTicks = 60;
		private static int _saveTimer;
		/// <summary>Arquivo da projecao do mundo atual (definido ao entrar no mundo).</summary>
		private static string _file;

		private static void MarkDirty()
		{
			if (_file != null)
				_saveTimer = SaveDelayTicks;
		}

		private static string FileFor(string worldName, System.Guid worldId)
		{
			string name = BlueprintIO.SanitizeFileName(worldName) + "_" + worldId.ToString("N").Substring(0, 8);
			return System.IO.Path.Combine(BlueprintIO.Folder, "Projections", name + ".tag");
		}

		/// <summary>Chamado quando o jogador local entra num mundo (single player ou servidor).</summary>
		public static void OnEnterWorld()
		{
			_file = FileFor(Main.worldName, Main.ActiveWorldFileData.UniqueId);
			_saveTimer = 0;
			if (System.IO.File.Exists(_file)) {
				try {
					var tag = TagIO.FromFile(_file, compressed: true);
					ClearMemory();
					ReadTag(tag);
				}
				catch (System.Exception e) {
					ModContent.GetInstance<Terraprint>().Logger.Warn("Could not load the saved Terraprint projection", e);
					ClearMemory();
				}
			}
			else if (HasProjection)
				SaveToFile(); // projecao antiga, que vinha dentro do arquivo do mundo
		}

		private static void SaveToFile()
		{
			_saveTimer = 0;
			if (_file == null)
				return;
			try {
				if (State != ProjectionState.Fixed || Source == null) {
					// Removida ou ainda seguindo o mouse sem nunca ter sido fixada.
					if (State == ProjectionState.None && System.IO.File.Exists(_file))
						System.IO.File.Delete(_file);
					return;
				}
				System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_file));
				TagIO.ToFile(WriteTag(), _file, compress: true);
			}
			catch (System.Exception e) {
				ModContent.GetInstance<Terraprint>().Logger.Warn("Could not save the Terraprint projection", e);
			}
		}

		private static TagCompound WriteTag() => new() {
			["blueprint"] = Source.Save(),
			["rotation"] = Rotation,
			["flipX"] = FlippedX,
			["x"] = Origin.X,
			["y"] = Origin.Y,
		};

		private static void ReadTag(TagCompound p)
		{
			Source = Blueprint.Load(p.GetCompound("blueprint"));
			Rotation = p.GetInt("rotation");
			FlippedX = p.GetBool("flipX");
			Current = BlueprintTransform.Apply(Source, Rotation, FlippedX, out int dropped);
			DroppedTiles = dropped;
			Origin = new Point(p.GetInt("x"), p.GetInt("y"));
			State = ProjectionState.Fixed;
			Version++;
		}

		// Ao sair do mundo: grava o que estiver pendente antes de limpar a memoria.
		public override void OnWorldUnload() => FlushAndForget();

		public override void ClearWorld()
		{
			FlushAndForget();
			ClearMemory();
		}

		private static void FlushAndForget()
		{
			if (_saveTimer > 0)
				SaveToFile();
			_file = null;
		}

		// Versoes antigas guardavam a projecao dentro do arquivo do mundo. Ela e lida uma vez e
		// passa para o arquivo do jogador ao entrar no mundo; o mundo nao guarda mais nada.
		public override void LoadWorldData(TagCompound tag)
		{
			if (Main.dedServ || !tag.TryGet("terraprint", out TagCompound p))
				return;
			try {
				ClearMemory();
				ReadTag(p);
			}
			catch (System.Exception e) {
				Mod.Logger.Warn("Could not load the old Terraprint projection", e);
				ClearMemory();
			}
		}
	}
}
