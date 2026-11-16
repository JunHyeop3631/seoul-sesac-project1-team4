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
    [SerializeField] private Vector2 allySlotSpacing = new Vector2(0.3f, 0.5f);

    private readonly Transform[] cells = new Transform[YutBoard.CellCount];
    private readonly YutBoardOccupancy occupancy = new YutBoardOccupancy();
    private readonly List<int> destinationCellIds = new List<int>();
    private readonly SpriteRenderer[] destinationMarkers = new SpriteRenderer[YutBoard.CellCount];
    private Ally selectedAlly;
    private readonly List<Ally> selectedAllies = new List<Ally>();

    public event System.Action<Ally> EvolutionRequested;

    public bool CanEvolve(Ally ally)
    {
        if (!isActiveAndEnabled || ally == null || !ally.isActiveAndEnabled ||
            !occupancy.TryGetCell(ally, out int cellId))
        {
            return false;
        }

        int count = 0;
        foreach (Ally occupant in occupancy.GetAllies(cellId))
        {
            if (occupant != null && occupant.isActiveAndEnabled && occupant.GetType() == ally.GetType())
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

        Debug.Log($"Evolution requested for {ally.GetType().Name}.", ally);
        EvolutionRequested?.Invoke(ally);
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
        if (prefab == null || cell == null)
        {
            Debug.LogWarning("Assign the matching ally prefab and Cell 01.", this);
            return false;
        }

        if (!occupancy.HasSpace(DeploymentCellId))
        {
            return false;
        }

        Ally ally = Instantiate(prefab, cell, false);
        ally.transform.localPosition = Vector3.zero;
        if (!RegisterAlly(DeploymentCellId, ally))
        {
            Destroy(ally.gameObject);
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

        ally.SetTargetingWave(targetingWave);
        ArrangeAllies(cellId);
        return true;
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
        foreach (Ally occupant in occupancy.GetAllies(currentCellId))
        {
            if (occupant != null && occupant.GetType() == ally.GetType())
            {
                selectedAllies.Add(occupant);
            }
        }

        foreach (int cellId in YutBoard.GetDestinationCells(currentCellId, result))
        {
            if (cellId == currentCellId || occupancy.HasSpace(cellId, selectedAllies.Count))
            {
                destinationCellIds.Add(cellId);
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
                if (ally != null && ContainsPoint(ally.GetComponent<SpriteRenderer>(), worldPoint))
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
