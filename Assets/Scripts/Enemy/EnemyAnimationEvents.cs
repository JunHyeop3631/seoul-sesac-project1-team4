using UnityEngine;

public class EnemyAnimationEvents : MonoBehaviour
{
    private Enemy enemy;

    private void Awake()
    {
        enemy = GetComponentInParent<Enemy>();
    }

    // Animation Events are sent to components on the Animator's GameObject.
    public void OnDeathAnimationFinished()
    {
        if (enemy != null)
            enemy.CompleteDeathAnimation();
    }
}
