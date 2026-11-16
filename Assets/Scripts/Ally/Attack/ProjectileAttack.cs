using UnityEngine;

[DisallowMultipleComponent]
public class ProjectileAttack : MonoBehaviour
{
    [SerializeField] private HomingProjectile projectilePrefab;
    [SerializeField, Min(0.01f)] private float flightDuration = 0.3f;

    public bool CanFire => isActiveAndEnabled && projectilePrefab != null;

    // The owning Ally selects the target and manages animation/cooldown.
    public void Fire(Enemy target, int damage)
    {
        if (!CanFire || target == null || !target.isActiveAndEnabled || target.CurrentHealth <= 0)
            return;

        HomingProjectile projectile = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        projectile.Launch(target, damage, flightDuration);
    }
}
