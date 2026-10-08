using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using Terraprint.Common.UI;

namespace Terraprint.Common.Systems
{
	[Autoload(Side = ModSide.Client)]
	public class UISystem : ModSystem
	{
		private static UserInterface _interface;
		private static TerraprintMenu _menu;
		private static GameTime _lastTime;

		public static bool MenuOpen => _interface?.CurrentState != null;
		public static bool MouseOverMenu => MenuOpen && _menu.IsMouseOverPanel;
		public static TerraprintMenu Menu => _menu;

		public override void Load()
		{
			_interface = new UserInterface();
			_menu = new TerraprintMenu();
			_menu.Activate();
		}

		public override void Unload()
		{
			_interface = null;
			_menu = null;
		}

		public static void ToggleMenu()
		{
			if (MenuOpen)
				CloseMenu();
			else
				OpenMenu();
		}

		public static void OpenMenu()
		{
			_interface?.SetState(_menu);
			_menu?.RefreshFiles();
		}

		public static void CloseMenu() => _interface?.SetState(null);

		public override void OnWorldUnload() => CloseMenu();

		public override void UpdateUI(GameTime gameTime)
		{
			_lastTime = gameTime;
			if (MenuOpen)
				_interface.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			if (index < 0)
				return;
			layers.Insert(index, new LegacyGameInterfaceLayer("Terraprint: Menu", () => {
				if (MenuOpen)
					_interface.Draw(Main.spriteBatch, _lastTime);
				return true;
			}, InterfaceScaleType.UI));
		}
	}
}
