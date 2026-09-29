using UnityEngine;

public class ActionCounter
{
    public int tackleCount = 0;
    public int jumpCount = 0;

    public void AddTackleCount()
    {
        tackleCount++;
    }
    public void AddJumpCount()
    {
        jumpCount++;
    }

}
