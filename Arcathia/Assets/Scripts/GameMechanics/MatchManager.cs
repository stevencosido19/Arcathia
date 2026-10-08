using Unity.Netcode;
using UnityEngine;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance { get; private set; }

    [Header("Match Settings")]
    public float matchDuration = 180f; // 3 Minutes default

    [Header("Networked Match States")]
    public NetworkVariable<float> timeRemaining = new NetworkVariable<float>(
        180f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> isMatchOver = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            timeRemaining.Value = matchDuration;
            isMatchOver.Value = false;
        }
    }

    private void Update()
    {
        if (!IsServer || isMatchOver.Value) return;

        timeRemaining.Value -= Time.deltaTime;

        if (timeRemaining.Value <= 0f)
        {
            timeRemaining.Value = 0f;
            EndMatch();
        }
    }

    private void EndMatch()
    {
        if (!IsServer || isMatchOver.Value) return;

        isMatchOver.Value = true;
        Debug.Log("<color=gold>[MatchManager] MATCH OVER! Calculating final rankings...</color>");
    }
}