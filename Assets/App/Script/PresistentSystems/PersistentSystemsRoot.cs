using UnityEngine;

public class PersistentSystemsRoot : MonoBehaviour
{
    public static PersistentSystemsRoot Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        //DontDestroyOnLoad(gameObject);
    }
}
