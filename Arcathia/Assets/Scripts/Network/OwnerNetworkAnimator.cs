using System.Text;
using Unity.Netcode.Components;
using UnityEngine;

[DisallowMultipleComponent]
public class OwnerNetworkAnimator : NetworkAnimator
{
    protected override void Awake()
    {
        ResolveAnimator();

        base.Awake();
    }

    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }

    private void ResolveAnimator()
    {
        
        if (!IsUsable(Animator))
        {
            Animator replacement = FindUsableAnimator();
            if (replacement != null)
            {
                Debug.LogWarning(
                    $"[OwnerNetworkAnimator] '{name}': NetworkAnimator.Animator was {Describe(Animator)}; " +
                    $"switching to {Describe(replacement)}. Fix the reference on the prefab to silence this.", this);

                
                Animator = replacement;
            }
            else
            {
                ReportProblem();
            }
        }

        
        if (Animator != null && Animator.runtimeAnimatorController != null)
        {
            Animator.Rebind();
            Animator.Update(0f);
        }
    }

    private static bool IsUsable(Animator a)
    {
        return a != null
               && a.gameObject.scene.IsValid()          
               && a.gameObject.activeInHierarchy        
               && a.runtimeAnimatorController != null;
    }

    private Animator FindUsableAnimator()
    {
        foreach (Animator a in GetComponentsInChildren<Animator>(true))
        {
            if (IsUsable(a)) return a;
        }
        return null;
    }

    private static string Describe(Animator a)
    {
        if (a == null) return "null";

        string controller = a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "NONE";
        return $"'{a.gameObject.name}' (inScene={a.gameObject.scene.IsValid()}, " +
               $"activeInHierarchy={a.gameObject.activeInHierarchy}, enabled={a.enabled}, controller={controller})";
    }

    private void ReportProblem()
    {
        var sb = new StringBuilder();
        sb.Append($"[OwnerNetworkAnimator] No usable Animator found for '{name}'. ");
        sb.Append($"NetworkAnimator.Animator = {Describe(Animator)}. Animators in hierarchy: ");

        Animator[] all = GetComponentsInChildren<Animator>(true);
        if (all.Length == 0) sb.Append("none");
        foreach (Animator a in all) sb.Append("\n  - ").Append(Describe(a));

        Debug.LogError(sb.ToString(), this);
    }
}