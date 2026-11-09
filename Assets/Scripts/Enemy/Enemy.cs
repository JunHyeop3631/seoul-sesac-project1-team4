using System;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float moveSpeed = 2f;

    private Vector3[] pathPoints;
    private int targetIndex;
    private Action<Enemy> onArrived;

    public bool IsMoving { get; private set; }

    public void StartMoving(Transform[] waypoints, Action<Enemy> onArrived = null)
    {
        StopMoving();
        if (waypoints == null || waypoints.Length < 2)
        {
            Debug.LogWarning("Assign at least two waypoints before moving an enemy.", this);
            return;
        }

        var points = new Vector3[waypoints.Length];
        for (int index = 0; index < waypoints.Length; index++)
        {
            if (waypoints[index] == null)
            {
                Debug.LogWarning("Enemy waypoints must not contain missing references.", this);
                return;
            }

            points[index] = waypoints[index].position;
        }

        pathPoints = points;
        this.onArrived = onArrived;
        transform.position = pathPoints[0];
        targetIndex = 1;
        IsMoving = true;
    }

    public void StopMoving()
    {
        IsMoving = false;
        pathPoints = null;
        onArrived = null;
        targetIndex = 0;
    }

    protected virtual void Update()
    {
        if (!IsMoving)
        {
            return;
        }

        float remainingDistance = Mathf.Max(0f, moveSpeed) * Time.deltaTime;
        // 모서리를 지났다면 이번 프레임의 남은 거리를 다음 구간에서 이동한다.
        while (IsMoving)
        {
            Vector3 target = pathPoints[targetIndex % pathPoints.Length];
            float distance = Vector3.Distance(transform.position, target);
            if (distance > remainingDistance)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, remainingDistance);
                return;
            }

            transform.position = target;
            remainingDistance -= distance;
            if (targetIndex == pathPoints.Length)
            {
                if (onArrived != null)
                {
                    onArrived(this);
                }
                else
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            targetIndex++;
        }
    }

    protected virtual void OnDisable()
    {
        StopMoving();
    }
}
