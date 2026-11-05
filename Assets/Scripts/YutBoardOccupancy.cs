using System.Collections.Generic;

public sealed class YutBoardOccupancy
{
    private readonly List<Ally>[] alliesByCell = new List<Ally>[YutBoard.CellCount];

    public YutBoardOccupancy()
    {
        for (int cellId = 0; cellId < alliesByCell.Length; cellId++)
        {
            alliesByCell[cellId] = new List<Ally>();
        }
    }
}
