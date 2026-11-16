using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public sealed class HitEffectPool : MonoBehaviour
{
    private readonly Dictionary<HitEffect, ObjectPool<HitEffect>> pools = new();

    public void Play(HitEffect prefab, Vector3 position)
    {
        if (!pools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<HitEffect>(
                () =>
                {
                    HitEffect effect = Instantiate(prefab, transform);
                    effect.gameObject.SetActive(false);
                    effect.ReturnToPool = pool.Release;
                    return effect;
                },
                actionOnRelease: effect => effect.gameObject.SetActive(false),
                actionOnDestroy: effect => Destroy(effect.gameObject));
            pools.Add(prefab, pool);
        }

        HitEffect result = pool.Get();
        result.transform.SetPositionAndRotation(position, Quaternion.identity);
        result.gameObject.SetActive(true);
        result.Play();
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
