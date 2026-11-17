using System.Collections.Generic;
using UnityEngine;

public sealed class YutActionController : MonoBehaviour
{
    private const int DeploymentCellId = 1;

    [SerializeField] private Do_Taoist doTaoistPrefab;
    [SerializeField] private Gae_Warrior gaeWarriorPrefab;
    [SerializeField] private Geol_Spearman geolSpearmanPrefab;
    [SerializeField] private Yut_Maceman yutMacemanPrefab;
    [SerializeField] private Mo_Archer moArcherPrefab;
    [SerializeField] private Transform boardRoot;
    [SerializeField] private Sprite destinationMarkerSprite;
    [SerializeField] private Wave targetingWave;
    [SerializeField] private ProjectilePool projectilePool;
    [SerializeField] private AllyPool allyPool;
    [System.Serializable]
    private class EvolutionEntry
    {
        public Ally firstStage = null;
        public Ally secondStage = null;
    }
    [SerializeField] private EvolutionEntry[] evolutions = System.Array.Empty<EvolutionEntry>();
    [SerializeField] private Vector2 allySlotSpacing = new Vector2(0.3f, 0.5f);

    private readonly Transform[] cells = new Transform[YutBoard.CellCount];
    private readonly YutBoardOccupancy occupancy = new YutBoardOccupancy();
    private readonly List<int> destinationCellIds = new List<int>();
    private readonly SpriteRenderer[] destinationMarkers = new SpriteRenderer[YutBoard.CellCount];
    private Ally selectedAlly;
    private readonly List<Ally> selectedAllies = new List<Ally>();
    private readonly Dictionary<Ally, List<int>> travelHistory = new();
    private readonly Dictionary<int, int[]> destinationPaths = new();
    private bool selectedMoveIsBackward;

    public event System.Action<Ally> EvolutionRequested;

    public bool CanMove(YutThrowResult result)
    {
        if (!isActiveAndEnabled)
            return false;

        for (int cellId = 0; cellId < cells.Length; cellId++)
        {
            foreach (Ally ally in occupancy.GetAllies(cellId))
            {
                if (ally == null || !ally.isActiveAndEnabled)
                    continue;

                List<Ally> group = GetMovingAllies(ally, cellId, result);
                if (result == YutThrowResult.Backdo)
                {
                    int previousCell = GetPreviousCell(ally);
                    if (previousCell >= 0 && GetCell(previousCell) != null &&
                        occupancy.HasSpace(previousCell, group.Count))
                        return true;
                }
                else
                {
                    foreach (int destination in YutBoard.GetDestinationCells(cellId, result))
                    {
                        if (GetCell(destination) != null &&
                            (destination == cellId || occupancy.HasSpace(destination, group.Count)))
                            return true;
                    }
                }
            }
        }
        return false;
    }

    private int GetPreviousCell(Ally ally)
    {
        return travelHistory.TryGetValue(ally, out List<int> history) && history.Count >= 2
            ? history[history.Count - 2] : -1;
    }

    private List<Ally> GetMovingAllies(Ally ally, int cellId, YutThrowResult result)
    {
        var group = new List<Ally>();
        foreach (Ally occupant in occupancy.GetAllies(cellId))
        {
            if (occupant != null && occupant.isActiveAndEnabled &&
                occupant.GetType() == ally.GetType() && occupant.EvolutionStage == ally.EvolutionStage &&
                (result != YutThrowResult.Backdo || GetPreviousCell(occupant) == GetPreviousCell(ally)))
                group.Add(occupant);
        }
        return group;
    }

    public bool CanEvolve(Ally ally)
    {
        if (!isActiveAndEnabled || allyPool == null || ally == null || !ally.isActiveAndEnabled ||
            ally.EvolutionStage != 1 || GetEvolutionPrefab(ally) == null ||
            !occupancy.TryGetCell(ally, out int cellId))
        {
            return false;
        }

        int count = 0;
        foreach (Ally occupant in occupancy.GetAllies(cellId))
        {
            if (occupant != null && occupant.isActiveAndEnabled && occupant.EvolutionStage == 1 &&
                occupant.GetType() == ally.GetType())
            {
                count++;
            }
        }

        return count >= 3;
    }

