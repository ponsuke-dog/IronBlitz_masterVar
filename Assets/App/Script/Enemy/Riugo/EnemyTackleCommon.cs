using UnityEngine;


[System.Serializable]
public class TackleBlowProfile
{
    [Header("Launch")]
    [Range(0f, 1f)] public float minPowerRatio = 0.25f;
    public float minUpwardPower = 7.0f;
    public float minLaunchAngle = 20f;
    public float maxLaunchAngle = 30f;
    public float minLaunchSpeed = 1f;
    public float horizontalLaunchBoost = 2f;
}

[System.Serializable]
public class TackleBlowSet
{
    public TackleBlowProfile normal = new TackleBlowProfile();

    public TackleBlowProfile charge = new TackleBlowProfile
    {
        minPowerRatio = 0.35f,
        minUpwardPower = 7.0f,
        minLaunchAngle = 20f,
        maxLaunchAngle = 30f,
        minLaunchSpeed = 1.25f,
        horizontalLaunchBoost = 2f
    };

    public TackleBlowProfile normaljust = new TackleBlowProfile
    {
        minPowerRatio = 0.5f,
        minUpwardPower = 8.0f,
        minLaunchAngle = 25f,
        maxLaunchAngle = 35f,
        minLaunchSpeed = 2f,
        horizontalLaunchBoost = 3f
    };

    public TackleBlowProfile chargejust = new TackleBlowProfile
    {
        minPowerRatio = 0.7f,
        minUpwardPower = 8.0f,
        minLaunchAngle = 25f,
        maxLaunchAngle = 35f,
        minLaunchSpeed = 2.5f,
        horizontalLaunchBoost = 3f
    };

    public TackleBlowProfile GetProfile(TackleType type)
    {
        switch (type)
        {
            case TackleType.Charge:
                return charge;
            case TackleType.Normal:
                return normal;
            case TackleType.JustCharge:
                return chargejust;
            case TackleType.JustNormal:
                return normaljust;
            default:
                return null;
        }
    }
}