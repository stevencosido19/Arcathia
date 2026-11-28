using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Step 1: Walking")]
    public GameObject movementAreaPanel;
    public GameObject cameraAreaPanel;
    public GameObject walkTaskSmallPanel;
    public GameObject walkTargetArea;

    [Header("Step 2: Jumping")]
    public GameObject jumpingPanel;
    public GameObject jumpTaskSmallPanel;
    public GameObject jumpTargetArea;

    [Header("Step 3: Scroll Spellbook Tutorial")]
    public GameObject spellbookIntroPanel;
    public GameObject spellbookTaskSmallPanel;
    public TMP_Text spellbookProgressText;
    public SpellbookUI spellbookUI;

    [Header("Step 4: Rune Dial - Answer RIGHT")]
    public GameObject runeRightIntroPanel;
    public GameObject runeRightTaskSmallPanel;

    [Header("Step 5: Rune Dial - Answer WRONG (Consequences)")]
    public GameObject runeWrongIntroPanel;
    public GameObject runeWrongTaskSmallPanel;

    [Header("Step 6: Shoot 3 Targets")]
    [Tooltip("Frozen panel explaining how to press the Attack button to shoot magic.")]
    public GameObject shootIntroPanel;
    [Tooltip("Small banner shown while the player shoots the 3 targets.")]
    public GameObject shootTaskSmallPanel;
    [Tooltip("Optional TMP_Text showing target progress like '0/3'.")]
    public TMP_Text shootProgressText;
    [Tooltip("Drag your 3 GameObjects (with TutorialTarget script) here.")]
    public List<GameObject> shootTargets = new List<GameObject>();

    [Header("Step 7: Tutorial Complete")]
    [Tooltip("Final panel shown when all 3 targets are destroyed.")]
    public GameObject tutorialCompletePanel;
    [Tooltip("Button inside the complete panel to exit back to Main Menu.")]
    public Button completeContinueButton;
    public string mainMenuSceneName = "MainMenu";

    [Header("Tap Safety")]
    public float tapCooldown = 0.2f;

    private bool isWaitingForTap = false;
    private bool screenTapped = false;

    private bool isWaitingForWalk = false;
    private bool walkCompleted = false;

    private bool isWaitingForJump = false;
    private bool jumpCompleted = false;

    private bool isWaitingForSpellbookScroll = false;
    private bool allSpellPagesExplored = false;
    private readonly HashSet<SpellType> visitedSpellPages = new HashSet<SpellType>();
    private int totalSpellPagesCount;

    private bool isWaitingForRightAnswer = false;
    private bool rightAnswerCompleted = false;

    private bool isWaitingForWrongAnswer = false;
    private bool wrongAnswerCompleted = false;

    private bool isWaitingForTargets = false;
    private int destroyedTargetsCount = 0;
    private int totalTargetsCount = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        totalSpellPagesCount = System.Enum.GetValues(typeof(SpellType)).Length;
    }

    private void Start()
    {
        SetGameFrozen(true);

        // Hide all frozen panels
        if (movementAreaPanel != null) movementAreaPanel.SetActive(false);
        if (cameraAreaPanel != null) cameraAreaPanel.SetActive(false);
        if (jumpingPanel != null) jumpingPanel.SetActive(false);
        if (spellbookIntroPanel != null) spellbookIntroPanel.SetActive(false);
        if (runeRightIntroPanel != null) runeRightIntroPanel.SetActive(false);
        if (runeWrongIntroPanel != null) runeWrongIntroPanel.SetActive(false);
        if (shootIntroPanel != null) shootIntroPanel.SetActive(false);
        if (tutorialCompletePanel != null) tutorialCompletePanel.SetActive(false);

        // Hide all small task panels & progress texts
        if (walkTaskSmallPanel != null) walkTaskSmallPanel.SetActive(false);
        if (jumpTaskSmallPanel != null) jumpTaskSmallPanel.SetActive(false);
        if (spellbookTaskSmallPanel != null) spellbookTaskSmallPanel.SetActive(false);
        if (spellbookProgressText != null) spellbookProgressText.gameObject.SetActive(false);
        if (runeRightTaskSmallPanel != null) runeRightTaskSmallPanel.SetActive(false);
        if (runeWrongTaskSmallPanel != null) runeWrongTaskSmallPanel.SetActive(false);
        if (shootTaskSmallPanel != null) shootTaskSmallPanel.SetActive(false);
        if (shootProgressText != null) shootProgressText.gameObject.SetActive(false);

        // Hide all 3D checkpoints & targets initially
        if (walkTargetArea != null) walkTargetArea.SetActive(false);
        if (jumpTargetArea != null) jumpTargetArea.SetActive(false);

        foreach (GameObject target in shootTargets)
        {
            if (target != null) target.SetActive(false);
        }

        if (completeContinueButton != null)
        {
            completeContinueButton.onClick.RemoveAllListeners();
            completeContinueButton.onClick.AddListener(ReturnToMainMenu);
        }

        StartCoroutine(RunTutorialRoutine());
    }

    private void Update()
    {
        if (!isWaitingForTap) return;

        bool touchReleased = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
        bool mouseReleased = Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;

        if (touchReleased || mouseReleased)
        {
            screenTapped = true;
        }
    }

    private IEnumerator RunTutorialRoutine()
    {
        yield return null;

        // STEP 1: WALKING
        yield return StartCoroutine(ShowPanelAndWaitForClick(movementAreaPanel));
        yield return StartCoroutine(ShowPanelAndWaitForClick(cameraAreaPanel));

        SetGameFrozen(false);
        walkCompleted = false;
        isWaitingForWalk = true;

        if (walkTaskSmallPanel != null) walkTaskSmallPanel.SetActive(true);
        if (walkTargetArea != null) walkTargetArea.SetActive(true);

        yield return new WaitUntil(() => walkCompleted);

        isWaitingForWalk = false;
        if (walkTaskSmallPanel != null) walkTaskSmallPanel.SetActive(false);
        if (walkTargetArea != null) walkTargetArea.SetActive(false);

        // STEP 2: JUMPING
        yield return StartCoroutine(ShowPanelAndWaitForClick(jumpingPanel));

        SetGameFrozen(false);
        jumpCompleted = false;
        isWaitingForJump = true;

        if (jumpTaskSmallPanel != null) jumpTaskSmallPanel.SetActive(true);
        if (jumpTargetArea != null) jumpTargetArea.SetActive(true);

        yield return new WaitUntil(() => jumpCompleted);

        isWaitingForJump = false;
        if (jumpTaskSmallPanel != null) jumpTaskSmallPanel.SetActive(false);
        if (jumpTargetArea != null) jumpTargetArea.SetActive(false);

        // STEP 3: EXPLORE ALL SPELLBOOK PAGES
        yield return StartCoroutine(RunSpellbookScrollStep());

        // STEP 4: RUNE DIAL - ANSWER RIGHT
        yield return StartCoroutine(RunRightAnswerStep());

        // STEP 5: RUNE DIAL - ANSWER WRONG
        yield return StartCoroutine(RunWrongAnswerStep());

        // STEP 6: SHOOT 3 TARGETS
        yield return StartCoroutine(RunShootTargetsStep());

        // STEP 7: TUTORIAL COMPLETE!
        SetGameFrozen(true);
        if (tutorialCompletePanel != null)
        {
            tutorialCompletePanel.SetActive(true);
        }
        else
        {
            Debug.Log("<color=green>[Tutorial] Tutorial Complete!</color>");
            SetGameFrozen(false);
        }
    }

    private IEnumerator RunSpellbookScrollStep()
    {
        if (spellbookIntroPanel != null)
        {
            yield return StartCoroutine(ShowPanelAndWaitForClick(spellbookIntroPanel));
        }

        SetGameFrozen(false);

        if (spellbookUI == null)
            spellbookUI = FindFirstObjectByType<SpellbookUI>();

        visitedSpellPages.Clear();

        if (spellbookUI != null)
        {
            spellbookUI.AddRuneToInventory(SpellType.Water);
            spellbookUI.AddRuneToInventory(SpellType.Lightning);
            spellbookUI.SetTutorialMode(SpellbookUI.TutorialInputMode.ScrollOnly);

            visitedSpellPages.Add(spellbookUI.GetCurrentSpellType());
        }

        UpdateSpellbookProgressText();

        allSpellPagesExplored = false;
        isWaitingForSpellbookScroll = true;

        if (spellbookTaskSmallPanel != null) spellbookTaskSmallPanel.SetActive(true);
        if (spellbookProgressText != null) spellbookProgressText.gameObject.SetActive(true);

        yield return new WaitUntil(() => allSpellPagesExplored);

        isWaitingForSpellbookScroll = false;
        yield return new WaitForSecondsRealtime(0.6f);

        if (spellbookTaskSmallPanel != null) spellbookTaskSmallPanel.SetActive(false);
        if (spellbookProgressText != null) spellbookProgressText.gameObject.SetActive(false);
    }

    private IEnumerator RunRightAnswerStep()
    {
        if (runeRightIntroPanel != null)
        {
            yield return StartCoroutine(ShowPanelAndWaitForClick(runeRightIntroPanel));
        }

        SetGameFrozen(false);

        if (spellbookUI == null)
            spellbookUI = FindFirstObjectByType<SpellbookUI>();

        if (spellbookUI != null)
        {
            spellbookUI.PrepareTutorialEquation();
            spellbookUI.SetTutorialMode(SpellbookUI.TutorialInputMode.RequireRightAnswer);
        }

        rightAnswerCompleted = false;
        isWaitingForRightAnswer = true;

        if (runeRightTaskSmallPanel != null) runeRightTaskSmallPanel.SetActive(true);

        yield return new WaitUntil(() => rightAnswerCompleted);

        isWaitingForRightAnswer = false;
        yield return new WaitForSecondsRealtime(0.7f);

        if (runeRightTaskSmallPanel != null) runeRightTaskSmallPanel.SetActive(false);
    }

    private IEnumerator RunWrongAnswerStep()
    {
        if (runeWrongIntroPanel != null)
        {
            yield return StartCoroutine(ShowPanelAndWaitForClick(runeWrongIntroPanel));
        }

        SetGameFrozen(false);

        if (spellbookUI == null)
            spellbookUI = FindFirstObjectByType<SpellbookUI>();

        if (spellbookUI != null)
        {
            spellbookUI.PrepareTutorialEquation();
            spellbookUI.SetTutorialMode(SpellbookUI.TutorialInputMode.RequireWrongAnswer);
        }

        wrongAnswerCompleted = false;
        isWaitingForWrongAnswer = true;

        if (runeWrongTaskSmallPanel != null) runeWrongTaskSmallPanel.SetActive(true);

        yield return new WaitUntil(() => wrongAnswerCompleted);

        isWaitingForWrongAnswer = false;
        yield return new WaitForSecondsRealtime(0.9f);

        if (runeWrongTaskSmallPanel != null) runeWrongTaskSmallPanel.SetActive(false);
    }

    private IEnumerator RunShootTargetsStep()
    {
        if (shootIntroPanel != null)
        {
            yield return StartCoroutine(ShowPanelAndWaitForClick(shootIntroPanel));
        }

        SetGameFrozen(false);

        if (spellbookUI == null)
            spellbookUI = FindFirstObjectByType<SpellbookUI>();

        // Unlock full Spellbook controls so the player can shoot AND solve more equations if they miss
        if (spellbookUI != null)
        {
            spellbookUI.SetTutorialMode(SpellbookUI.TutorialInputMode.Normal);
            spellbookUI.PrepareTutorialEquation();
        }

        // Give the player 3 starting shots on their currently selected spell so they can shoot right away
        GrantStarterAmmoForShooting(3);

        destroyedTargetsCount = 0;
        totalTargetsCount = 0;

        foreach (GameObject target in shootTargets)
        {
            if (target != null)
            {
                target.SetActive(true);
                totalTargetsCount++;
            }
        }

        // Safety fallback if no targets were assigned in the Inspector
        if (totalTargetsCount == 0)
        {
            Debug.LogWarning("[TutorialManager] No shootTargets assigned in Inspector! Skipping Step 6.");
            yield break;
        }

        UpdateShootProgressText();

        isWaitingForTargets = true;
        if (shootTaskSmallPanel != null) shootTaskSmallPanel.SetActive(true);
        if (shootProgressText != null) shootProgressText.gameObject.SetActive(true);

        // Wait until all 3 targets are destroyed
        yield return new WaitUntil(() => destroyedTargetsCount >= totalTargetsCount);

        isWaitingForTargets = false;
        yield return new WaitForSecondsRealtime(0.6f);

        if (shootTaskSmallPanel != null) shootTaskSmallPanel.SetActive(false);
        if (shootProgressText != null) shootProgressText.gameObject.SetActive(false);
    }

    private void GrantStarterAmmoForShooting(int amount)
    {
        SpellType activeSpell = (spellbookUI != null) ? spellbookUI.GetCurrentSpellType() : SpellType.Fire;

        PlayerMagicNetwork[] players = FindObjectsByType<PlayerMagicNetwork>(FindObjectsSortMode.None);
        foreach (PlayerMagicNetwork p in players)
        {
            if (p.IsOwner)
            {
                p.AddSpellAmmo(activeSpell, amount);
                break;
            }
        }
    }

    private void UpdateSpellbookProgressText()
    {
        if (spellbookProgressText != null)
        {
            spellbookProgressText.text = $"Swipe Spellbook to explore all spells ({visitedSpellPages.Count}/{totalSpellPagesCount})";
        }
    }

    private void UpdateShootProgressText()
    {
        if (shootProgressText != null)
        {
            shootProgressText.text = $"Shoot all targets ({destroyedTargetsCount}/{totalTargetsCount})";
        }
    }

    private IEnumerator ShowPanelAndWaitForClick(GameObject panel)
    {
        if (panel == null) yield break;

        SetGameFrozen(true);
        panel.SetActive(true);

        yield return new WaitForSecondsRealtime(tapCooldown);

        screenTapped = false;
        isWaitingForTap = true;

        yield return new WaitUntil(() => screenTapped);

        isWaitingForTap = false;
        panel.SetActive(false);
    }

    private void SetGameFrozen(bool freeze)
    {
        Time.timeScale = freeze ? 0f : 1f;
    }

    public void CompleteWalkingStep()
    {
        if (isWaitingForWalk) walkCompleted = true;
    }

    public void CompleteJumpingStep()
    {
        if (isWaitingForJump) jumpCompleted = true;
    }

    public void NotifySpellbookScrolled(SpellType pageType)
    {
        if (!isWaitingForSpellbookScroll) return;

        visitedSpellPages.Add(pageType);
        UpdateSpellbookProgressText();

        if (visitedSpellPages.Count >= totalSpellPagesCount)
        {
            allSpellPagesExplored = true;
        }
    }

    public void NotifyRightAnswerSelected()
    {
        if (isWaitingForRightAnswer) rightAnswerCompleted = true;
    }

    public void NotifyWrongAnswerSelected()
    {
        if (isWaitingForWrongAnswer) wrongAnswerCompleted = true;
    }

    public void NotifyTargetDestroyed()
    {
        if (!isWaitingForTargets) return;

        destroyedTargetsCount++;
        UpdateShootProgressText();
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            NetworkManager.Singleton.Shutdown();
        }

        LoadingScreen.Load(mainMenuSceneName);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}