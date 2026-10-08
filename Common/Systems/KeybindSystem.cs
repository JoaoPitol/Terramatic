using Terraria.ModLoader;

namespace Terraprint.Common.Systems
{
	public class KeybindSystem : ModSystem
	{
		public static ModKeybind ToggleMenu { get; private set; }

		public override void Load()
		{
			// Teclas sem uso no Terraria vanilla; podem ser trocadas em Configuracoes > Controles.
			ToggleMenu = KeybindLoader.RegisterKeybind(Mod, "ToggleMenu", "K");
		}

		public override void Unload()
		{
			ToggleMenu = null;
		}
	}
}
