using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerScoreSlotUI : MonoBehaviour
{
    [Header("UI References")]
    public Image avatarIcon;
    public TMP_Text playerNameText; // (Optional) Shows the player's login name
    public TMP_Text scoreText;

    private PlayerScore boundPlayer;

    public void SetupSlot(PlayerScore player, Sprite icon)
    {
        // Unsubscribe old events if recycled
        if (boundPlayer != null)
        {
            boundPlayer.score.OnValueChanged -= OnScoreChanged;
        }

        boundPlayer = player;
        transform.localScale = Vector3.one;

        // 1. Setup Avatar Icon
        if (avatarIcon != null)
        {
            if (icon != null)
            {
                avatarIcon.sprite = icon;
                avatarIcon.color = Color.white;
                avatarIcon.enabled = true;
            }
            else
            {
                avatarIcon.enabled = false;
            }
        }

        // 2. Setup Player Name
        if (playerNameText != null && boundPlayer != null)
        {
            playerNameText.text = boundPlayer.GetPlayerName();
        }

        // 3. Setup Score
        if (boundPlayer != null)
        {
            UpdateScoreDisplay(boundPlayer.score.Value);
            boundPlayer.score.OnValueChanged += OnScoreChanged;
        }
    }

    private void OnScoreChanged(int oldScore, int newScore)
    {
        UpdateScoreDisplay(newScore);
    }

    public void UpdateScoreDisplay(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = score.ToString();
        }
    }

    private void OnDestroy()
    {
        if (boundPlayer != null)
        {
            boundPlayer.score.OnValueChanged -= OnScoreChanged;
        }
    }
}