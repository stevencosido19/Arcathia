using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SpellbookUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public enum BookMode { Spells, PowerUps }

    [Header("Mode & Bookmark Toggle")]
    public BookMode currentBookMode = BookMode.Spells;
    public Button modeBookmarkButton;
    public TMP_Text modeBookmarkText;

    [Header("Spellbook Shared Text Elements")]
    public TMP_Text spellTypeTitleText;
    public TMP_Text equationDisplayText;

    [Header("Spell Dial Elements")]
    public List<TMP_Text> quadrantTexts;
    public List<Button> quadrantButtons;

    [Header("Ammo HUD Elements")]
    public List<Image> fireAmmoDots;
    public List<Image> waterAmmoDots;
    public List<Image> lightningAmmoDots;
    public Button attackButton;

    [Header("Double-Click / Double-Tap Settings")]
    public float doubleTapThreshold = 0.3f;
    private float lastTapTime = 0f;

    [Header("Swipe Detection Settings")]
    public float swipeThreshold = 50f;
    private Vector2 touchStartPos;
    private bool swipeHandled = false;

    [Header("Settings & Timers")]
    public float fireCooldownTime = 10.0f;
    public int wrongAnswerDamage = 10;
    public Image dialCooldownFillOverlay;
    private float fireCooldownTimer = 0f;

    [Header("Rune Inventory Queues")]
    private readonly Queue<MathEquationGenerator.EquationData> waterRuneQueue = new Queue<MathEquationGenerator.EquationData>();
    private readonly Queue<MathEquationGenerator.EquationData> lightningRuneQueue = new Queue<MathEquationGenerator.EquationData>();
    private MathEquationGenerator.EquationData currentFireEquation;

    [Header("Dependencies")]
    public MathEquationGenerator equationGenerator;

    // Cached Local Player Component References
    private PlayerPowerUpHandler localPowerUpHandler;
    private PlayerMagicNetwork localMagicNetwork;
    private PlayerHealth localPlayerHealth;

    private SpellType currentSpellType = SpellType.Fire;

    // Track state of Rune Lens exclusions per problem
    private readonly HashSet<int> disabledQuadrantIndices = new HashSet<int>();

    void Start()
    {
        if (equationGenerator == null)
            equationGenerator = FindFirstObjectByType<MathEquationGenerator>();

        FetchNewFireProblem();

        for (int i = 0; i < quadrantButtons.Count; i++)
        {
            int index = i;
            if (quadrantButtons[i] != null)
            {
                quadrantButtons[i].onClick.RemoveAllListeners();
                quadrantButtons[i].onClick.AddListener(() => OnQuadrantSelected(index));
            }
        }

        if (attackButton != null)
        {
            attackButton.onClick.RemoveAllListeners();
            attackButton.onClick.AddListener(OnAttackButtonPressed);
        }

        if (modeBookmarkButton != null)
        {
            modeBookmarkButton.onClick.RemoveAllListeners();
            modeBookmarkButton.onClick.AddListener(ToggleBookMode);
        }

        UpdateBookUI();
    }

    void Update()
    {
        HandleFireCooldown();
    }

    public void BindPowerUpHandler(PlayerPowerUpHandler handler)
    {
        localPowerUpHandler = handler;
        if (localPowerUpHandler != null)
        {
            localPowerUpHandler.SetSpellbookUI(this);
        }
        UpdateBookUI();
    }

    public void ToggleBookMode()
    {
        currentBookMode = (currentBookMode == BookMode.Spells) ? BookMode.PowerUps : BookMode.Spells;
        disabledQuadrantIndices.Clear();
        UpdateBookUI();
    }

    public void AddRuneToInventory(SpellType type)
    {
        if (equationGenerator == null) return;

        MathEquationGenerator.EquationData newEq = equationGenerator.GenerateProblem(type);
        if (newEq == null) return;

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
            if (dots[i] == null) continue;

            dots[i].enabled = true;
            dots[i].color = (i < currentCount) ? Color.white : new Color(1f, 1f, 1f, 0.2f);
        }

        UpdateDialState();
    }

    public void UpdateBookUI()
    {
        bool isSpellsPage = (currentBookMode == BookMode.Spells);

        foreach (Button b in quadrantButtons)
        {
            if (b != null) b.gameObject.SetActive(isSpellsPage);
        }

        foreach (TMP_Text t in quadrantTexts)
        {
            if (t != null) t.gameObject.SetActive(isSpellsPage);
        }

        if (modeBookmarkText != null)
            modeBookmarkText.text = isSpellsPage ? "POWER-UPS" : "SPELLS";

        if (isSpellsPage)
        {
            UpdateSpellsTextUI();
        }
        else
        {
            UpdatePowerUpsTextUI();
        }
    }

    private void UpdateSpellsTextUI()
    {
        if (spellTypeTitleText != null)
            spellTypeTitleText.text = $"{currentSpellType.ToString().ToUpper()} SPELL";

        if (currentSpellType == SpellType.Fire)
        {
            if (fireCooldownTimer > 0)
                SetDialText("RECHARGING DIAL...", "", "", "", "");
            else if (currentFireEquation != null)
                DisplayEquation(currentFireEquation);
        }
        else if (currentSpellType == SpellType.Water)
        {
            if (waterRuneQueue.Count > 0)
                DisplayEquation(waterRuneQueue.Peek());
            else
                SetDialText($"NO WATER RUNES ({waterRuneQueue.Count})", "-", "-", "-", "-");
        }
        else if (currentSpellType == SpellType.Lightning)
        {
            if (lightningRuneQueue.Count > 0)
                DisplayEquation(lightningRuneQueue.Peek());
            else
                SetDialText($"NO LIGHTNING RUNES ({lightningRuneQueue.Count})", "-", "-", "-", "-");
        }

        UpdateDialState();
    }

    private void UpdatePowerUpsTextUI()
    {
        PlayerPowerUpHandler handler = GetLocalPlayerPowerUp();

        if (spellTypeTitleText != null)
            spellTypeTitleText.text = "STORED POWER-UPS";

        if (dialCooldownFillOverlay != null)
            dialCooldownFillOverlay.enabled = false;

        if (handler == null)
        {
            if (equationDisplayText != null) equationDisplayText.text = "No Player Found";
            return;
        }

        PowerUpType activeType = handler.GetSelectedPowerUp();
        int totalStored = handler.GetStoredPowerUpsCount();

        if (activeType == PowerUpType.None || totalStored == 0)
        {
            if (equationDisplayText != null)
                equationDisplayText.text = "No Power-Ups Stored";
        }
        else
        {
            if (equationDisplayText != null)
                equationDisplayText.text = $"<b>{FormatPowerUpName(activeType)}</b>";
        }
    }

    public void UpdatePowerUpDisplay(List<PowerUpType> storedPowerUps, int selectedIndex)
    {
        UpdateBookUI();
    }

    private string FormatPowerUpName(PowerUpType type) => type switch
    {
        PowerUpType.RuneLens => "RUNE LENS",
        PowerUpType.PrismBarrier => "PRISM BARRIER",
        PowerUpType.OverchargeMatrix => "OVERCHARGE MATRIX",
        _ => "NONE"
    };

    // --- INPUT GESTURES ---

    public void OnPointerDown(PointerEventData eventData)
    {
        touchStartPos = eventData.position;
        swipeHandled = false;

        float timeSinceLastTap = Time.time - lastTapTime;
        if (timeSinceLastTap <= doubleTapThreshold)
        {
            OnSpellbookDoubleTapped();
            lastTapTime = 0f;
        }
        else
        {
            lastTapTime = Time.time;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (swipeHandled) return;

        Vector2 dragDelta = eventData.position - touchStartPos;

        if (dragDelta.magnitude >= swipeThreshold)
        {
            if (Mathf.Abs(dragDelta.x) > Mathf.Abs(dragDelta.y))
            {
                swipeHandled = true;
                int direction = dragDelta.x > 0 ? 1 : -1;
                CyclePage(direction);
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        swipeHandled = false;
    }

    public void CyclePage(int direction)
    {
        if (currentBookMode == BookMode.Spells)
        {
            int totalTypes = System.Enum.GetValues(typeof(SpellType)).Length;
            int currentIndex = (int)currentSpellType;

            currentIndex = (currentIndex + direction + totalTypes) % totalTypes;
            currentSpellType = (SpellType)currentIndex;

            disabledQuadrantIndices.Clear();
            UpdateBookUI();
        }
        else if (currentBookMode == BookMode.PowerUps)
        {
            PlayerPowerUpHandler handler = GetLocalPlayerPowerUp();
            if (handler != null)
            {
                handler.CycleSelectedPowerUp(direction);
                UpdateBookUI();
            }
        }
    }

    private void OnSpellbookDoubleTapped()
    {
        if (currentBookMode == BookMode.PowerUps)
        {
            PlayerPowerUpHandler handler = GetLocalPlayerPowerUp();
            if (handler != null)
            {
                handler.UseSelectedPowerUp();
            }
        }
    }

    // --- RUNE LENS EFFECT ---

    public void ApplyRuneLensEffect()
    {
        MathEquationGenerator.EquationData activeEq = currentSpellType switch
        {
            SpellType.Fire => currentFireEquation,
            SpellType.Water => waterRuneQueue.Count > 0 ? waterRuneQueue.Peek() : null,
            SpellType.Lightning => lightningRuneQueue.Count > 0 ? lightningRuneQueue.Peek() : null,
            _ => null
        };

        if (activeEq == null || activeEq.dialOptions == null) return;

        List<int> wrongOptionIndices = new List<int>();
        for (int i = 0; i < activeEq.dialOptions.Count; i++)
        {
            if (activeEq.dialOptions[i] != activeEq.correctAnswer)
                wrongOptionIndices.Add(i);
        }

        for (int i = 0; i < 2 && wrongOptionIndices.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, wrongOptionIndices.Count);
            disabledQuadrantIndices.Add(wrongOptionIndices[randomIndex]);
            wrongOptionIndices.RemoveAt(randomIndex);
        }

        UpdateBookUI();
    }

    // --- STANDARD DIAL & SPELL LOGIC ---

    private void FetchNewFireProblem()
    {
        disabledQuadrantIndices.Clear();
        if (equationGenerator != null)
            currentFireEquation = equationGenerator.GenerateProblem(SpellType.Fire);
    }

    private void HandleFireCooldown()
    {
        if (fireCooldownTimer > 0)
        {
            fireCooldownTimer -= Time.deltaTime;
            if (fireCooldownTimer <= 0)
            {
                FetchNewFireProblem();
                if (currentSpellType == SpellType.Fire && currentBookMode == BookMode.Spells)
                    UpdateBookUI();
            }
        }

        if (currentBookMode == BookMode.Spells)
        {
            UpdateDialState();
        }
    }

    private void DisplayEquation(MathEquationGenerator.EquationData eq)
    {
        if (eq == null) return;
        if (equationDisplayText != null) equationDisplayText.text = eq.questionText;

        for (int i = 0; i < 4; i++)
        {
            if (i < quadrantTexts.Count && quadrantTexts[i] != null)
            {
                if (disabledQuadrantIndices.Contains(i))
                {
                    quadrantTexts[i].text = "<color=red>X</color>";
                }
                else
                {
                    quadrantTexts[i].text = (eq.dialOptions != null && i < eq.dialOptions.Count)
                        ? eq.dialOptions[i].ToString()
                        : "-";
                }
            }
        }
    }

    private void SetDialText(string question, string q1, string q2, string q3, string q4)
    {
        if (equationDisplayText != null) equationDisplayText.text = question;
        string[] opts = { q1, q2, q3, q4 };
        for (int i = 0; i < 4; i++)
        {
            if (i < quadrantTexts.Count && quadrantTexts[i] != null) quadrantTexts[i].text = opts[i];
        }
    }

    private void OnQuadrantSelected(int index)
    {
        if (disabledQuadrantIndices.Contains(index)) return;
        if (currentSpellType == SpellType.Fire && fireCooldownTimer > 0) return;
        if (currentSpellType == SpellType.Water && waterRuneQueue.Count == 0) return;
        if (currentSpellType == SpellType.Lightning && lightningRuneQueue.Count == 0) return;

        MathEquationGenerator.EquationData currentEq = currentSpellType switch
        {
            SpellType.Fire => currentFireEquation,
            SpellType.Water => waterRuneQueue.Count > 0 ? waterRuneQueue.Peek() : null,
            SpellType.Lightning => lightningRuneQueue.Count > 0 ? lightningRuneQueue.Peek() : null,
            _ => null
        };

        if (currentEq == null || currentEq.dialOptions == null || index >= currentEq.dialOptions.Count) return;

        int selectedValue = currentEq.dialOptions[index];

        if (selectedValue == currentEq.correctAnswer)
        {
            PlayerMagicNetwork localPlayer = GetLocalPlayerMagic();
            if (localPlayer != null) localPlayer.AddSpellAmmo(currentSpellType, 1);

            disabledQuadrantIndices.Clear();

            if (currentSpellType == SpellType.Fire) fireCooldownTimer = fireCooldownTime;
            else if (currentSpellType == SpellType.Water) waterRuneQueue.Dequeue();
            else if (currentSpellType == SpellType.Lightning) lightningRuneQueue.Dequeue();

            UpdateBookUI();
        }
        else
        {
            PlayerHealth localHealth = GetLocalPlayerHealth();
            if (localHealth != null) localHealth.ApplyBackfireServerRpc(wrongAnswerDamage);

            disabledQuadrantIndices.Clear();

            if (currentSpellType == SpellType.Fire) FetchNewFireProblem();
            else if (currentSpellType == SpellType.Water && waterRuneQueue.Count > 0) waterRuneQueue.Dequeue();
            else if (currentSpellType == SpellType.Lightning && lightningRuneQueue.Count > 0) lightningRuneQueue.Dequeue();

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
        if (currentBookMode != BookMode.Spells) return;

        bool hasRuneOrActive = currentSpellType switch
        {
            SpellType.Fire => fireCooldownTimer <= 0,
            SpellType.Water => waterRuneQueue.Count > 0,
            SpellType.Lightning => lightningRuneQueue.Count > 0,
            _ => false
        };

        for (int i = 0; i < quadrantButtons.Count; i++)
        {
            if (quadrantButtons[i] != null)
            {
                bool isBlockedByLens = disabledQuadrantIndices.Contains(i);
                quadrantButtons[i].interactable = hasRuneOrActive && !isBlockedByLens;
            }
        }

        PlayerMagicNetwork localPlayer = GetLocalPlayerMagic();
        bool hasAmmo = localPlayer != null && localPlayer.HasAmmo(currentSpellType);
        if (attackButton != null) attackButton.interactable = hasAmmo;

        if (dialCooldownFillOverlay != null)
        {
            bool isFireCooldown = currentSpellType == SpellType.Fire && fireCooldownTimer > 0;
            dialCooldownFillOverlay.enabled = isFireCooldown;
            dialCooldownFillOverlay.fillAmount = isFireCooldown ? (fireCooldownTimer / fireCooldownTime) : 0f;
        }
    }

    // --- CACHED LOCAL PLAYER GETTERS ---

    private PlayerMagicNetwork GetLocalPlayerMagic()
    {
        if (localMagicNetwork != null) return localMagicNetwork;

        PlayerMagicNetwork[] players = FindObjectsByType<PlayerMagicNetwork>(FindObjectsSortMode.None);
        foreach (PlayerMagicNetwork p in players)
        {
            if (p.IsOwner)
            {
                localMagicNetwork = p;
                return localMagicNetwork;
            }
        }
        return null;
    }

    private PlayerHealth GetLocalPlayerHealth()
    {
        if (localPlayerHealth != null) return localPlayerHealth;

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (PlayerHealth p in players)
        {
            if (p.IsOwner)
            {
                localPlayerHealth = p;
                return localPlayerHealth;
            }
        }
        return null;
    }

    private PlayerPowerUpHandler GetLocalPlayerPowerUp()
    {
        if (localPowerUpHandler != null) return localPowerUpHandler;

        PlayerPowerUpHandler[] players = FindObjectsByType<PlayerPowerUpHandler>(FindObjectsSortMode.None);
        foreach (PlayerPowerUpHandler p in players)
        {
            if (p.IsOwner)
            {
                localPowerUpHandler = p;
                localPowerUpHandler.SetSpellbookUI(this);
                return localPowerUpHandler;
            }
        }
        return null;
    }
}