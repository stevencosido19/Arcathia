using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeathScreenUI : MonoBehaviour
{
    public static DeathScreenUI Instance { get; private set; }

    [Header("UI Panels")]
    [Tooltip("The parent dark overlay panel for the death screen.")]
    public GameObject deathScreenPanel;

    [Header("Text References")]
    public TMP_Text countdownTimerText;
    public TMP_Text spectatingTargetNameText;

    [Header("Spectate Switching Buttons")]
    public Button nextPlayerButton;
    public Button prevPlayerButton;

    private Coroutine countdownCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (deathScreenPanel != null)
        {
            deathScreenPanel.SetActive(false);
        }

        if (nextPlayerButton != null) nextPlayerButton.onClick.AddListener(OnNextPlayerClicked);
        if (prevPlayerButton != null) prevPlayerButton.onClick.AddListener(OnPrevPlayerClicked);
    }

    public void ShowDeathScreen(float duration)
    {
        if (deathScreenPanel != null)
        {
            deathScreenPanel.SetActive(true);
        }

        if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
        countdownCoroutine = StartCoroutine(CountdownRoutine(duration));
    }

    public void HideDeathScreen()
    {
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        if (deathScreenPanel != null)
        {
            deathScreenPanel.SetActive(false);
        }
    }

    private IEnumerator CountdownRoutine(float timeRemaining)
    {
        while (timeRemaining > 0f)
        {
            if (countdownTimerText != null)
            {
                countdownTimerText.text = $"Respawning in {Mathf.CeilToInt(timeRemaining)}s...";
            }

            yield return new WaitForSeconds(1.0f);
            timeRemaining -= 1.0f;
        }

        if (countdownTimerText != null)
        {
            countdownTimerText.text = "Respawning...";
        }
    }

    public void UpdateSpectatingName(string targetName)
    {
        if (spectatingTargetNameText != null)
        {
            spectatingTargetNameText.text = $"Spectating: <color=yellow>{targetName}</color>";
        }
    }

    private void OnNextPlayerClicked()
    {
        if (ThirdPersonCamera.LocalCamera != null)
        {
            ThirdPersonCamera.LocalCamera.CycleSpectateTarget(1);
        }
    }

    private void OnPrevPlayerClicked()
    {
        if (ThirdPersonCamera.LocalCamera != null)
        {
            ThirdPersonCamera.LocalCamera.CycleSpectateTarget(-1);
        }
    }
}