using UnityEngine;

[RequireComponent(typeof(ProjectileAttack))]
public class Do_Taoist : Ally
{
    public override int ExitReward => GetOrdinaryExitReward(3);
    // Animation Event on both Do attack clips.
    public void ReleaseMagic()
    {
        ReleaseProjectile();
    }
}
