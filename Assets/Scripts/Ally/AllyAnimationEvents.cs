using UnityEngine;

public sealed class AllyAnimationEvents : MonoBehaviour
{
    private Ally ally;

    private void Awake()
    {
        ally = GetComponentInParent<Ally>();
    }

    // Animation Events are received on the Visual object that owns the Animator.
    public void ApplyAttack()
    {
        if (ally != null)
            ally.ApplyAttack();
    }

    public void ReleaseMagic()
    {
        if (ally is Do_Taoist taoist)
            taoist.ReleaseMagic();
    }

    public void ReleaseArrow()
    {
        if (ally is Mo_Archer archer)
            archer.ReleaseArrow();
    }
}
