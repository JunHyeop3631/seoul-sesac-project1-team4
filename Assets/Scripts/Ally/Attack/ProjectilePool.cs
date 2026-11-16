using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public sealed class ProjectilePool : MonoBehaviour
{
    private readonly Dictionary<HomingProjectile, ObjectPool<HomingProjectile>> pools = new();

    public HomingProjectile Request(HomingProjectile prefab, Vector3 position)
    {
        if (!pools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<HomingProjectile>(
                () =>
                {
                    var projectile = Instantiate(prefab, transform);
                    projectile.gameObject.SetActive(false);
                    projectile.ReturnToPool = pool.Release;
                    return projectile;
                },
                actionOnRelease: projectile => projectile.gameObject.SetActive(false),
                actionOnDestroy: projectile => Destroy(projectile.gameObject));
            pools.Add(prefab, pool);
        }

        HomingProjectile result = pool.Get();
        result.transform.SetPositionAndRotation(position, Quaternion.identity);
        result.gameObject.SetActive(true);
        return result;
    }

    private void OnDestroy()
    {
        foreach (var pool in pools.Values)
        {
            pool.Dispose();
        }

        pools.Clear();
    }
}
