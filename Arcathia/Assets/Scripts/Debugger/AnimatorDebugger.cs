using UnityEngine;

public class AnimatorDebugger : MonoBehaviour
{
    private void Awake()
    {
        Animator[] animators = GetComponentsInChildren<Animator>(true);
        Debug.Log($"<color=cyan>[AnimatorDebug] Found {animators.Length} Animator(s) on {gameObject.name}:</color>");

        foreach (var anim in animators)
        {
            Debug.Log($"• Object: <b>{anim.gameObject.name}</b> | Active: {anim.gameObject.activeInHierarchy} | Enabled: {anim.enabled} | Controller: {(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NONE")}");
        }
    }
}