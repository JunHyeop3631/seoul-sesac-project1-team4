using UnityEngine;

[RequireComponent(typeof(ProjectileAttack))]
public class Mo_Archer : Ally
{
    public override int ExitReward => GetOrdinaryExitReward(5);
    // Animation Event on both Mo attack clips.
    public void ReleaseArrow()
    {
        ReleaseProjectile();
    }
}
