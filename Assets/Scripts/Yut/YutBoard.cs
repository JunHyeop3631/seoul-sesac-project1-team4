using System;
using System.Collections.Generic;

public static class YutBoard
{
    public const int StartCellId = 0;

    private const int TopRightCornerCellId = 5;
    private const int TopLeftCornerCellId = 10;
    private const int CenterCellId = 22;
    private const int TopRightDiagonalBeforeCenterCellId = 21;
    private const int TopLeftDiagonalBeforeCenterCellId = 26;

    // Outer cells 0–19 run counterclockwise. Start at 00 and move up to 01.
    //
    // 10----09----08----07----06----05
    // |  \                       /  |
    // 11   25                  20   04
    // |      \               /      |
    // 12       26          21       03
    // |          \       /          |
    // |             22              |
    // |          /       \          |
    // 13       23          27       02
    // |      /               \      |
    // 14   24                  28   01
    // |  /                       \  ↑
    // 15----16----17----18----19----00 (start)
    private static readonly int[][] NextCells =
    {
        new[] { 1 }, // 0
        new[] { 2 }, // 1
        new[] { 3 }, // 2
        new[] { 4 }, // 3
        new[] { 5 }, // 4
        new[] { 6, 20 }, // 5
        new[] { 7 }, // 6
        new[] { 8 }, // 7
        new[] { 9 }, // 8
        new[] { 10 }, // 9
        new[] { 11, 25 }, // 10
        new[] { 12 }, // 11
        new[] { 13 }, // 12
        new[] { 14 }, // 13
        new[] { 15 }, // 14
        new[] { 16 }, // 15
        new[] { 17 }, // 16
        new[] { 18 }, // 17
        new[] { 19 }, // 18
        new[] { 0 }, // 19
        new[] { 21 }, // 20
        new[] { 22 }, // 21
        new[] { 23, 27 }, // 22
        new[] { 24 }, // 23
        new[] { 15 }, // 24
        new[] { 26 }, // 25
        new[] { 22 }, // 26
        new[] { 28 }, // 27
        new[] { 0 }, // 28
    };

    public static int CellCount => NextCells.Length;

    public static IReadOnlyList<int> GetDestinationCells(int startCellId, YutThrowResult result)
    {
        var destinations = new List<int>();
        foreach (int[] path in GetForwardPaths(startCellId, result))
        {
            int destination = path[path.Length - 1];
            if (!destinations.Contains(destination))
                destinations.Add(destination);
        }
        return destinations;
    }

    internal static IReadOnlyList<int[]> GetForwardPaths(int startCellId, YutThrowResult result)
    {
        int stepCount = result switch
        {
            YutThrowResult.Do => 1,
            YutThrowResult.Gae => 2,
            YutThrowResult.Geol => 3,
            YutThrowResult.Yut => 4,
            YutThrowResult.Mo => 5,
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };

        ReadOnlySpan<int> firstSteps = GetNextCells(startCellId);
        var paths = new List<int[]>(firstSteps.Length);
        foreach (int firstStep in firstSteps)
        {
            int previousCellId = startCellId;
            int currentCellId = firstStep;
            var path = new int[stepCount];
            path[0] = firstStep;

            // A route can branch on its first step. Later steps follow its selected path.
            for (int stepsTaken = 1; stepsTaken < stepCount; stepsTaken++)
            {
                int nextCellId = GetNextCells(currentCellId, previousCellId)[0];
                previousCellId = currentCellId;
                currentCellId = nextCellId;
                path[stepsTaken] = currentCellId;
            }

            paths.Add(path);
        }

        return paths;
    }

    // Omit previousCellId on the first step. Pass the immediately preceding cell on later steps.
    public static ReadOnlySpan<int> GetNextCells(int cellId, int? previousCellId = null)
    {
        ReadOnlySpan<int> nextCells = NextCells[cellId];
        if (!previousCellId.HasValue)
        {
            return nextCells;
        }

        if (cellId == TopRightCornerCellId || cellId == TopLeftCornerCellId)
        {
            return nextCells.Slice(0, 1);
        }

        if (cellId == CenterCellId)
        {
            return previousCellId.Value switch
            {
                TopRightDiagonalBeforeCenterCellId => nextCells.Slice(0, 1),
                TopLeftDiagonalBeforeCenterCellId => nextCells.Slice(1, 1),
                _ => throw new ArgumentException("The previous cell must lead into the center.", nameof(previousCellId)),
            };
        }

        return nextCells;
    }
}
