using System.Collections.Generic;
using UnityEngine;

public sealed class WhiteTiger : Ally
{
    [SerializeField] private WhiteTigerSlash slash;
    private readonly List<Enemy> targets = new();
    private readonly List<int> versions = new();
    public override int PopulationCost => 3;
    public override bool CanStack => false;
    public override bool CanEvolve => false;
    public override int ExitReward => 15;

    public override void ApplyAttack()
    {
        if (!TryBeginAttackEvent()) return;
        Wave wave = TargetingWave;
        Vector2 origin = transform.position;
        Vector2 direction = (Vector2)CurrentTarget.transform.position - origin;
        if (direction.sqrMagnitude == 0f) direction = Vector2.right;
        wave.CollectSectorTargets(origin, direction, AttackRange, targets);
        versions.Clear();
        foreach (Enemy enemy in targets) versions.Add(enemy.ActivationVersion);
        slash?.Play(origin, direction, AttackRange);
        for (int index = 0; index < targets.Count; index++)
        {
            if (!wave.IsRunning || !isActiveAndEnabled) break;
            Enemy enemy = targets[index];
            if (enemy != null && enemy.isActiveAndEnabled && enemy.CurrentHealth > 0f &&
                enemy.ActivationVersion == versions[index])
                enemy.TakeDamage(AttackDamage);
        }
    }

    protected override void OnTargetUnavailable()
    {
        base.OnTargetUnavailable();
        if (TargetingWave == null || !TargetingWave.IsRunning)
        {
            nextAttackTime = 0;
            slash?.Stop();
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        slash?.Stop();
    }
}
