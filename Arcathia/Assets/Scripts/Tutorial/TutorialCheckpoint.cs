using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TutorialCheckpoint : MonoBehaviour
{
    public enum CheckpointType
    {
        WalkArea,
        JumpArea
    }

    [Header("Checkpoint Settings")]
    [Tooltip("Select which tutorial step this trigger completes.")]
    public CheckpointType checkpointType = CheckpointType.WalkArea;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        // Check if the object that stepped in is the player (or a child collider of the player)
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
        {
            hasTriggered = true;

            if (TutorialManager.Instance == null) return;

            if (checkpointType == CheckpointType.WalkArea)
            {
                TutorialManager.Instance.CompleteWalkingStep();
            }
            else if (checkpointType == CheckpointType.JumpArea)
            {
                TutorialManager.Instance.CompleteJumpingStep();
            }
        }
    }

    public void ResetCheckpoint()
    {
        hasTriggered = false;
    }
}