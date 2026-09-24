using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SpellbookUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("UI Display References")]
    public TMP_Text equationDisplayText;
    public TMP_Text spellTypeTitleText;
    public List<TMP_Text> quadrantTexts;
    public List<Button> quadrantButtons;

    [Header("Ammo HUD Elements")]
    public List<Image> fireAmmoDots;
    public List<Image> waterAmmoDots;
    public List<Image> lightningAmmoDots;
    public Button attackButton; // The dagger/shoot button in your sketch

    [Header("Dependencies")]
    public MathEquationGenerator equationGenerator;

    private Dictionary<SpellType, MathEquationGenerator.EquationData> activePages
        = new Dictionary<SpellType, MathEquationGenerator.EquationData>();

    private SpellType currentSpellType = SpellType.Fire;
    private Vector2 touchStartPos;
    public float swipeThreshold = 50f;

    void Start()
    {
        activePages[SpellType.Fire] = equationGenerator.GenerateProblem(SpellType.Fire);
        activePages[SpellType.Water] = equationGenerator.GenerateProblem(SpellType.Water);
        activePages[SpellType.Lightning] = equationGenerator.GenerateProblem(SpellType.Lightning);

        for (int i = 0; i < quadrantButtons.Count; i++)
        {
            int index = i;
            quadrantButtons[i].onClick.AddListener(() => OnQuadrantSelected(index));
        }

        if (attackButton != null)
        {
            attackButton.onClick.AddListener(OnAttackButtonPressed);
        }

        UpdateBookUI();
    }

    public void UpdateBookUI()
    {
        MathEquationGenerator.EquationData currentEq = activePages[currentSpellType];

        if (spellTypeTitleText != null)
            spellTypeTitleText.text = $"{currentSpellType.ToString().ToUpper()} SPELL";

        equationDisplayText.text = currentEq.questionText;

        for (int i = 0; i < 4; i++)
        {
            if (i < quadrantTexts.Count)
            {
                quadrantTexts[i].text = currentEq.dialOptions[i].ToString();
            }
        }
    }

    private void OnQuadrantSelected(int index)
    {
        MathEquationGenerator.EquationData currentEq = activePages[currentSpellType];
        int selectedValue = currentEq.dialOptions[index];

        if (selectedValue == currentEq.correctAnswer)
        {
            Debug.Log($"Correct! Adding +1 Ammo to {currentSpellType}!");

            // Charge spell ammo on local player
            PlayerMagicNetwork localPlayer = GetLocalPlayerMagic();
            if (localPlayer != null)
            {
                localPlayer.AddSpellAmmo(currentSpellType, 1);
            }

            // Generate new equation for this spell type
            activePages[currentSpellType] = equationGenerator.GenerateProblem(currentSpellType);
            UpdateBookUI();
        }
        else
        {
            Debug.Log("Wrong Answer!");
        }
    }

    private void OnAttackButtonPressed()
    {
        PlayerMagicNetwork localPlayer = GetLocalPlayerMagic();
        if (localPlayer != null)
        {
            // Shoots the currently active spell type in the book
            localPlayer.CastSpell(currentSpellType);
        }
    }

    public void UpdateAmmoUI(SpellType type, int currentCount, int maxCount)
    {
        List<Image> dots = type switch
        {
            SpellType.Fire => fireAmmoDots,
            SpellType.Water => waterAmmoDots,
            SpellType.Lightning => lightningAmmoDots,
            _ => null
        };

        if (dots == null) return;

        for (int i = 0; i < dots.Count; i++)
        {
            if (i < currentCount)
            {
                dots[i].enabled = true;
                dots[i].color = Color.white; // Active Dot
            }
            else
            {
                // Dimmed or disabled dot for empty ammo
                dots[i].enabled = true;
                dots[i].color = new Color(1f, 1f, 1f, 0.2f);
            }
        }
    }

    private PlayerMagicNetwork GetLocalPlayerMagic()
    {
        PlayerMagicNetwork[] players = FindObjectsByType<PlayerMagicNetwork>(FindObjectsSortMode.None);
        foreach (PlayerMagicNetwork p in players)
        {
            if (p.IsOwner) return p;
        }
        return null;
    }

    public void CyclePage(bool swipeRight)
    {
        int totalTypes = System.Enum.GetValues(typeof(SpellType)).Length;
        int currentIndex = (int)currentSpellType;

        if (swipeRight) currentIndex = (currentIndex + 1) % totalTypes;
        else currentIndex = (currentIndex - 1 + totalTypes) % totalTypes;

        currentSpellType = (SpellType)currentIndex;
        UpdateBookUI();
    }

    public void OnPointerDown(PointerEventData eventData) => touchStartPos = eventData.position;

    public void OnPointerUp(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - touchStartPos;
        if (Mathf.Abs(delta.x) > swipeThreshold)
        {
            if (delta.x < 0) CyclePage(true);
            else CyclePage(false);
        }
    }
}