using Terraria.Localization;

namespace Terraprint.Common
{
	internal static class Lang
	{
		/// <summary>Texto localizado de Mods.Terraprint.UI.{key}.</summary>
		public static string UI(string key, params object[] args) => Language.GetTextValue("Mods.Terraprint.UI." + key, args);
	}
}
