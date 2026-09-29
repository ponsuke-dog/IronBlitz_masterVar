using UnityEngine;

public class PlayerAnimationAudio : MonoBehaviour
{
    private PlayerController playerController;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    // AnimationEvent ‚©‚çŒÄ‚Ô
    public void PlayFootstepAudio()
    {
        if (playerController == null)
            return;

        playerController.RequestFootstepFromAnimation();
    }
}