using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class StageMusicManager : MonoBehaviour
{
    public static StageMusicManager Instance { get; private set; }

    [Header("Stage 1 Background Music")]
    [Tooltip("Drag your Stage 1 music clip (.mp3, .wav, .ogg) here.")]
    public AudioClip stageMusicClip;

    [Range(0f, 1f)]
    public float defaultVolume = 0.5f;

    [Tooltip("If TRUE, reads and saves the same music volume used by MenuMusicPlayer.")]
    public bool syncWithMenuVolume = true;

    [Header("Fade & Playback Settings")]
    [Tooltip("Smoothly fades the music in when Stage 1 starts (0 = instant).")]
    public float fadeInDuration = 1.5f;

    [Tooltip("Keep playing music even if the game is paused (Time.timeScale = 0).")]
    public bool ignoreListenerPause = true;

    [Tooltip("Automatically fade out the music when MatchManager ends the match.")]
    public bool fadeOutOnMatchOver = true;
    public float fadeOutDuration = 1.5f;

    private AudioSource audioSource;
    private float targetVolume;
    private bool matchEndedHandled = false;
    private Coroutine fadeRoutine;

    private const string VolumePrefKey = "MenuMusicVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        audioSource = GetComponent<AudioSource>();

        if (stageMusicClip != null)
        {
            audioSource.clip = stageMusicClip;
        }

        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // Pure 2D background music
        audioSource.ignoreListenerPause = ignoreListenerPause;

        targetVolume = syncWithMenuVolume
            ? PlayerPrefs.GetFloat(VolumePrefKey, defaultVolume)
            : defaultVolume;
    }

    private void Start()
    {
        PlayStageMusic();
    }

    private void Update()
    {
        if (fadeOutOnMatchOver && !matchEndedHandled)
        {
            if (MatchManager.Instance != null && MatchManager.Instance.isMatchOver.Value)
            {
                matchEndedHandled = true;
                StopStageMusic(fadeOutDuration);
            }
        }
    }

    public void PlayStageMusic()
    {
        if (audioSource == null || audioSource.clip == null)
        {
            Debug.LogWarning("[StageMusicManager] No AudioClip assigned for Stage 1!");
            return;
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);

        if (fadeInDuration > 0.01f)
        {
            audioSource.volume = 0f;
            audioSource.Play();
            fadeRoutine = StartCoroutine(FadeVolumeRoutine(0f, targetVolume, fadeInDuration, false));
        }
        else
        {
            audioSource.volume = targetVolume;
            audioSource.Play();
        }
    }

    public void StopStageMusic(float fadeDuration = 0f)
    {
        if (audioSource == null || !audioSource.isPlaying) return;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);

        if (fadeDuration > 0.01f)
        {
            fadeRoutine = StartCoroutine(FadeVolumeRoutine(audioSource.volume, 0f, fadeDuration, true));
        }
        else
        {
            audioSource.Stop();
        }
    }

    /// <summary>
    /// Optional: Swap to a different track (e.g., intense boss/lava music) mid-stage.
    /// </summary>
    public void ChangeMusic(AudioClip newClip)
    {
        if (audioSource == null || newClip == null || audioSource.clip == newClip) return;

        audioSource.Stop();
        audioSource.clip = newClip;
        PlayStageMusic();
    }

    public void SetVolume(float volume)
    {
        targetVolume = Mathf.Clamp01(volume);

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (audioSource != null)
        {
            audioSource.volume = targetVolume;
        }

        if (syncWithMenuVolume)
        {
            PlayerPrefs.SetFloat(VolumePrefKey, targetVolume);
            PlayerPrefs.Save();
        }
    }

    private IEnumerator FadeVolumeRoutine(float from, float to, float duration, bool stopAtEnd)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (audioSource != null)
            {
                audioSource.volume = Mathf.Lerp(from, to, t);
            }
            yield return null;
        }

        if (audioSource != null)
        {
            audioSource.volume = to;
            if (stopAtEnd)
            {
                audioSource.Stop();
            }
        }

        fadeRoutine = null;
    }
}