using UnityEngine;

public class EnemyPatrolRouteManager : MonoBehaviour
{
    [SerializeField]
    private EnemyPatrolRoute[] routes;

    public EnemyPatrolRoute[] Routes => routes;
    public bool HasRoutes => routes != null && routes.Length > 0;

    public bool TryFindBestReachableRoute(
        Vector3 fromPosition,
        EnemyNavMotor motor,
        out EnemyPatrolRoute bestRoute,
        out int bestPointIndex,
        EnemyPatrolRoute excludeRoute = null)
    {
        bestRoute = null;
        bestPointIndex = -1;

        if (!HasRoutes || motor == null)
            return false;

        float bestPathLength = float.MaxValue;

        for (int i = 0; i < routes.Length; i++)
        {
            EnemyPatrolRoute route = routes[i];
            if (route == null || !route.HasPoints)
                continue;

            if (route == excludeRoute)
                continue;

            int reachableIndex = route.FindNearestReachableIndex(fromPosition, motor);
            if (reachableIndex < 0)
                continue;

            float pathLength = motor.GetPathLength(route.GetPoint(reachableIndex));
            if (pathLength < bestPathLength)
            {
                bestPathLength = pathLength;
                bestRoute = route;
                bestPointIndex = reachableIndex;
            }
        }

        return bestRoute != null;
    }
}