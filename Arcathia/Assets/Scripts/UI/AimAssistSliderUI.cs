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

        
        slider.value = PlayerPrefs.GetFloat(PREFS_KEY, 0.5f);

        
        slider.onValueChanged.AddListener(OnSliderValueChanged);
    }

    private void OnSliderValueChanged(float value)
    {
        
        PlayerPrefs.SetFloat(PREFS_KEY, value);
        PlayerPrefs.Save();

        
        if (ThirdPersonCamera.LocalCamera != null)
        {
            ThirdPersonCamera.LocalCamera.assistStrength = value;
        }
    }
}