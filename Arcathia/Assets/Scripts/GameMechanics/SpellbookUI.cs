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
    public Button attackButton;

    [Header("Equation Cooldown Settings (Seconds)")]
    public float fireCooldownTime = 10.0f;
    public float waterCooldownTime = 6.0f;
    public float lightningCooldownTime = 3.0f;

    [Header("Wrong Answer Penalty Settings")]
    public int wrongAnswerDamage = 10;

    [Header("Equation Cooldown Visuals")]
    public Image dialCooldownFillOverlay;

    // Cooldown state tracking per spell type
    private float fireCooldownTimer = 0f;
    private float waterCooldownTimer = 0f;
    private float lightningCooldownTimer = 0f;

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

    void Update()
    {
        HandleCooldowns();
    }

    private void HandleCooldowns()
    {
        if (fireCooldownTimer > 0)
        {
            fireCooldownTimer -= Time.deltaTime;
            if (fireCooldownTimer <= 0) OnCooldownComplete(SpellType.Fire);
        }

        if (waterCooldownTimer > 0)
        {
            waterCooldownTimer -= Time.deltaTime;
            if (waterCooldownTimer <= 0) OnCooldownComplete(SpellType.Water);
        }

        if (lightningCooldownTimer > 0)
        {
            lightningCooldownTimer -= Time.deltaTime;
            if (lightningCooldownTimer <= 0) OnCooldownComplete(SpellType.Lightning);
        }

        UpdateDialState();
    }

    private void OnCooldownComplete(SpellType type)
    {
        activePages[type] = equationGenerator.GenerateProblem(type);
        if (currentSpellType == type)
        {
            UpdateBookUI();
        }
    }

    public void UpdateBookUI()
    {
        MathEquationGenerator.EquationData currentEq = activePages[currentSpellType];

        if (spellTypeTitleText != null)
            spellTypeTitleText.text = $"{currentSpellType.ToString().ToUpper()} SPELL";

        equationDisplayText.text = IsCurrentSpellOnCooldown() ? "RECHARGING DIAL..." : currentEq.questionText;

        for (int i = 0; i < 4; i++)
        {
            if (i < quadrantTexts.Count)
            {
                quadrantTexts[i].text = currentEq.dialOptions[i].ToString();
            }
        }

        UpdateDialState();
    }

    private void OnQuadrantSelected(int index)
    {
        if (IsCurrentSpellOnCooldown()) return;

        MathEquationGenerator.EquationData currentEq = activePages[currentSpellType];
        int selectedValue = currentEq.dialOptions[index];

        if (selectedValue == currentEq.correctAnswer)
        {
            Debug.Log($"Correct! Adding +1 Ammo to {currentSpellType}!");

            PlayerMagicNetwork localPlayer = GetLocalPlayerMagic();
            if (localPlayer != null)
            {
                localPlayer.AddSpellAmmo(currentSpellType, 1);
            }

            StartCooldownForCurrentSpell();
            UpdateBookUI();
        }
        else
        {
            Debug.Log($"Wrong Answer! Player takes {wrongAnswerDamage} self-damage!");

            // Take self damage via ServerRpc
            PlayerHealth localHealth = GetLocalPlayerHealth();
            if (localHealth != null)
            {
                localHealth.TakeDamageServerRpc(wrongAnswerDamage);
            }

            // Generate new problem on wrong answer
            activePages[currentSpellType] = equationGenerator.GenerateProblem(currentSpellType);
            UpdateBookUI();
        }
    }

    private void OnAttackButtonPressed()
    {
        PlayerMagicNetwork localPlayer = GetLocalPlayerMagic();
        if (localPlayer != null)
        {
            localPlayer.CastSpell(currentSpellType);
            UpdateDialState(); // Re-evaluate attack button interactability
        }
    }

    private void StartCooldownForCurrentSpell()
    {
        switch (currentSpellType)
        {
            case SpellType.Fire:
                fireCooldownTimer = fireCooldownTime;
                break;
            case SpellType.Water:
                waterCooldownTimer = waterCooldownTime;
                break;
            case SpellType.Lightning:
                lightningCooldownTimer = lightningCooldownTime;
                break;
        }
    }

    private bool IsCurrentSpellOnCooldown()
    {
        return currentSpellType switch
        {
            SpellType.Fire => fireCooldownTimer > 0,
            SpellType.Water => waterCooldownTimer > 0,
            SpellType.Lightning => lightningCooldownTimer > 0,
            _ => false
        };
    }

    private void UpdateDialState()
    {
        float currentTimer = currentSpellType switch
        {
            SpellType.Fire => fireCooldownTimer,
            SpellType.Water => waterCooldownTimer,
            SpellType.Lightning => lightningCooldownTimer,
            _ => 0f
        };

        float maxCooldown = currentSpellType switch
        {
            SpellType.Fire => fireCooldownTime,
            SpellType.Water => waterCooldownTime,
            SpellType.Lightning => lightningCooldownTime,
            _ => 1f
        };

        bool isOnCooldown = currentTimer > 0;

        // Enable or disable math answer buttons based on cooldown
        for (int i = 0; i < quadrantButtons.Count; i++)
        {
            if (quadrantButtons[i] != null)
            {
                quadrantButtons[i].interactable = !isOnCooldown;
            }
        }

        // Check if player currently has ammo for the selected element
        PlayerMagicNetwork localPlayer = GetLocalPlayerMagic();
        bool hasAmmo = localPlayer != null && localPlayer.HasAmmo(currentSpellType);

        if (attackButton != null)
        {
            // Attack button requires ammo to be enabled
            attackButton.interactable = hasAmmo;
        }

        if (dialCooldownFillOverlay != null)
        {
            dialCooldownFillOverlay.enabled = isOnCooldown;
            dialCooldownFillOverlay.fillAmount = isOnCooldown ? (currentTimer / maxCooldown) : 0f;
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
                // Empty dot opacity
                dots[i].enabled = true;
                dots[i].color = new Color(1f, 1f, 1f, 0.2f);
            }
        }

        UpdateDialState();
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

    private PlayerHealth GetLocalPlayerHealth()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (PlayerHealth p in players)
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