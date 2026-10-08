using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ModLoader.IO;

namespace Terraprint.Common.Blueprints
{
	/// <summary>Leitura e escrita de arquivos .tprint na pasta de blueprints.</summary>
	public static class BlueprintIO
	{
		/// <summary>Documents/My Games/Terraria/tModLoader/Terraprint</summary>
		public static string Folder => Path.Combine(Main.SavePath, "Terraprint");

		public static void EnsureFolder() => Directory.CreateDirectory(Folder);

		public static string SanitizeFileName(string name)
		{
			name = (name ?? "").Trim();
			foreach (char ch in Path.GetInvalidFileNameChars())
				name = name.Replace(ch, '_');
			if (name.Length == 0)
				name = "blueprint_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
			return name;
		}

		/// <summary>Grava o blueprint e devolve o caminho final. Nao sobrescreve: acrescenta (2), (3)...</summary>
		public static string Save(Blueprint bp)
		{
			EnsureFolder();
			string baseName = SanitizeFileName(bp.Name);
			string path = Path.Combine(Folder, baseName + Blueprint.Extension);
			for (int i = 2; File.Exists(path); i++)
				path = Path.Combine(Folder, $"{baseName} ({i}){Blueprint.Extension}");

			TagIO.ToFile(bp.Save(), path, compress: true);
			return path;
		}

		public static Blueprint Load(string path)
		{
			TagCompound tag = TagIO.FromFile(path, compressed: true);
			var bp = Blueprint.Load(tag);
			if (string.IsNullOrWhiteSpace(bp.Name))
				bp.Name = Path.GetFileNameWithoutExtension(path);
			return bp;
		}

		public static List<string> ListFiles()
		{
			EnsureFolder();
			return Directory.GetFiles(Folder, "*" + Blueprint.Extension)
				.OrderByDescending(File.GetLastWriteTimeUtc)
				.ToList();
		}
	}
}
