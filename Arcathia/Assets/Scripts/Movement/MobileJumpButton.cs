using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MobileJumpButton : MonoBehaviour
{
    private Button jumpButton;

    void Start()
    {
        jumpButton = GetComponent<Button>();
        jumpButton.onClick.AddListener(OnJumpButtonPressed);
    }

    private void OnJumpButtonPressed()
    {
        // Find all player controllers in the scene
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        foreach (PlayerController player in players)
        {
            // Trigger jump ONLY for your local player instance
            if (player.IsOwner)
            {
                player.TriggerJump();
                break;
            }
        }
    }
}