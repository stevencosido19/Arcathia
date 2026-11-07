using System.Text;
using Unity.Netcode.Components;
using UnityEngine;

[DisallowMultipleComponent]
public class OwnerNetworkAnimator : NetworkAnimator
{
    protected override void Awake()
    {
        ResolveAnimator();

        // Netcode reads layerCount / parameters from Animator in here, so the Animator
        // must already be a live, active one with a controller.
        base.Awake();
    }

    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }

    private void ResolveAnimator()
    {
        // NetworkAnimator.Animator must point at an Animator that lives on THIS spawned instance,
        // is active, and has a controller. Otherwise base.Awake() logs
        // "Animator is not playing an AnimatorController". Typical culprits:
        //  - the field points at an Animator on an inactive child (e.g. a hidden role model)
        //  - the field points at an Animator inside a prefab ASSET instead of the spawned clone
        //  - the referenced Animator has no controller assigned
        if (!IsUsable(Animator))
        {
            Animator replacement = FindUsableAnimator();
            if (replacement != null)
            {
                Debug.LogWarning(
                    $"[OwnerNetworkAnimator] '{name}': NetworkAnimator.Animator was {Describe(Animator)}; " +
                    $"switching to {Describe(replacement)}. Fix the reference on the prefab to silence this.", this);

                // The old code found an Animator but never assigned it back, so Netcode kept using the bad one.
                Animator = replacement;
            }
            else
            {
                ReportProblem();
            }
        }

        // Make sure the Animator has built its graph before Netcode queries it.
        if (Animator != null && Animator.runtimeAnimatorController != null)
        {
            Animator.Rebind();
            Animator.Update(0f);
        }
    }

    private static bool IsUsable(Animator a)
    {
        return a != null
               && a.gameObject.scene.IsValid()          // false for references into prefab assets
               && a.gameObject.activeInHierarchy        // false for animators on inactive children
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