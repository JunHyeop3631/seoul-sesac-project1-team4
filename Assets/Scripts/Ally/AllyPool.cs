using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public sealed class AllyPool : MonoBehaviour
{
    private readonly Dictionary<Ally, ObjectPool<Ally>> pools = new();
    private readonly Dictionary<Ally, ObjectPool<Ally>> owners = new();

    private ObjectPool<Ally> GetPool(Ally prefab)
    {
        if (!pools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<Ally>(
                () =>
                {
                    Ally ally = Instantiate(prefab, transform);
                    ally.gameObject.SetActive(false);
                    return ally;
                },
                actionOnRelease: ally =>
                {
                    ally.gameObject.SetActive(false);
                    ally.transform.SetParent(transform, false);
                },
                actionOnDestroy: ally =>
                {
                    owners.Remove(ally);
                    Destroy(ally.gameObject);
                });
            pools.Add(prefab, pool);
        }

        return pool;
    }

    internal void Register(Ally ally, Ally prefab)
    {
        if (!owners.ContainsKey(ally))
            owners.Add(ally, GetPool(prefab));
    }

    public Ally Request(Ally prefab, Transform cell)
    {
        var pool = GetPool(prefab);
        Ally ally = pool.Get();
        owners[ally] = pool;
        ally.transform.SetParent(cell, false);
        ally.transform.localPosition = Vector3.zero;
        ally.gameObject.SetActive(true);
        ally.ResetForReuse();
        return ally;
    }

    public void Return(Ally ally)
    {
        if (ally != null && ally.gameObject.activeSelf && owners.TryGetValue(ally, out var pool))
            pool.Release(ally);
    }

    private void OnDestroy()
    {
        foreach (var pool in pools.Values)
            pool.Dispose();
        pools.Clear();
        owners.Clear();
    }
}
