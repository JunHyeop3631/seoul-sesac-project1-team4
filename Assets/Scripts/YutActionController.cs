using System.Collections.Generic;
using UnityEngine;

public sealed class YutActionController : MonoBehaviour
{
    private const int DeploymentCellId = 1;

    [SerializeField] private Ally1 ally1Prefab;
    [SerializeField] private Gae_Archer gaeArcherPrefab;
    [SerializeField] private Ally3 ally3Prefab;
    [SerializeField] private Ally4 ally4Prefab;
    [SerializeField] private Ally5 ally5Prefab;
    [SerializeField] private Transform boardRoot;
    [SerializeField] private Sprite destinationMarkerSprite;
    [SerializeField] private Wave targetingWave;

    private readonly Transform[] cells = new Transform[YutBoard.CellCount];
    private readonly YutBoardOccupancy occupancy = new YutBoardOccupancy();
    private readonly List<int> destinationCellIds = new List<int>();
    private readonly SpriteRenderer[] destinationMarkers = new SpriteRenderer[YutBoard.CellCount];
    private Ally selectedAlly;

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
            YutThrowResult.Do => ally1Prefab,
            YutThrowResult.Gae => gaeArcherPrefab,
            YutThrowResult.Geol => ally3Prefab,
            YutThrowResult.Yut => ally4Prefab,
            YutThrowResult.Mo => ally5Prefab,
            _ => null
        };

        Transform cell = GetCell(DeploymentCellId);
        if (prefab == null || cell == null)
        {
            Debug.LogWarning("Assign the matching ally prefab and Cell 01.", this);
            return false;
        }

        Ally ally = Instantiate(prefab, cell, false);
        ally.transform.localPosition = Vector3.zero;
        RegisterAlly(DeploymentCellId, ally);
        return true;
    }

    private void RegisterAlly(int cellId, Ally ally)
    {
        ally.SetTargetingWave(targetingWave);
        occupancy.AddAlly(cellId, ally);
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
        destinationCellIds.AddRange(YutBoard.GetDestinationCells(currentCellId, result));

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

        if (!occupancy.TryMoveAlly(selectedAlly, destinationCellId))
        {
            return false;
        }

        selectedAlly.transform.SetParent(cell, false);
        selectedAlly.transform.localPosition = Vector3.zero;
        ClearSelection();
        return true;
    }

    internal void ClearSelection()
    {
        selectedAlly = null;
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
            markerObject.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
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
