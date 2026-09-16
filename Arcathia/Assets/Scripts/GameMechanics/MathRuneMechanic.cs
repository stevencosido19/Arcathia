using UnityEngine;
using TMPro;

public class MathRuneMechanic : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI equationText;
    public GameObject runeDialCanvasGroup; // Show/Hide the Dial

    [Header("Player Reference")]
    public MagicShooter shooter;

    private int currentTargetAnswer;

    public void RequestReload()
    {
        // 1. Generate a random missing value equation (e.g., 7 + _ = 15)
        int a = Random.Range(1, 10);
        int missing = Random.Range(1, 10);
        int sum = a + missing;

        currentTargetAnswer = missing;
        equationText.text = $"{a} + _ = {sum}";

        // 2. Open the Dial UI
        runeDialCanvasGroup.SetActive(true);
    }

    // Called by UI Buttons (0 through 9) on the Dial
    public void SelectRuneNumber(int number)
    {
        if (number == currentTargetAnswer)
        {
            Debug.Log("Spell Charged!");
            shooter.AddAmmo(3); // Grants 3 flame charges
            runeDialCanvasGroup.SetActive(false); // Hide dial
        }
        else
        {
            Debug.Log("Wrong Rune! Try again.");
            // Optional: Add screen shake or penalize timer
        }
    }
}