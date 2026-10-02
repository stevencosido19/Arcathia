using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerScoreSlotUI : MonoBehaviour
{
    [Header("UI References")]
    public Image avatarIcon;
    public TMP_Text scoreText;

    private PlayerScore boundPlayer;

    public void SetupSlot(PlayerScore player, Sprite icon)
    {
        if (boundPlayer != null)
        {
            boundPlayer.score.OnValueChanged -= OnScoreChanged;
        }

        boundPlayer = player;
        transform.localScale = Vector3.one;

        if (avatarIcon != null && icon != null)
        {
            avatarIcon.sprite = icon;
            avatarIcon.color = Color.white;
            avatarIcon.enabled = true;
        }

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
        else
        {
            Debug.LogError($"<color=red>[PlayerScoreSlotUI ERROR]</color> ScoreText TMP_Text reference is NULL on {gameObject.name}!");
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