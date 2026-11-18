using System.Collections.Generic;

public sealed class YutBoardOccupancy
{
    internal const int MaxPopulationPerCell = 6;
    internal const int MaxAlliesPerStack = 3;

    private readonly List<Ally>[] alliesByCell = new List<Ally>[YutBoard.CellCount];

    public YutBoardOccupancy()
    {
        for (int cellId = 0; cellId < alliesByCell.Length; cellId++)
        {
            alliesByCell[cellId] = new List<Ally>();
        }
    }

    internal static int GetPopulationCost(IReadOnlyList<Ally> allies)
    {
        int population = 0;
        foreach (Ally ally in allies)
        {
            if (ally != null)
                population = (int)System.Math.Min((long)population + ally.PopulationCost, int.MaxValue);
        }
        return population;
    }

    internal int GetPopulation(int cellId) => GetPopulationCost(alliesByCell[cellId]);

    internal bool HasSpace(int cellId, int incomingPopulation)
    {
        return cellId >= 0 && cellId < alliesByCell.Length && incomingPopulation > 0 &&
            incomingPopulation <= MaxPopulationPerCell - GetPopulation(cellId);
    }

    internal bool TryAddAlly(int cellId, Ally ally)
    {
        if (ally == null || !HasSpace(cellId, ally.PopulationCost) || TryGetCell(ally, out _))
        {
            return false;
        }

        alliesByCell[cellId].RemoveAll(occupant => occupant == null);
        alliesByCell[cellId].Add(ally);
        return true;
    }

    internal IReadOnlyList<Ally> GetAllies(int cellId)
    {
        return alliesByCell[cellId];
    }

    // Cell registration order defines membership. Querying after any occupancy
    // change automatically repartitions the remaining/arriving allies.
    internal List<Ally> GetStack(Ally selected)
    {
        var matching = new List<Ally>();
        if (selected == null || !selected.isActiveAndEnabled || !TryGetCell(selected, out int cellId))
            return matching;

        foreach (Ally ally in alliesByCell[cellId])
        {
            if (ally != null && ally.isActiveAndEnabled && ally.GetType() == selected.GetType() &&
                ally.EvolutionStage == selected.EvolutionStage)
                matching.Add(ally);
        }

        int index = matching.IndexOf(selected);
        if (index < 0) return new List<Ally>();
        int first = index / MaxAlliesPerStack * MaxAlliesPerStack;
        return matching.GetRange(first, System.Math.Min(MaxAlliesPerStack, matching.Count - first));
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

    internal bool TryRemoveAllies(IReadOnlyList<Ally> allies)
    {
        if (allies.Count == 0 || allies[0] == null || !TryGetCell(allies[0], out int cellId))
            return false;
        var unique = new HashSet<Ally>();
        foreach (Ally ally in allies)
        {
            if (ally == null || !unique.Add(ally) || !alliesByCell[cellId].Contains(ally))
                return false;
        }
        foreach (Ally ally in allies) alliesByCell[cellId].Remove(ally);
        return true;
    }

    internal bool TryMoveAlly(Ally ally, int destinationCellId)
    {
        return TryMoveAllies(new[] { ally }, destinationCellId);
    }

    internal bool TryReplaceAllies(int cellId, IReadOnlyList<Ally> materials, Ally replacement)
    {
        if (cellId < 0 || cellId >= alliesByCell.Length) return false;
        var occupants = alliesByCell[cellId];
        var unique = new HashSet<Ally>(materials);
        if (replacement == null || materials.Count != 3 || unique.Count != 3 ||
            TryGetCell(replacement, out _))
            return false;
        foreach (Ally ally in materials)
        {
            if (ally == null || !occupants.Contains(ally))
                return false;
        }

        if ((long)GetPopulation(cellId) - GetPopulationCost(materials) + replacement.PopulationCost > MaxPopulationPerCell)
            return false;

        foreach (Ally ally in materials)
            occupants.Remove(ally);
        occupants.Add(replacement);
        return true;
    }

    internal bool TryMoveAllies(IReadOnlyList<Ally> allies, int destinationCellId)
    {
        if (allies.Count == 0 || allies[0] == null ||
            !TryGetCell(allies[0], out int currentCellId))
        {
            return false;
        }

        var uniqueAllies = new HashSet<Ally>();
        foreach (Ally ally in allies)
        {
            if (ally == null || !uniqueAllies.Add(ally) ||
                !alliesByCell[currentCellId].Contains(ally))
            {
                return false;
            }
        }

        if (currentCellId == destinationCellId)
        {
            return true;
        }

        if (!HasSpace(destinationCellId, GetPopulationCost(allies)))
        {
            return false;
        }

        foreach (Ally ally in allies)
        {
            alliesByCell[currentCellId].Remove(ally);
            alliesByCell[destinationCellId].Add(ally);
        }
        return true;
    }
}
