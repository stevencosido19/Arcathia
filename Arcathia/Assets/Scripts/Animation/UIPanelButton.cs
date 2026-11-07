using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIPanelButton : MonoBehaviour
{
    public enum ButtonAction
    {
        Open,
        Close,
        Toggle
    }

    [Header("Target Panel")]
    [Tooltip("The panel you want this button to Open, Close, or Toggle.")]
    public GameObject targetPanel;

    [Header("Action")]
    public ButtonAction action = ButtonAction.Open;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ExecuteAction);
    }

    public void ExecuteAction()
    {
        if (targetPanel == null) return;

        UIPanelPopUp popUp = targetPanel.GetComponent<UIPanelPopUp>();

        switch (action)
        {
            case ButtonAction.Open:
                // Activating automatically triggers UIPanelPopUp entrance!
                targetPanel.SetActive(true);
                break;

            case ButtonAction.Close:
                // Smoothly shrinks and deactivates if it has UIPanelPopUp
                if (popUp != null && targetPanel.activeInHierarchy)
                {
                    popUp.ClosePanel();
                }
                else
                {
                    targetPanel.SetActive(false);
                }
                break;

            case ButtonAction.Toggle:
                if (targetPanel.activeInHierarchy)
                {
                    if (popUp != null) popUp.ClosePanel();
                    else targetPanel.SetActive(false);
                }
                else
                {
                    targetPanel.SetActive(true);
                }
                break;
        }
    }
}