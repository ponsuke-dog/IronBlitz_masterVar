using UnityEngine;

public enum TackleType
{
    Normal,
    Charge,
    JustNormal,
    JustCharge
}

public struct BlowPayload
{
    public TackleType tackleType;
    public float damageConstant;
    public float powerConstant;
    public float powerRate;
    public Vector3 powerDirection;
}



public class ChainPayload
{
    public IHitSource source;

    public int chainIndex;

    public Vector3 direction;

    public float horizontalPower;
    public float verticalPower;

    public float damage;
}
