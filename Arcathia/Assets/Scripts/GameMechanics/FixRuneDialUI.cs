using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RuneDialController))]
public class FixRuneDialUI : MonoBehaviour
{
    private void Awake()
    {
        RuneDialController dial = GetComponent<RuneDialController>();

        // Auto-find choice buttons if empty
        if (dial.choiceButtons == null || dial.choiceButtons.Length < 4 || dial.choiceButtons[0] == null)
        {
            Button[] foundButtons = GetComponentsInChildren<Button>();
            dial.choiceButtons = new Button[4];
            dial.choiceTexts = new TextMeshProUGUI[4];

            int choiceIndex = 0;
            foreach (Button btn in foundButtons)
            {
                // Ignore the central Fire Button
                if (btn.gameObject == dial.fireButton?.gameObject || btn.name.ToLower().Contains("fire") || btn.name.ToLower().Contains("cast"))
                    continue;

                if (choiceIndex < 4)
                {
                    dial.choiceButtons[choiceIndex] = btn;
                    dial.choiceTexts[choiceIndex] = btn.GetComponentInChildren<TextMeshProUGUI>();
                    choiceIndex++;
                }
            }
        }
    }
}