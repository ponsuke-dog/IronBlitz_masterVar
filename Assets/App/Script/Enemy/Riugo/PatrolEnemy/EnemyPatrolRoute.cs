using UnityEngine;

public class EnemyPatrolRoute : MonoBehaviour
{

    [SerializeField]
    private Transform[] patrolPoints;

    public bool HasPoints => patrolPoints != null && patrolPoints.Length > 0;
    public int Count => HasPoints ? patrolPoints.Length : 0;

    public Vector3 GetPoint(int index)
    {
        if (!HasPoints)
            return transform.position;

        index = Mathf.Clamp(index, 0, patrolPoints.Length - 1);

        Transform point = patrolPoints[index];
        return point != null ? point.position : transform.position;
    }

    public int GetNextIndex(int current)
    {
        if (!HasPoints)
            return 0;

        return (current + 1) % patrolPoints.Length;
    }

    public int FindNearestIndex(Vector3 pos)
    {
        if (!HasPoints)
            return 0;

        int nearestIndex = 0;
        float nearestSqr = float.MaxValue;

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] == null)
                continue;

            float sqr = (patrolPoints[i].position - pos).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    public int FindNearestReachableIndex(Vector3 pos, EnemyNavMotor motor)
    {
        if (!HasPoints || motor == null)
            return -1;

        int nearestIndex = FindNearestIndex(pos);

        Vector3 point = GetPoint(nearestIndex);

        if (!motor.CanReach(point))
            return -1;

        return nearestIndex;
    }

    private void OnDrawGizmosSelected()
    {
        if (!HasPoints)
            return;

        Gizmos.color = Color.green;

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] == null) continue;

            Gizmos.DrawSphere(patrolPoints[i].position, 0.2f);

            int next = (i + 1) % patrolPoints.Length;
            if (patrolPoints[next] != null)
            {
                Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[next].position);
            }
        }
    }
}
