using System;
using UnityEngine;

public sealed class SpecialSummonController : MonoBehaviour
{
    public const int Cost = 30;
    public const int RequiredPopulation = 3;
    [SerializeField] private TurnController turnController;
    [SerializeField] private YutActionController actions;
    [SerializeField] private StageCurrency currency;
    [SerializeField] private Ally[] candidates = Array.Empty<Ally>();

    public event Action<Ally> Summoned;
    public bool IsSummoning => actions != null && actions.IsResolvingSummon;
    public bool HasSpace => actions != null && actions.HasDeploymentSpace(RequiredPopulation);
    public bool CanSummon
    {
        get
        {
            if (!isActiveAndEnabled || turnController == null || !turnController.CanOpenPreparationPanel ||
                actions == null || currency == null || !currency.isActiveAndEnabled || currency.SpecialBalance < Cost ||
                candidates == null || candidates.Length == 0) return false;
            foreach (Ally candidate in candidates)
                if (candidate == null || candidate.PopulationCost != RequiredPopulation ||
                    !candidate.enabled || !candidate.gameObject.activeSelf || !actions.CanDeploy(candidate)) return false;
            return true;
        }
    }

    public bool TrySummon()
    {
        if (!CanSummon) return false;
        Ally candidate = candidates[UnityEngine.Random.Range(0, candidates.Length)];
        return actions.TryDeployPaidAlly(candidate, currency, Cost, ally => Summoned?.Invoke(ally));
    }
}
