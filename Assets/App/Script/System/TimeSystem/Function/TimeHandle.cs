// ============================================
// è“®Modifier§Œäƒnƒ“ƒhƒ‹
// ============================================

public class TimeHandle
{
    TimeModifier modifier;

    public TimeHandle(TimeModifier mod)
    {
        modifier = mod;
    }

    public void End()
    {
        modifier.ForceExpire();
    }
}