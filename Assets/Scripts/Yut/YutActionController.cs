using System.Collections.Generic;
using UnityEngine;

public sealed class YutActionController : MonoBehaviour
{
    private const int DeploymentCellId = YutBoard.DeploymentCellId;
    [SerializeField] private TurnController turnController;

    [SerializeField] private Do_Taoist doTaoistPrefab;
    [SerializeField] private Gae_Warrior gaeWarriorPrefab;
    [SerializeField] private Geol_Spearman geolSpearmanPrefab;
    [SerializeField] private Yut_Maceman yutMacemanPrefab;
    [SerializeField] private Mo_Archer moArcherPrefab;
    [SerializeField] private Transform boardRoot;
    [SerializeField] private Sprite destinationMarkerSprite;
    [SerializeField] private Font exitLabelFont;
    [SerializeField] private Vector2 exitMarkerOffset = new Vector2(0f, -0.95f);
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
    private int selectedSourceCellId = -1;
    private ExitDestinationView exitDestinationView;
    private MovePreviewLabel sourcePreview;
    private readonly MovePreviewLabel[] destinationPreviews = new MovePreviewLabel[YutBoard.CellCount];
    private YutThrowResult selectedResult;

    public event System.Action<Ally> EvolutionRequested;
    public bool IsResolvingMove { get; private set; }
    internal Font PreparationFont => exitLabelFont;
    public bool CanUsePreparationInput => isActiveAndEnabled && turnController != null &&
        turnController.CanUsePreparationInput;

    public bool CanMove(YutThrowResult result)
    {
        if (!CanUsePreparationInput || turnController.IsThrowing)
            return false;

        for (int cellId = 0; cellId < cells.Length; cellId++)
        {
            foreach (Ally ally in occupancy.GetAllies(cellId))
            {
                if (ally == null || !ally.isActiveAndEnabled)
                    continue;

                List<Ally> group = occupancy.GetStack(ally);
                foreach (int destination in YutBoard.GetDestinationCells(cellId, result))
                {
                    if (CanUseDestination(cellId, destination, group))
                        return true;
                }
            }
        }
        return false;
    }

    private bool CanUseDestination(int sourceCellId, int destinationCellId, IReadOnlyList<Ally> group)
    {
        if (destinationCellId == YutBoard.ExitCellId)
        {
            if (group.Count == 0 || allyPool == null || turnController.Currency == null ||
                turnController.PreparationCanvas == null || exitLabelFont == null || GetCell(YutBoard.LastCellId) == null)
                return false;
            foreach (Ally ally in group)
                if (!allyPool.CanReturn(ally)) return false;
            return true;
        }
        return GetCell(destinationCellId) != null &&
            (destinationCellId == sourceCellId || occupancy.HasSpace(destinationCellId, YutBoardOccupancy.GetPopulationCost(group)));
    }

