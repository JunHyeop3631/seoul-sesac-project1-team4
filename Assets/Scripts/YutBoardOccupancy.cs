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

    internal void AddAlly(int cellId, Ally ally)
    {
        alliesByCell[cellId].Add(ally);
    }

    internal IReadOnlyList<Ally> GetAllies(int cellId)
    {
        return alliesByCell[cellId];
    }

    internal bool TryGetCell(Ally ally, out int cellId)
    {
        for (cellId = 0; cellId < alliesByCell.Length; cellId++)
        {
            if (alliesByCell[cellId].Contains(ally))
            {
                return true;
            }
        }

        cellId = -1;
        return false;
    }

    internal bool TryMoveAlly(Ally ally, int destinationCellId)
    {
        if (!TryGetCell(ally, out int currentCellId))
        {
            return false;
        }

        alliesByCell[currentCellId].Remove(ally);
        AddAlly(destinationCellId, ally);
        return true;
    }
}
