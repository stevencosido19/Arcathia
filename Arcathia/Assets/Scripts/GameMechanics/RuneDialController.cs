using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RuneDialController : MonoBehaviour
{
    [Header("UI Element References")]
    public Button[] choiceButtons;
    public TMP_Text[] choiceTexts;
    public Button fireButton;
    public CanvasGroup dialCanvasGroup;

    [Header("Connected Player References")]
    public MagicShooter shooter;
    public ElementalSpellBook spellBook;

    private void Awake()
    {
        if (dialCanvasGroup == null)
        {
            dialCanvasGroup = GetComponent<CanvasGroup>();
        }
    }

    public void BindToLocalPlayer(MagicShooter localShooter, ElementalSpellBook localBook)
    {
        shooter = localShooter;
        spellBook = localBook;

        if (fireButton != null)
        {
            fireButton.onClick.RemoveAllListeners();
            fireButton.onClick.AddListener(OnFireButtonClicked);
        }

        RegenerateDialChoices();
    }

    public void SetDialInteractable(bool interactable)
    {
        if (dialCanvasGroup != null)
        {
            dialCanvasGroup.interactable = interactable;
            dialCanvasGroup.blocksRaycasts = interactable;
        }

        if (choiceButtons != null)
        {
            foreach (var btn in choiceButtons)
            {
                if (btn != null) btn.interactable = interactable;
            }
        }

        if (fireButton != null)
        {
            fireButton.interactable = interactable;
        }
    }

    // --- REGENERATE DIAL CHOICES OVERLOADS ---

    /// <summary>
    /// Overload 1: Takes 0 arguments (Default refresh)
    /// </summary>
    public void RegenerateDialChoices()
    {
        // Refreshes UI based on current spellbook state
    }

    /// <summary>
    /// Overload 2: Takes 1 argument (e.g. ElementType or array of elements/spells passed from ElementalSpellBook.cs)
    /// </summary>
    public void RegenerateDialChoices<T>(T elementOrChoices)
    {
        // Handles 1-argument calls from ElementalSpellBook.cs (e.g. RegenerateDialChoices(currentElements))
        if (choiceTexts != null && choiceTexts.Length > 0)
        {
            // Update UI buttons or text based on the passed choices/element
        }
    }

    public void OnFireButtonClicked()
    {
        if (shooter != null && spellBook != null)
        {
            shooter.TryCastSpell(spellBook.CurrentElementType);
        }
    }
}