    public bool CanEvolve(Ally ally)
    {
        if (!CanUsePreparationInput || !turnController.CanEvolve ||
            allyPool == null || ally == null || !ally.isActiveAndEnabled ||
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
        ConnectAlly(evolved);
        foreach (Ally material in materials)
        {
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

    private void OnDestroy()
    {
        if (exitDestinationView != null) Destroy(exitDestinationView.gameObject);
        if (sourcePreview != null) Destroy(sourcePreview.gameObject);
        foreach (var preview in destinationPreviews)
            if (preview != null) Destroy(preview.gameObject);
    }

    private Ally GetDeploymentPrefab(YutThrowResult result) => result switch
    {
        YutThrowResult.Do => doTaoistPrefab,
        YutThrowResult.Gae => gaeWarriorPrefab,
        YutThrowResult.Geol => geolSpearmanPrefab,
        YutThrowResult.Yut => yutMacemanPrefab,
        YutThrowResult.Mo => moArcherPrefab,
        _ => null
    };

    public bool CanDeploy(YutThrowResult result)
    {
        Ally prefab = GetDeploymentPrefab(result);
        return CanUsePreparationInput && !turnController.IsThrowing && prefab != null &&
            GetCell(DeploymentCellId) != null && allyPool != null &&
            occupancy.HasSpace(DeploymentCellId, prefab.PopulationCost);
    }

    public bool DeployAlly(YutThrowResult result)
    {
        if (!CanDeploy(result))
        {
            return false;
        }

        Ally prefab = GetDeploymentPrefab(result);

        Transform cell = GetCell(DeploymentCellId);
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
            Debug.LogWarning($"Cell {cellId:00} cannot exceed population {YutBoardOccupancy.MaxPopulationPerCell} or register duplicate allies.", ally);
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
        if (!CanUsePreparationInput || turnController.IsThrowing || ally == null ||
            !occupancy.TryGetCell(ally, out int currentCellId))
        {
            return;
        }

        ClearSelection();
        selectedAlly = ally;
        selectedSourceCellId = currentCellId;
        selectedResult = result;
        selectedAllies.AddRange(occupancy.GetStack(ally));
        RefreshMovePreview();
    }

    private void LateUpdate()
    {
        if (selectedAlly != null) RefreshMovePreview();
        else if (selectedAllies.Count > 0) ClearSelection();
    }

    private void RefreshMovePreview()
    {
        var stack = occupancy.GetStack(selectedAlly);
        if (!CanUsePreparationInput || stack.Count != selectedAllies.Count ||
            !stack.TrueForAll(ally => selectedAllies.Contains(ally)) ||
            !occupancy.TryGetCell(selectedAlly, out int currentCellId) || currentCellId != selectedSourceCellId)
        {
            ClearSelection(); return;
        }
        if (turnController.PreparationCanvas != null && exitLabelFont != null)
        {
            if (sourcePreview == null) sourcePreview = MovePreviewLabel.Create(turnController.PreparationCanvas, exitLabelFont);
            sourcePreview.Show(GetCell(currentCellId).position + Vector3.up * .85f,
                $"선택 {stack.Count}기 · 인구 {YutBoardOccupancy.GetPopulationCost(stack)}");
        }
        var nextDestinations = new List<int>();
        foreach (int cellId in YutBoard.GetDestinationCells(currentCellId, selectedResult))
        {
            if (CanUseDestination(currentCellId, cellId, selectedAllies))
            {
                nextDestinations.Add(cellId);
            }
        }
        // Keep existing buttons active while refreshing so pointer-down state survives a frame.
        foreach (int previous in destinationCellIds)
            if (!nextDestinations.Contains(previous)) SetMarkerActive(previous, false);
        destinationCellIds.Clear();
        destinationCellIds.AddRange(nextDestinations);

        foreach (int cellId in destinationCellIds)
        {
            SetMarkerActive(cellId, true);
        }
    }

    public bool MoveSelectedAlly(int destinationCellId)
    {
        if (!CanUsePreparationInput || turnController.IsThrowing || selectedAlly == null ||
            !destinationCellIds.Contains(destinationCellId))
        {
            return false;
        }

        List<Ally> currentStack = occupancy.GetStack(selectedAlly);
        if (currentStack.Count != selectedAllies.Count ||
            !currentStack.TrueForAll(ally => selectedAllies.Contains(ally)) ||
            !occupancy.TryGetCell(selectedAlly, out int sourceCellId) || sourceCellId != selectedSourceCellId)
        {
            ClearSelection();
            return false;
        }

        if (!CanUseDestination(sourceCellId, destinationCellId, selectedAllies))
            return false;

        IsResolvingMove = true;
        try
        {
            if (destinationCellId == YutBoard.ExitCellId)
            {
                var exiting = new List<Ally>(selectedAllies);
                int reward = GetExitReward(exiting);
                if (!occupancy.TryRemoveAllies(exiting)) return false;
                // Clear before pool/wallet callbacks: a repeated destination cannot pay twice.
                ClearSelection();
                foreach (Ally ally in exiting) allyPool.Return(ally);
                ArrangeAllies(sourceCellId);
                turnController.Currency.GrantSpecial(reward);
                return true;
            }

            if (!occupancy.TryMoveAllies(selectedAllies, destinationCellId)) return false;
            Transform cell = GetCell(destinationCellId);
            foreach (Ally ally in selectedAllies) ally.transform.SetParent(cell, false);
            ArrangeAllies(sourceCellId);
            ArrangeAllies(destinationCellId);
            ClearSelection();
            return true;
        }
        finally
        {
            IsResolvingMove = false;
        }
    }

    internal void ClearSelection()
    {
        selectedAlly = null;
        selectedSourceCellId = -1;
        selectedAllies.Clear();
        if (sourcePreview != null) sourcePreview.Hide();
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
            if (destination == YutBoard.ExitCellId) continue; // UI Button owns off-board input.
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
        return cellId >= 0 && cellId < cells.Length ? cells[cellId] : null;
    }

    private void SetMarkerActive(int cellId, bool active)
    {
        if (cellId == YutBoard.ExitCellId)
        {
            if (active)
            {
                if (exitDestinationView == null)
                    exitDestinationView = ExitDestinationView.Create(turnController.PreparationCanvas, exitLabelFont,
                        () => turnController.SelectDestination(YutBoard.ExitCellId));
                exitDestinationView.Show(GetCell(YutBoard.LastCellId).position + (Vector3)exitMarkerOffset,
                    GetExitReward(selectedAllies));
            }
            else if (exitDestinationView != null) exitDestinationView.Hide();
            return;
        }
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
        if (active && cell != null && turnController.PreparationCanvas != null && exitLabelFont != null)
        {
            if (destinationPreviews[cellId] == null)
                destinationPreviews[cellId] = MovePreviewLabel.Create(turnController.PreparationCanvas, exitLabelFont);
            int expected = occupancy.GetPopulation(cellId) +
                (cellId == selectedSourceCellId ? 0 : YutBoardOccupancy.GetPopulationCost(selectedAllies));
            destinationPreviews[cellId].Show(cell.position + Vector3.down * .8f,
                $"예상 인구 {expected}/{YutBoardOccupancy.MaxPopulationPerCell}");
        }
        else if (destinationPreviews[cellId] != null) destinationPreviews[cellId].Hide();
    }

    private static int GetExitReward(IReadOnlyList<Ally> group)
    {
        int reward = 0;
        foreach (Ally ally in group)
            if (ally != null)
                reward = (int)System.Math.Min((long)reward + System.Math.Max(0, ally.ExitReward), int.MaxValue);
        return reward;
    }
}
