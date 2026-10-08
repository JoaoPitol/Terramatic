using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Terraprint.Common.Configs
{
	/// <summary>Opcoes do Terraprint (Configuracoes > Mods > Terraprint, ou o botao Opcoes no menu do mod).</summary>
	public class TerraprintConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		public static TerraprintConfig Instance => ModContent.GetInstance<TerraprintConfig>();

		[Header("Capture")]
		[DefaultValue(true)]
		public bool CaptureLiquids;

		[DefaultValue(true)]
		public bool CapturePaint;

		[Range(10, 1000)]
		[DefaultValue(400)]
		public int MaxBlueprintSize;
	}
}
