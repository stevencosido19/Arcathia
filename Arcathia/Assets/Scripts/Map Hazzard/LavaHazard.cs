using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(Collider))]
public class LavaHazard : NetworkBehaviour
{
    private enum LavaPhase
    {
        WaitingToRise,
        Rising,
        StayingRaised,
        Lowering
    }

    [Header("Lava Rising Timer")]
    [Tooltip("Seconds between each lava rise (60 = 1 minute).")]
    public float riseInterval = 60f;

    [Tooltip("How many seconds before rising the warning text appears.")]
    public float warningLeadTime = 15f;

    [Tooltip("How many seconds the lava stays raised before returning to its original position.")]
    public float stayRaisedDuration = 15f;

    [Tooltip("How many meters the lava rises above its starting position.")]
    public float riseHeight = 2.0f;

    [Tooltip("How fast the lava moves upward (meters per second).")]
    public float riseSpeed = 0.6f;

    [Tooltip("How fast the lava goes back down to its original position (meters per second).")]
    public float lowerSpeed = 0.8f;

    [Tooltip("0 = repeats endlessly during the match. Set > 0 to limit total rises.")]
    public int maxRiseCount = 0;

    [Header("Lava Damage & Pushback (Anti-Sink)")]
    public int lavaDamage = 25;

    [Tooltip("How often a player can be bounced/damaged if they land back in lava.")]
    public float hitCooldown = 0.75f;

    [Tooltip("Upward bounce force so the player launches out of the lava and doesn't sink.")]
    public float upwardPushForce = 12f;

    [Tooltip("Horizontal pushback force.")]
    public float horizontalPushForce = 10f;

    [Tooltip("How long controls lock during the bounce back.")]
    public float stunDuration = 0.35f;

    [Tooltip("Recommended: Drag an empty GameObject in the middle of your safe arena/island so lava pushes players back toward land.")]
    public Transform safeCenterPoint;

    [Header("Warning UI")]
    [Tooltip("Drag your warning TextMeshPro text from the Canvas here.")]
    public TMP_Text lavaWarningText;

    [Tooltip("Optional: Background banner behind the warning text.")]
    public GameObject lavaWarningPanel;