    public void RequestEvolution(Ally ally)
    {
        if (!CanEvolve(ally))
        {
            return;
        }

        occupancy.TryGetCell(ally, out int cellId);
        var materials = new List<Ally> { ally };
        foreach (Ally occupant in occupancy.GetAllies(cellId))
        {
            if (materials.Count == 3)
                break;
            if (occupant != null && occupant != ally && occupant.isActiveAndEnabled &&
                occupant.EvolutionStage == 1 && occupant.GetType() == ally.GetType())
                materials.Add(occupant);
        }

        Ally evolved = allyPool.Request(GetEvolutionPrefab(ally), GetCell(cellId));
        if (!occupancy.TryReplaceAllies(cellId, materials, evolved))
        {
            allyPool.Return(evolved);
            return;
        }

        ClearSelection();
        travelHistory[evolved] = new List<int>(travelHistory[ally]);
        ConnectAlly(evolved);
        foreach (Ally material in materials)
        {
            travelHistory.Remove(material);
            allyPool.Return(material);
        }
        ArrangeAllies(cellId);
        EvolutionRequested?.Invoke(ally);
    }

    private Ally GetEvolutionPrefab(Ally ally)
    {
        foreach (var entry in evolutions)
        {
            if (entry.firstStage != null && entry.secondStage != null &&
                entry.firstStage.GetType() == ally.GetType())
                return entry.secondStage;
        }
        return null;
    }

    private void Awake()
    {
        if (boardRoot == null)
        {
            Debug.LogWarning("Assign the board root containing the YutCell objects.", this);
            return;
        }

        foreach (Transform child in boardRoot)
        {
            if (!child.TryGetComponent(out YutCell cell))
            {
                continue;
            }

            int cellId = cell.CellId;
            if (cellId < 0 || cellId >= cells.Length || cells[cellId] != null)
            {
                Debug.LogError($"Invalid or duplicate YutCell ID: {cellId}.", cell);
                continue;
            }

            cells[cellId] = child;
            foreach (Transform occupant in child)
            {
                if (occupant.TryGetComponent(out Ally ally))
                {
                    RegisterAlly(cellId, ally);
                }
            }
        }
    }

    private void OnDisable()
    {
        ClearSelection();
    }

    public bool DeployAlly(YutThrowResult result)
    {
        if (!isActiveAndEnabled)
        {
            return false;
        }

        Ally prefab = result switch
        {
            YutThrowResult.Do => doTaoistPrefab,
            YutThrowResult.Gae => gaeWarriorPrefab,
            YutThrowResult.Geol => geolSpearmanPrefab,
            YutThrowResult.Yut => yutMacemanPrefab,
            YutThrowResult.Mo => moArcherPrefab,
            _ => null
        };

        Transform cell = GetCell(DeploymentCellId);
        if (prefab == null || cell == null || allyPool == null)
        {
            Debug.LogWarning("Assign the matching ally prefab and Cell 01.", this);
            return false;
        }

        if (!occupancy.HasSpace(DeploymentCellId))
        {
            return false;
        }

        Ally ally = allyPool.Request(prefab, cell);
        ally.transform.localPosition = Vector3.zero;
        if (!RegisterAlly(DeploymentCellId, ally))
        {
            allyPool.Return(ally);
            return false;
        }

        return true;
    }

    private bool RegisterAlly(int cellId, Ally ally)
    {
        if (!occupancy.TryAddAlly(cellId, ally))
        {
            Debug.LogWarning($"Cell {cellId:00} cannot register more than {YutBoardOccupancy.MaxAlliesPerCell} allies or duplicate allies.", ally);
            return false;
        }

        if (allyPool != null)
        {
            foreach (var entry in evolutions)
            {
                Ally prefab = ally.EvolutionStage == 1 ? entry.firstStage : entry.secondStage;
                if (prefab != null && prefab.GetType() == ally.GetType())
                {
                    allyPool.Register(ally, prefab);
                    break;
                }
            }
        }
        ConnectAlly(ally);
        travelHistory[ally] = cellId == DeploymentCellId
            ? new List<int> { YutBoard.StartCellId, cellId }
            : new List<int> { cellId };
        ArrangeAllies(cellId);
        return true;
    }

    private void ConnectAlly(Ally ally)
    {
        ally.SetTargetingWave(targetingWave);
        if (ally.TryGetComponent(out ProjectileAttack projectileAttack))
        {
            projectileAttack.SetPool(projectilePool);
        }
    }

    private void ArrangeAllies(int cellId)
    {
        const int columns = 3;
        IReadOnlyList<Ally> allies = occupancy.GetAllies(cellId);
        int slotIndex = 0;
        foreach (Ally ally in allies)
        {
            if (ally == null)
            {
                continue;
            }

            // Fill the top row left to right, then the bottom row.
            int column = slotIndex % columns;
            int row = slotIndex / columns;
            ally.transform.localPosition = new Vector3(
                (column - 1) * allySlotSpacing.x,
                (0.5f - row) * allySlotSpacing.y,
                0f);
            slotIndex++;
        }
    }

