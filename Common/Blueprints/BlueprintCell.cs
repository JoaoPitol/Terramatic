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

		public ushort WallType; // 0 = sem parede
		public byte WallPaint;

		public byte LiquidAmount;
		public byte LiquidType;

		/// <summary>True quando a celula nao exige nada (sem bloco, parede ou liquido).</summary>
		public readonly bool IsEmpty => !HasTile && WallType == 0 && LiquidAmount == 0;
	}
}
