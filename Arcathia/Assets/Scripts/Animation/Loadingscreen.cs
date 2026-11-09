using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    [Header("Boot Curtain")]
    [Tooltip("If true, starts 100% covered (black) on game launch to hide the empty Bootstrap scene.")]
    [SerializeField] private bool startCoveredOnBoot = true;

    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TMP_Text statusText;

    [Header("Timing")]
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private float minimumShowTime = 0.8f;
    [SerializeField] private float settleTime = 0.2f;
    [SerializeField] private float readyTimeout = 10f;
    [SerializeField] private float networkLoadTimeout = 30f;

    // ---- NEW: smooth bar ------------------------------------------------
    [Header("Progress Smoothing")]
    [Tooltip("Higher = softer ease in/out of the bar")]
    [SerializeField, Range(0.01f, 2f)] private float progressSmoothTime = 0.25f;
    [Tooltip("Slowest time the bar may take to go 0% -> 100%. This is what makes an instant 0->100 jump still slide. Lower = faster loads.")]
    [SerializeField] private float minFullFillDuration = 1.2f;
    // ---------------------------------------------------------------------

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private static bool hasBootedOnce = false;

    private bool offlineLoadRunning;
    private Coroutine networkRoutine;
    private string pendingNetworkScene;
    private bool networkLoadEventCompleted;
    private NetworkManager hookedManager;
    private NetworkSceneManager subscribedSceneManager;
    private int lastPercent = -1;

    // NEW: target = where loading really is, shown = what the bar displays
    private float targetProgress;
    private float shownProgress;
    private float progressVelocity;

    private readonly List<System.Func<bool>> readyConditions = new List<System.Func<bool>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Log("Duplicate loading screen destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (transform.parent != null)
        {
            Debug.LogWarning("[LoadingScreen] Must be at the scene root for DontDestroyOnLoad to work!");
        }
        DontDestroyOnLoad(gameObject);

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            enabled = false;
            return;
        }

        // -------------------------------------------------------------
        // CRITICAL FIX: Start 100% covered on first boot so no lag/empty scene is visible!
        // -------------------------------------------------------------
        if (startCoveredOnBoot && !hasBootedOnce)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            hasBootedOnce = true;
        }
        else
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        ResetProgress();
        Log("Ready.");
    }

    private void Update()
    {
        if (Instance != this) return;

        NetworkManager nm = NetworkManager.Singleton;
        if (nm != hookedManager)
        {
            UnhookManager();
            if (nm != null) HookManager(nm);
        }

        EnsureSceneManagerSubscription();
        UpdateSmoothProgress(); // NEW
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        UnhookManager();
        Unsubscribe();
        Instance = null;
    }

    public static void Load(string sceneName)
    {
        if (Instance != null)
        {
            Instance.LoadScene(sceneName);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    public void LoadScene(string sceneName)
    {
        if (offlineLoadRunning) return;

        StartCoroutine(OfflineLoadRoutine(sceneName));
    }

    public void HoldUntil(System.Func<bool> condition)
    {
        if (condition != null) readyConditions.Add(condition);
    }

    private IEnumerator OfflineLoadRoutine(string sceneName)
    {
        offlineLoadRunning = true;
        readyConditions.Clear();
        ResetProgress(); // CHANGED: was SetProgress(0f)

        // If not already covered, fade in first
        if (canvasGroup.alpha < 0.99f)
        {
            yield return Fade(1f, fadeDuration);
        }

        float shownAt = Time.unscaledTime;

        // Load asynchronously in background (prevents main thread stutter)
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            yield return Fade(0f, fadeDuration);
            offlineLoadRunning = false;
            yield break;
        }
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            SetProgress(op.progress / 0.9f);
            yield return null;
        }
        SetProgress(1f);

        // NEW: let the bar visibly slide to 100% before the scene switches
        yield return WaitForBarFull();

        while (Time.unscaledTime - shownAt < minimumShowTime) yield return null;

        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return null;

        yield return WaitForReadyConditions();
        yield return Resources.UnloadUnusedAssets();
        yield return new WaitForSecondsRealtime(settleTime);

        // Smoothly reveal the loaded scene!
        yield return Fade(0f, fadeDuration);
        offlineLoadRunning = false;
        Log($"Finished loading '{sceneName}'.");
    }

    private void HookManager(NetworkManager nm)
    {
        hookedManager = nm;
        nm.OnServerStarted += EnsureSceneManagerSubscription;
        nm.OnClientStarted += EnsureSceneManagerSubscription;
    }

    private void UnhookManager()
    {
        if (hookedManager == null) return;

        hookedManager.OnServerStarted -= EnsureSceneManagerSubscription;
        hookedManager.OnClientStarted -= EnsureSceneManagerSubscription;
        hookedManager = null;
    }

    private void EnsureSceneManagerSubscription()
    {
        NetworkManager nm = NetworkManager.Singleton;
        NetworkSceneManager sm = nm != null ? nm.SceneManager : null;

        if (sm == subscribedSceneManager) return;

        Unsubscribe();
        if (sm != null) Subscribe(sm);
    }

    private void Subscribe(NetworkSceneManager sm)
    {
        subscribedSceneManager = sm;
        sm.OnLoad += HandleNetworkLoadStarted;
        sm.OnLoadEventCompleted += HandleNetworkLoadEventCompleted;
    }

    private void Unsubscribe()
    {
        if (subscribedSceneManager == null) return;

        subscribedSceneManager.OnLoad -= HandleNetworkLoadStarted;
        subscribedSceneManager.OnLoadEventCompleted -= HandleNetworkLoadEventCompleted;
        subscribedSceneManager = null;
    }

    private void HandleNetworkLoadStarted(ulong clientId, string sceneName, LoadSceneMode mode, AsyncOperation asyncOperation)
    {
        bool isLocal = NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId;

        if (mode != LoadSceneMode.Single) return;
        if (!isLocal) return;
        if (offlineLoadRunning) return;

        pendingNetworkScene = sceneName;
        networkLoadEventCompleted = false;
        readyConditions.Clear();

        if (networkRoutine != null) StopCoroutine(networkRoutine);
        networkRoutine = StartCoroutine(NetworkLoadRoutine(asyncOperation));
    }

    private void HandleNetworkLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName == pendingNetworkScene) networkLoadEventCompleted = true;
    }

    private IEnumerator NetworkLoadRoutine(AsyncOperation op)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        ResetProgress(); // CHANGED: was SetProgress(0f)
        float shownAt = Time.unscaledTime;

        float timeoutAt = Time.unscaledTime + networkLoadTimeout;
        while (!networkLoadEventCompleted && Time.unscaledTime < timeoutAt)
        {
            if (op != null) SetProgress(op.progress);
            yield return null;
        }
        SetProgress(1f);

        // NEW: let the bar visibly slide to 100%
        yield return WaitForBarFull();

        yield return WaitForReadyConditions();
        yield return Resources.UnloadUnusedAssets();
        yield return new WaitForSecondsRealtime(settleTime);

        float remaining = minimumShowTime - (Time.unscaledTime - shownAt);
        if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);

        yield return Fade(0f, fadeDuration);
        networkRoutine = null;
    }

    private IEnumerator WaitForReadyConditions()
    {
        float timeoutAt = Time.unscaledTime + readyTimeout;

        while (Time.unscaledTime < timeoutAt)
        {
            bool allReady = true;
            for (int i = 0; i < readyConditions.Count; i++)
            {
                if (!readyConditions[i]())
                {
                    allReady = false;
                    break;
                }
            }

            if (allReady) break;
            yield return null;
        }

        readyConditions.Clear();
    }

    private IEnumerator Fade(float target, float duration)
    {
        canvasGroup.blocksRaycasts = true;

        float start = canvasGroup.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
            yield return null;
        }

        canvasGroup.alpha = target;
        canvasGroup.blocksRaycasts = target > 0f;
    }

    // ===================== SMOOTH PROGRESS (NEW) =====================

    /// <summary>Tell the bar where loading really is (0-1). Can jump 0 -> 1, the bar still slides. Never goes backwards.</summary>
    private void SetProgress(float progress01)
    {
        targetProgress = Mathf.Max(targetProgress, Mathf.Clamp01(progress01));
    }

    /// <summary>Back to 0% instantly (start of a new load).</summary>
    private void ResetProgress()
    {
        targetProgress = 0f;
        shownProgress = 0f;
        progressVelocity = 0f;
        lastPercent = -1;
        ApplyProgressVisual();
    }

    /// <summary>Runs every frame (unscaled time) and eases the shown value toward the target.</summary>
    private void UpdateSmoothProgress()
    {
        if (shownProgress == targetProgress) return;

        float maxSpeed = 1f / Mathf.Max(0.01f, minFullFillDuration);
        shownProgress = Mathf.SmoothDamp(shownProgress, targetProgress, ref progressVelocity,
            progressSmoothTime, maxSpeed, Time.unscaledDeltaTime);

        // SmoothDamp never quite arrives, so snap when close
        if (Mathf.Abs(targetProgress - shownProgress) < 0.002f)
        {
            shownProgress = targetProgress;
            progressVelocity = 0f;
        }

        ApplyProgressVisual();
    }

    /// <summary>Waits until the bar has visually reached 100% (with a safety timeout).</summary>
    private IEnumerator WaitForBarFull()
    {
        float timeoutAt = Time.unscaledTime + minFullFillDuration + progressSmoothTime * 6f + 1f;
        while (shownProgress < 1f && Time.unscaledTime < timeoutAt) yield return null;
    }

    private void ApplyProgressVisual()
    {
        if (progressBar != null) progressBar.value = shownProgress; // smooth, every frame

        int percent = Mathf.RoundToInt(Mathf.Clamp01(shownProgress) * 100f);
        if (percent == lastPercent) return;
        lastPercent = percent;

        if (statusText != null) statusText.text = $"Loading... {percent}%";
    }

    // =================================================================

    private void Log(string message)
    {
        if (debugLogs) Debug.Log($"[LoadingScreen] {message}");
    }
}