    public void SelectAlly(Ally ally, YutThrowResult result)
    {
        if (!isActiveAndEnabled || ally == null ||
            !occupancy.TryGetCell(ally, out int currentCellId))
        {
            return;
        }

        ClearSelection();
        selectedAlly = ally;
        selectedMoveIsBackward = result == YutThrowResult.Backdo;
        selectedAllies.AddRange(GetMovingAllies(ally, currentCellId, result));
        if (selectedMoveIsBackward)
        {
            int previousCell = GetPreviousCell(ally);
            if (previousCell >= 0 && GetCell(previousCell) != null &&
                occupancy.HasSpace(previousCell, selectedAllies.Count))
            {
                destinationCellIds.Add(previousCell);
            }
        }
        else
        {
            foreach (int[] path in YutBoard.GetForwardPaths(currentCellId, result))
            {
                int cellId = path[path.Length - 1];
                if (GetCell(cellId) != null &&
                    (cellId == currentCellId || occupancy.HasSpace(cellId, selectedAllies.Count)) &&
                    !destinationPaths.ContainsKey(cellId))
                {
                    destinationCellIds.Add(cellId);
                    destinationPaths.Add(cellId, path);
                }
            }
        }

        foreach (int cellId in destinationCellIds)
        {
            SetMarkerActive(cellId, true);
        }
    }

    public bool MoveSelectedAlly(int destinationCellId)
    {
        if (!isActiveAndEnabled || selectedAlly == null ||
            !destinationCellIds.Contains(destinationCellId))
        {
            return false;
        }

        Transform cell = GetCell(destinationCellId);
        if (cell == null)
        {
            Debug.LogWarning($"Assign Cell {destinationCellId:00}.", this);
            return false;
        }

        if (!occupancy.TryGetCell(selectedAlly, out int sourceCellId) ||
            !occupancy.TryMoveAllies(selectedAllies, destinationCellId))
        {
            return false;
        }

        foreach (Ally ally in selectedAllies)
        {
            List<int> history = travelHistory[ally];
            if (selectedMoveIsBackward)
                history.RemoveAt(history.Count - 1);
            else
                history.AddRange(destinationPaths[destinationCellId]);
            ally.transform.SetParent(cell, false);
        }
        ArrangeAllies(sourceCellId);
        ArrangeAllies(destinationCellId);
        ClearSelection();
        return true;
    }

    internal void ClearSelection()
    {
        selectedAlly = null;
        selectedAllies.Clear();
        foreach (int cellId in destinationCellIds)
        {
            SetMarkerActive(cellId, false);
        }

        destinationCellIds.Clear();
        destinationPaths.Clear();
    }

    internal bool TryGetDestinationAt(Vector2 worldPoint, out int cellId)
    {
        foreach (int destination in destinationCellIds)
        {
            if (ContainsPoint(destinationMarkers[destination], worldPoint))
            {
                cellId = destination;
                return true;
            }
        }

        cellId = -1;
        return false;
    }

    internal Ally GetAllyAt(Vector2 worldPoint)
    {
        for (int cellId = 0; cellId < YutBoard.CellCount; cellId++)
        {
            IReadOnlyList<Ally> allies = occupancy.GetAllies(cellId);
            for (int index = allies.Count - 1; index >= 0; index--)
            {
                Ally ally = allies[index];
                if (ally != null && ContainsPoint(ally.VisualRenderer, worldPoint))
                {
                    return ally;
                }
            }
        }

        return null;
    }

    private static bool ContainsPoint(SpriteRenderer renderer, Vector2 point)
    {
        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
        {
            return false;
        }

        Bounds bounds = renderer.bounds;
        return bounds.Contains(new Vector3(point.x, point.y, bounds.center.z));
    }

    private Transform GetCell(int cellId)
    {
        return cellId < cells.Length ? cells[cellId] : null;
    }

    private void SetMarkerActive(int cellId, bool active)
    {
        SpriteRenderer marker = destinationMarkers[cellId];
        Transform cell = GetCell(cellId);
        if (marker == null && active && cell != null && destinationMarkerSprite != null)
        {
            GameObject markerObject = new GameObject("Destination Marker");
            markerObject.transform.SetParent(cell, false);
            //markerObject.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            marker = markerObject.AddComponent<SpriteRenderer>();
            marker.sprite = destinationMarkerSprite;
            marker.sortingOrder = 2;
            destinationMarkers[cellId] = marker;
        }

        if (marker != null)
        {
            marker.gameObject.SetActive(active);
        }
    }
}
