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
		private static UserInterface _materialsInterface;
		private static MaterialsPanel _materials;
		private static GameTime _lastTime;

		public static bool MenuOpen => _interface?.CurrentState != null;
		public static bool MaterialsOpen => _materialsInterface?.CurrentState != null;
		public static bool MouseOverMenu => MenuOpen && _menu.IsMouseOverPanel || MaterialsOpen && _materials.IsMouseOverPanel;
		public static bool Typing => MenuOpen && _menu.IsTyping;
		public static TerraprintMenu Menu => _menu;

		public override void Load()
		{
			_interface = new UserInterface();
			// SetState ativa (e monta) o menu na primeira abertura, quando os textos ja estao carregados.
			_menu = new TerraprintMenu();
			_materialsInterface = new UserInterface();
			_materials = new MaterialsPanel();
		}

		public override void Unload()
		{
			_interface = null;
			_menu = null;
			_materialsInterface = null;
			_materials = null;
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

		/// <summary>A lista de materiais e uma janela separada: continua aberta com o menu fechado.</summary>
		public static void ToggleMaterials() => _materialsInterface?.SetState(MaterialsOpen ? null : _materials);

		public static void CloseMaterials() => _materialsInterface?.SetState(null);

		public override void OnWorldUnload()
		{
			CloseMenu();
			CloseMaterials();
		}

		public override void UpdateUI(GameTime gameTime)
		{
			_lastTime = gameTime;
			if (MaterialsOpen)
				_materialsInterface.Update(gameTime);
			if (MenuOpen)
				_interface.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			if (index < 0)
				return;
			layers.Insert(index, new LegacyGameInterfaceLayer("Terraprint: Menu", () => {
				if (MaterialsOpen)
					_materialsInterface.Draw(Main.spriteBatch, _lastTime);
				if (MenuOpen)
					_interface.Draw(Main.spriteBatch, _lastTime);
				return true;
			}, InterfaceScaleType.UI));
		}
	}
}