    // Networked State:
    // -2 = Calm/Hidden, 1..15 = Countdown seconds, 0 = Rising right now
    private NetworkVariable<int> netWarningState = new NetworkVariable<int>(
        -2, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<float> netTargetY = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float originalY;
    private LavaPhase currentPhase = LavaPhase.WaitingToRise;
    private float phaseTimer;
    private int currentRiseCount = 0;

    private readonly Dictionary<ulong, float> playerHitCooldowns = new Dictionary<ulong, float>();

    private void Awake()
    {
        // Remember the starting Y height before any movement occurs
        originalY = transform.position.y;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        netWarningState.OnValueChanged += OnWarningStateChanged;

        if (IsServer)
        {
            netTargetY.Value = originalY;
            netWarningState.Value = -2;
            currentPhase = LavaPhase.WaitingToRise;
            phaseTimer = riseInterval;
            currentRiseCount = 0;
        }
        else
        {
            // Snap late-joining clients to the current lava height
            Vector3 pos = transform.position;
            pos.y = netTargetY.Value;
            transform.position = pos;
        }

        UpdateWarningUI(netWarningState.Value);
    }

    public override void OnNetworkDespawn()
    {
        netWarningState.OnValueChanged -= OnWarningStateChanged;
        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (MatchManager.Instance != null && MatchManager.Instance.isMatchOver.Value) return;

        // 1. Smoothly move the lava on ALL screens toward netTargetY
        Vector3 pos = transform.position;
        bool isAtTarget = Mathf.Approximately(pos.y, netTargetY.Value);

        if (!isAtTarget)
        {
            float moveSpeed = (netTargetY.Value > pos.y) ? riseSpeed : lowerSpeed;
            pos.y = Mathf.MoveTowards(pos.y, netTargetY.Value, moveSpeed * Time.deltaTime);
            transform.position = pos;
            isAtTarget = Mathf.Approximately(pos.y, netTargetY.Value);
        }

        // 2. Server manages the state timeline
        if (!IsServer) return;

        switch (currentPhase)
        {
            case LavaPhase.WaitingToRise:
                if (maxRiseCount > 0 && currentRiseCount >= maxRiseCount) return;

                phaseTimer -= Time.deltaTime;

                if (phaseTimer <= 0f)
                {
                    // Start Rising!
                    currentRiseCount++;
                    netTargetY.Value = originalY + riseHeight;
                    netWarningState.Value = 0; // "LAVA IS RISING!"
                    currentPhase = LavaPhase.Rising;
                }
                else if (phaseTimer <= warningLeadTime)
                {
                    // Countdown 15, 14, 13 ... 1
                    int secondsLeft = Mathf.CeilToInt(phaseTimer);
                    if (netWarningState.Value != secondsLeft)
                    {
                        netWarningState.Value = secondsLeft;
                    }
                }
                break;

            case LavaPhase.Rising:
                if (isAtTarget)
                {
                    // Finished rising -> Hide warning UI & wait 15 seconds before dropping back down
                    netWarningState.Value = -2;
                    phaseTimer = stayRaisedDuration;
                    currentPhase = LavaPhase.StayingRaised;
                }
                break;

            case LavaPhase.StayingRaised:
                phaseTimer -= Time.deltaTime;

                if (phaseTimer <= 0f)
                {
                    // 15 seconds passed -> Return to original position!
                    netTargetY.Value = originalY;
                    currentPhase = LavaPhase.Lowering;
                }
                break;

            case LavaPhase.Lowering:
                if (isAtTarget)
                {
                    // Back at original position -> Start next 1-minute countdown
                    phaseTimer = riseInterval;
                    netWarningState.Value = -2;
                    currentPhase = LavaPhase.WaitingToRise;
                }
                break;
        }
    }

    private void OnWarningStateChanged(int oldVal, int newVal)
    {
        UpdateWarningUI(newVal);
    }

    private void UpdateWarningUI(int state)
    {
        bool shouldShow = (state >= 0);

        if (lavaWarningPanel != null)
        {
            lavaWarningPanel.SetActive(shouldShow);
        }

        if (lavaWarningText != null)
        {
            lavaWarningText.gameObject.SetActive(shouldShow);

            if (state > 0)
            {
                lavaWarningText.text = $"WARNING: LAVA RISING IN {state}s!";
            }
            else if (state == 0)
            {
                lavaWarningText.text = "LAVA IS RISING!";
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleLavaContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        HandleLavaContact(other);
    }

    private void HandleLavaContact(Collider other)
    {
        if (!IsServer) return;
        if (MatchManager.Instance != null && MatchManager.Instance.isMatchOver.Value) return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null || playerHealth.isDead.Value) return;

        ulong netId = playerHealth.NetworkObjectId;

        if (playerHitCooldowns.TryGetValue(netId, out float nextAllowedTime) && Time.time < nextAllowedTime)
        {
            return;
        }

        playerHitCooldowns[netId] = Time.time + hitCooldown;

        playerHealth.TakeDamage(lavaDamage, null);

        if (!playerHealth.isDead.Value)
        {
            PlayerController playerController = other.GetComponentInParent<PlayerController>();
            if (playerController != null)
            {
                Vector3 pushDir;

                if (safeCenterPoint != null)
                {
                    pushDir = safeCenterPoint.position - playerController.transform.position;
                    pushDir.y = 0f;
                }
                else
                {
                    pushDir = -playerController.transform.forward;
                    pushDir.y = 0f;
                }

                if (pushDir.sqrMagnitude < 0.001f)
                {
                    pushDir = Vector3.forward;
                }
                pushDir.Normalize();

                Vector3 finalForce = (pushDir * horizontalPushForce) + (Vector3.up * upwardPushForce);
                playerController.ApplyKnockbackAndStun(finalForce, stunDuration);
            }
        }
    }
}