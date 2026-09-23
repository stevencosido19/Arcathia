using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class RuneDialController : MonoBehaviour
{
    [Header("Dependencies")]
    public ElementalSpellBook spellBook;

    [Header("UI Dial References")]
    public Button[] choiceButtons = new Button[4];
    public TextMeshProUGUI[] choiceTexts = new TextMeshProUGUI[4];
    public Button fireButton;

    private int[] currentChoices = new int[4];

    private void Start()
    {
        // Wire up choice button click listeners
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int index = i;
            if (choiceButtons[i] != null)
            {
                choiceButtons[i].onClick.RemoveAllListeners();
                choiceButtons[i].onClick.AddListener(() => OnChoiceButtonPressed(index));
            }
        }

        // Wire up fire / cast button listener
        if (fireButton != null)
        {
            fireButton.onClick.RemoveAllListeners();
            fireButton.onClick.AddListener(OnFireButtonPressed);
        }
    }

    // Disables or enables answer buttons when ammo is full/available
    public void SetDialInteractable(bool interactable)
    {
        if (choiceButtons != null)
        {
            foreach (Button btn in choiceButtons)
            {
                if (btn != null)
                {
                    btn.interactable = interactable;
                }
            }
        }
    }

    public void RegenerateDialChoices(int correctAnswer)
    {
        List<int> wrongOptions = new List<int>();

        while (wrongOptions.Count < 3)
        {
            int offset = Random.Range(-4, 5);
            int fakeAns = correctAnswer + offset;

            if (fakeAns >= 0 && fakeAns != correctAnswer && !wrongOptions.Contains(fakeAns))
            {
                wrongOptions.Add(fakeAns);
            }
        }

        int correctSlot = Random.Range(0, 4);
        int wrongIdx = 0;

        for (int i = 0; i < 4; i++)
        {
            if (i == correctSlot)
            {
                currentChoices[i] = correctAnswer;
            }
            else
            {
                currentChoices[i] = wrongOptions[wrongIdx];
                wrongIdx++;
            }

            if (choiceTexts != null && i < choiceTexts.Length && choiceTexts[i] != null)
            {
                choiceTexts[i].text = currentChoices[i].ToString();
                choiceTexts[i].SetAllDirty();
            }
        }
    }

    private void OnChoiceButtonPressed(int buttonIndex)
    {
        if (spellBook != null)
        {
            spellBook.SubmitRuneAnswer(currentChoices[buttonIndex]);
        }
    }

    private void OnFireButtonPressed()
    {
        if (spellBook != null && spellBook.shooter != null)
        {
            spellBook.shooter.TryCastSpell();
        }
    }
}