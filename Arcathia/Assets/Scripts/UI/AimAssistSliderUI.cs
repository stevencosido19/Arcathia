using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class AimAssistSliderUI : MonoBehaviour
{
    private Slider slider;
    private const string PREFS_KEY = "Arcathia_AimAssistStrength";

    private void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        // Load saved value from PlayerPrefs (default 0.5)
        slider.value = PlayerPrefs.GetFloat(PREFS_KEY, 0.5f);

        // Listen for slider movement
        slider.onValueChanged.AddListener(OnSliderValueChanged);
    }

    private void OnSliderValueChanged(float value)
    {
        // 1. Save locally so it carries into matches and across game restarts
        PlayerPrefs.SetFloat(PREFS_KEY, value);
        PlayerPrefs.Save();

        // 2. If the local player camera is currently active in the scene, update it live
        if (ThirdPersonCamera.LocalCamera != null)
        {
            ThirdPersonCamera.LocalCamera.assistStrength = value;
        }
    }
}