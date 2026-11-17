using UnityEngine;

[DisallowMultipleComponent]
public class ProjectileAttack : MonoBehaviour
{
    [SerializeField] private HomingProjectile projectilePrefab;
    [SerializeField, Min(0.01f)] private float flightDuration = 0.3f;
    [SerializeField] private ProjectilePool projectilePool;
    [SerializeField] private SpriteRenderer launchCenterRenderer;

    public bool CanFire => isActiveAndEnabled && projectilePrefab != null && projectilePool != null;

    internal void SetPool(ProjectilePool pool)
    {
        projectilePool = pool;
    }

    // The owning Ally selects the target and manages animation/cooldown.
    public void Fire(Enemy target, float damage, HitEffect hitEffect = null)
    {
        if (!CanFire || target == null || !target.isActiveAndEnabled || target.CurrentHealth <= 0)
            return;

        Vector3 position = launchCenterRenderer != null
            ? launchCenterRenderer.bounds.center
            : transform.position;
        HomingProjectile projectile = projectilePool.Request(projectilePrefab, position);
        projectile.Launch(target, damage, flightDuration, hitEffect);
    }
}
