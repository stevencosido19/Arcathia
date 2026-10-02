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

    [Header("Fire Cooldown Settings (Seconds)")]
    public float fireCooldownTime = 10.0f;

    [Header("Wrong Answer Penalty Settings")]
    public int wrongAnswerDamage = 10;

    [Header("Equation Cooldown Visuals")]
    public Image dialCooldownFillOverlay;

    // Fire dial cooldown tracking
    private float fireCooldownTimer = 0f;

    [Header("Rune Inventory Queues")]
    // Tracks stored single-use equations for picked-up runes
    private Queue<MathEquationGenerator.EquationData> waterRuneQueue = new Queue<MathEquationGenerator.EquationData>();
    private Queue<MathEquationGenerator.EquationData> lightningRuneQueue = new Queue<MathEquationGenerator.EquationData>();

    // Current Fire Problem (Permanent)
    private MathEquationGenerator.EquationData currentFireEquation;

    [Header("Dependencies")]
    public MathEquationGenerator equationGenerator;

    private SpellType currentSpellType = SpellType.Fire;
    private Vector2 touchStartPos;
    public float swipeThreshold = 50f;

    void Start()
    {
        // Generate initial permanent Fire addition problem
        currentFireEquation = equationGenerator.GenerateProblem(SpellType.Fire);

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
        HandleFireCooldown();
    }

    private void HandleFireCooldown()
    {
        if (fireCooldownTimer > 0)
        {
            fireCooldownTimer -= Time.deltaTime;
            if (fireCooldownTimer <= 0)
            {
                // Generate a fresh Fire equation when cooldown ends
                currentFireEquation = equationGenerator.GenerateProblem(SpellType.Fire);
                if (currentSpellType == SpellType.Fire) UpdateBookUI();
            }
        }

        UpdateDialState();
    }

    // Called by RunePickup.cs when player walks over a rune in the stage
    public void AddRuneToInventory(SpellType type)
    {
        MathEquationGenerator.EquationData newEq = equationGenerator.GenerateProblem(type);

        if (type == SpellType.Water)
        {
            waterRuneQueue.Enqueue(newEq);
        }
        else if (type == SpellType.Lightning)
        {
            lightningRuneQueue.Enqueue(newEq);
        }

        UpdateBookUI();
    }

    public void UpdateBookUI()
    {
        if (spellTypeTitleText != null)
            spellTypeTitleText.text = $"{currentSpellType.ToString().ToUpper()} SPELL";

        // Check availability per spell type
        if (currentSpellType == SpellType.Fire)
        {
            if (fireCooldownTimer > 0)
            {
                SetDialText("RECHARGING DIAL...", "", "", "", "");
            }
            else
            {
                DisplayEquation(currentFireEquation);
            }
        }
        else if (currentSpellType == SpellType.Water)
        {
            if (waterRuneQueue.Count > 0)
            {
                DisplayEquation(waterRuneQueue.Peek());
            }
            else
            {
                SetDialText($"NO WATER RUNES ({waterRuneQueue.Count})", "-", "-", "-", "-");
            }
        }
        else if (currentSpellType == SpellType.Lightning)
        {
            if (lightningRuneQueue.Count > 0)
            {
                DisplayEquation(lightningRuneQueue.Peek());
            }
            else
            {
                SetDialText($"NO LIGHTNING RUNES ({lightningRuneQueue.Count})", "-", "-", "-", "-");
            }
        }

        UpdateDialState();
    }

    private void DisplayEquation(MathEquationGenerator.EquationData eq)
    {
        equationDisplayText.text = eq.questionText;
        for (int i = 0; i < 4; i++)
        {
            if (i < quadrantTexts.Count)
            {
                quadrantTexts[i].text = eq.dialOptions[i].ToString();
            }
        }
    }

    private void SetDialText(string question, string q1, string q2, string q3, string q4)
    {
        equationDisplayText.text = question;
        string[] opts = { q1, q2, q3, q4 };
        for (int i = 0; i < 4; i++)
        {
            if (i < quadrantTexts.Count) quadrantTexts[i].text = opts[i];
        }
    }

    private void OnQuadrantSelected(int index)
    {
        // Fire Cooldown Check
        if (currentSpellType == SpellType.Fire && fireCooldownTimer > 0) return;

        // Check if Rune exists for Water or Lightning
        if (currentSpellType == SpellType.Water && waterRuneQueue.Count == 0) return;
        if (currentSpellType == SpellType.Lightning && lightningRuneQueue.Count == 0) return;

        // Fetch active equation
        MathEquationGenerator.EquationData currentEq = currentSpellType switch
        {
            SpellType.Fire => currentFireEquation,
            SpellType.Water => waterRuneQueue.Peek(),
            SpellType.Lightning => lightningRuneQueue.Peek(),
            _ => default
        };

        int selectedValue = currentEq.dialOptions[index];

        if (selectedValue == currentEq.correctAnswer)
        {
            Debug.Log($"Correct! +1 Ammo to {currentSpellType}");

            // Add Ammo to Player
            PlayerMagicNetwork localPlayer = GetLocalPlayerMagic();
            if (localPlayer != null)
            {
                localPlayer.AddSpellAmmo(currentSpellType, 1);
            }

            // Consume or trigger cooldown
            if (currentSpellType == SpellType.Fire)
            {
                fireCooldownTimer = fireCooldownTime; // Trigger Fire Cooldown
            }
            else if (currentSpellType == SpellType.Water)
            {
                waterRuneQueue.Dequeue(); // Consume 1 Water Rune
            }
            else if (currentSpellType == SpellType.Lightning)
            {
                lightningRuneQueue.Dequeue(); // Consume 1 Lightning Rune
            }

            UpdateBookUI();
        }
        else
        {
            Debug.Log($"Wrong Answer! Self-damage penalty applied.");

            PlayerHealth localHealth = GetLocalPlayerHealth();
            if (localHealth != null)
            {
                // Fixed: Request server to apply backfire damage via ServerRpc
                localHealth.ApplyBackfireServerRpc(wrongAnswerDamage);
            }

            // Consume rune on wrong answer or re-roll Fire problem
            if (currentSpellType == SpellType.Fire)
            {
                currentFireEquation = equationGenerator.GenerateProblem(SpellType.Fire);
            }
            else if (currentSpellType == SpellType.Water && waterRuneQueue.Count > 0)
            {
                waterRuneQueue.Dequeue(); // Burn rune on mistake
            }
            else if (currentSpellType == SpellType.Lightning && lightningRuneQueue.Count > 0)
            {
                lightningRuneQueue.Dequeue(); // Burn rune on mistake
            }

            UpdateBookUI();
        }
    }

    private void OnAttackButtonPressed()
    {
        PlayerMagicNetwork localPlayer = GetLocalPlayerMagic();
        if (localPlayer != null)
        {
            localPlayer.CastSpell(currentSpellType);
            UpdateDialState();
        }
    }

    private void UpdateDialState()
    {
        bool hasRuneOrActive = currentSpellType switch
        {
            SpellType.Fire => fireCooldownTimer <= 0,
            SpellType.Water => waterRuneQueue.Count > 0,
            SpellType.Lightning => lightningRuneQueue.Count > 0,
            _ => false
        };

        // Enable or disable dial buttons
        for (int i = 0; i < quadrantButtons.Count; i++)
        {
            if (quadrantButtons[i] != null)
            {
                quadrantButtons[i].interactable = hasRuneOrActive;
            }
        }

        // Enable attack button if local player has ammo
        PlayerMagicNetwork localPlayer = GetLocalPlayerMagic();
        bool hasAmmo = localPlayer != null && localPlayer.HasAmmo(currentSpellType);

        if (attackButton != null)
        {
            attackButton.interactable = hasAmmo;
        }

        // Radial fill overlay for Fire cooldown
        if (dialCooldownFillOverlay != null)
        {
            bool isFireCooldown = currentSpellType == SpellType.Fire && fireCooldownTimer > 0;
            dialCooldownFillOverlay.enabled = isFireCooldown;
            dialCooldownFillOverlay.fillAmount = isFireCooldown ? (fireCooldownTimer / fireCooldownTime) : 0f;
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
                dots[i].color = Color.white;
            }
            else
            {
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