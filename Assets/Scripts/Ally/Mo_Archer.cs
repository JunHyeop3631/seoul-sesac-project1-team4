using UnityEngine;

[RequireComponent(typeof(ProjectileAttack))]
public class Mo_Archer : Ally
{
    // Animation Event on both Mo attack clips.
    public void ReleaseArrow()
    {
        ReleaseProjectile();
    }
}
