using System.ComponentModel;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Terraprint.Common.Configs
{
	/// <summary>Opcoes do Terraprint (Configuracoes > Mods > Terraprint, ou o botao Opcoes no menu do mod).</summary>
	public class TerraprintConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		public static TerraprintConfig Instance => ModContent.GetInstance<TerraprintConfig>();

		public enum Anchor { BottomCenter, Center, TopLeft }

		[Header("General")]
		[DefaultValue(true)]
		public bool ShowChatMessages;

		[DefaultValue(true)]
		public bool CloseMenuOnLoad;

		[DefaultValue(Anchor.BottomCenter)]
		[DrawTicks]
		public Anchor PlacementAnchor;

		[Header("Capture")]
		[DefaultValue(true)]
		public bool CaptureLiquids;

		[DefaultValue(true)]
		public bool CapturePaint;

		[Range(10, 1000)]
		[DefaultValue(400)]
		public int MaxBlueprintSize;

		[Header("Ghost")]
		[DefaultValue(true)]
		public bool ShowGhost;

		[Range(0.1f, 1f)]
		[Increment(0.05f)]
		[DefaultValue(0.55f)]
		public float GhostOpacity;

		[DefaultValue(true)]
		public bool ShowWalls;

		[DefaultValue(false)]
		public bool ShowLiquids;

		[DefaultValue(false)]
		public bool ShowCorrect;

		[DefaultValue(true)]
		public bool MarkObstructions;

		[DefaultValue(true)]
		public bool CheckSlopes;

		[DefaultValue(false)]
		public bool CheckPaint;

		[DefaultValue(true)]
		public bool ShowOutline;

		[DefaultValue(true)]
		public bool ShowProgress;

		[Header("Map")]
		[DefaultValue(true)]
		public bool ShowOnMinimap;

		[DefaultValue(true)]
		public bool ShowOnFullscreenMap;

		[Range(0.1f, 1f)]
		[Increment(0.05f)]
		[DefaultValue(0.75f)]
		public float MapOpacity;

		[Header("Colors")]
		[DefaultValue(typeof(Color), "120, 200, 255, 255")]
		public Color ColorMissing;

		[DefaultValue(typeof(Color), "255, 70, 70, 255")]
		public Color ColorWrong;

		[DefaultValue(typeof(Color), "90, 255, 120, 255")]
		public Color ColorCorrect;

		/// <summary>Contador que muda sempre que alguma opcao e salva (o fantasma e o mapa se redesenham).</summary>
		public static int ChangeCount { get; private set; }

		public override void OnChanged() => ChangeCount++;

		/// <summary>Altera uma opcao pelo menu do mod e grava no arquivo de configuracao.</summary>
		public static void Set(System.Action<TerraprintConfig> change)
		{
			var pending = (TerraprintConfig)ConfigManager.GeneratePopulatedClone(Instance);
			change(pending);
			Instance.SaveChanges(pending, silent: true);
		}
	}
}
