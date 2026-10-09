using Terraria.ModLoader;

namespace Terraprint.Common.Systems
{
	public class KeybindSystem : ModSystem
	{
		public static ModKeybind ToggleMenu { get; private set; }
		public static ModKeybind Rotate { get; private set; }
		public static ModKeybind Flip { get; private set; }
		public static ModKeybind NudgeUp { get; private set; }
		public static ModKeybind NudgeDown { get; private set; }
		public static ModKeybind NudgeLeft { get; private set; }
		public static ModKeybind NudgeRight { get; private set; }
		public static ModKeybind AutoBuild { get; private set; }

		public override void Load()
		{
			// Teclas sem uso no Terraria vanilla; podem ser trocadas em Configuracoes > Controles.
			ToggleMenu = KeybindLoader.RegisterKeybind(Mod, "ToggleMenu", "K");
			Rotate = KeybindLoader.RegisterKeybind(Mod, "Rotate", "OemPeriod");
			Flip = KeybindLoader.RegisterKeybind(Mod, "Flip", "OemComma");
			// As setas so movem o fantasma com o menu aberto (para nao brigar com outros usos).
			NudgeUp = KeybindLoader.RegisterKeybind(Mod, "NudgeUp", "Up");
			NudgeDown = KeybindLoader.RegisterKeybind(Mod, "NudgeDown", "Down");
			NudgeLeft = KeybindLoader.RegisterKeybind(Mod, "NudgeLeft", "Left");
			NudgeRight = KeybindLoader.RegisterKeybind(Mod, "NudgeRight", "Right");
			// Segurar para construir automaticamente (o mesmo que segurar o botao Construir do menu).
			AutoBuild = KeybindLoader.RegisterKeybind(Mod, "AutoBuild", "P");
		}

		public override void Unload()
		{
			ToggleMenu = Rotate = Flip = NudgeUp = NudgeDown = NudgeLeft = NudgeRight = AutoBuild = null;
		}
	}
}
