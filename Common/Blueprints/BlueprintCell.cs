namespace Terraprint.Common.Blueprints
{
	/// <summary>
	/// Uma celula (um tile do mundo) dentro de um blueprint.
	/// TileType/WallType sao os IDs em tempo de execucao (resolvidos a partir dos nomes salvos no arquivo).
	/// </summary>
	public struct BlueprintCell
	{
		public bool HasTile;
		public ushort TileType;
		public short FrameX;
		public short FrameY;
		public byte Slope;      // SlopeType (0 = solido)
		public bool HalfBlock;
		public byte TilePaint;
		public bool HasActuator;
		/// <summary>Bloco desativado por atuador (o jogador passa por dentro).</summary>
		public bool Inactive;

		public ushort WallType; // 0 = sem parede
		public byte WallPaint;

		public byte LiquidAmount;
		public byte LiquidType;

		// So em memoria (nao vao para o arquivo): celulas ignoradas pelas listas de bloqueio das opcoes.
		// O fantasma, o progresso, os materiais e a construcao automatica nao exigem nada nelas.
		public bool SkipTile;
		public bool SkipWall;

		/// <summary>True quando a celula nao exige nada (sem bloco, parede ou liquido).</summary>
		public readonly bool IsEmpty => !HasTile && WallType == 0 && LiquidAmount == 0;
	}
}
