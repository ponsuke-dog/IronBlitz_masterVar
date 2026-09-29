using UnityEngine;

public enum EnemySurfaceKind
{
    Floor,
    Wall,
    Ceiling
}

public struct EnemySurfaceHit
{
    public Vector3 normal;
    public EnemySurfaceKind kind;
    public Collider other;
}

public static class EnemySurfaceUtility
{
    public static bool TryBuildSurfaceHit(
        Collider selfCollider,
        Collider other,
        Vector3 fallbackPosition,
        Vector3 fallbackTravelDirection,
        float floorNormalYThreshold,
        float wallNormalYThreshold,
        float ceilingNormalYThreshold,
        float slopeFloorAssistYThreshold,
        out EnemySurfaceHit hit)
    {
        hit = default;

        if (selfCollider == null || other == null)
            return false;

        Vector3 normal = Vector3.zero;
        bool gotNormal = false;

        // 1. penetration ベース
        bool gotPenetration = Physics.ComputePenetration(
            selfCollider, selfCollider.transform.position, selfCollider.transform.rotation,
            other, other.transform.position, other.transform.rotation,
            out Vector3 penetrationDir,
            out float penetrationDist
        );

        if (gotPenetration && penetrationDist > 0.0001f && penetrationDir.sqrMagnitude > 0.0001f)
        {
            normal = penetrationDir.normalized;
            gotNormal = true;
        }

        // 2. ClosestPoint ベース
        if (!gotNormal)
        {
            Vector3 closest = other.ClosestPoint(fallbackPosition);
            Vector3 cpNormal = fallbackPosition - closest;

            if (cpNormal.sqrMagnitude > 0.0001f)
            {
                normal = cpNormal.normalized;
                gotNormal = true;
            }
        }

        // 3. bounds center ベース
        if (!gotNormal)
        {
            Vector3 centerNormal = fallbackPosition - other.bounds.center;
            if (centerNormal.sqrMagnitude > 0.0001f)
            {
                normal = centerNormal.normalized;
                gotNormal = true;
            }
        }

        // 4. 最後の保険
        if (!gotNormal)
        {
            Vector3 fallback = -fallbackTravelDirection;
            if (fallback.sqrMagnitude < 0.0001f)
                fallback = Vector3.up;

            normal = fallback.normalized;
            gotNormal = true;
        }

        if (!gotNormal)
            return false;

        Vector3 travelDir = fallbackTravelDirection;
        if (travelDir.sqrMagnitude > 0.0001f)
            travelDir.Normalize();

        float upDot = Vector3.Dot(normal, Vector3.up);

        EnemySurfaceKind kind;

        if (upDot >= floorNormalYThreshold)
        {
            kind = EnemySurfaceKind.Floor;
        }
        else if (upDot <= -ceilingNormalYThreshold)
        {
            kind = EnemySurfaceKind.Ceiling;
        }
        else if (upDot > slopeFloorAssistYThreshold)
        {
            kind = EnemySurfaceKind.Floor;
        }
        else if (Mathf.Abs(upDot) <= wallNormalYThreshold)
        {
            kind = EnemySurfaceKind.Wall;
        }
        else
        {
            kind = upDot > 0f ? EnemySurfaceKind.Floor : EnemySurfaceKind.Ceiling;
        }

        // 高い床・段差上面の補正
        if (kind != EnemySurfaceKind.Floor)
        {
            if (TryResolveFloorByDownRayOnSameCollider(
                    selfCollider,
                    other,
                    fallbackPosition,
                    floorNormalYThreshold,
                    slopeFloorAssistYThreshold,
                    out RaycastHit downHit))
            {
                normal = downHit.normal.normalized;
                kind = EnemySurfaceKind.Floor;
            }
        }

        hit = new EnemySurfaceHit
        {
            normal = normal,
            kind = kind,
            other = other
        };

        return true;
    }

    private static bool TryResolveFloorByDownRayOnSameCollider(
        Collider selfCollider,
        Collider other,
        Vector3 fallbackPosition,
        float floorNormalYThreshold,
        float slopeFloorAssistYThreshold,
        out RaycastHit hit)
    {
        hit = default;

        if (selfCollider == null || other == null)
            return false;

        Bounds selfBounds = selfCollider.bounds;

        float probeUp = Mathf.Max(selfBounds.extents.y + 0.25f, 0.6f);
        float probeDistance = Mathf.Max(selfBounds.size.y + 1.2f, 2.2f);

        Vector3 rayOrigin = fallbackPosition + Vector3.up * probeUp;
        Ray ray = new Ray(rayOrigin, Vector3.down);

        if (!other.Raycast(ray, out hit, probeDistance))
            return false;

        float upDot = Vector3.Dot(hit.normal.normalized, Vector3.up);

        if (upDot >= floorNormalYThreshold || upDot > slopeFloorAssistYThreshold)
            return true;

        return false;
    }
}