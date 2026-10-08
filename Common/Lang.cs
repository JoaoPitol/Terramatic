using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using Terraprint.Common.Configs;

namespace Terraprint.Common
{
	internal static class Lang
	{
		/// <summary>Texto localizado de Mods.Terraprint.UI.{key}.</summary>
		public static string UI(string key, params object[] args) => Language.GetTextValue("Mods.Terraprint.UI." + key, args);

		/// <summary>Mensagem informativa no chat (pode ser desligada nas opcoes). Avisos e erros usam Main.NewText direto.</summary>
		public static void Info(string text, Color? color = null)
		{
			if (TerraprintConfig.Instance.ShowChatMessages)
				Main.NewText(text, color ?? Color.White);
		}
	}
}
