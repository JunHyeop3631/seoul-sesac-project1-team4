using UnityEngine;

[RequireComponent(typeof(ProjectileAttack))]
public class Do_Taoist : Ally
{
    // Animation Event on both Do attack clips.
    public void ReleaseMagic()
    {
        ReleaseProjectile();
    }
}
