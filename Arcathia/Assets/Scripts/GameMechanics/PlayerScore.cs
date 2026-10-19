using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerScore : NetworkBehaviour
{
    [Header("Networked Player Stats")]
    public NetworkVariable<int> score = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> avatarIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>(
        "Player",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        // 1. If we own this player, send our saved local profile to the server
        if (IsOwner)
        {
            string savedName = GameDataManager.Instance != null ? GameDataManager.Instance.PlayerName : "Player";
            int savedAvatar = GameDataManager.Instance != null ? GameDataManager.Instance.SelectedAvatarIndex : 0;

            SetProfileServerRpc(savedName, savedAvatar);
        }

        // 2. Listen for profile changes from other players to refresh UI
        avatarIndex.OnValueChanged += OnProfileDataChanged;
        playerName.OnValueChanged += OnProfileDataChanged;

        TriggerScoreboardRebuild();
    }

    public override void OnNetworkDespawn()
    {
        avatarIndex.OnValueChanged -= OnProfileDataChanged;
        playerName.OnValueChanged -= OnProfileDataChanged;

        TriggerScoreboardRebuild();
    }

    private void OnProfileDataChanged<T>(T oldVal, T newVal)
    {
        TriggerScoreboardRebuild();
    }

    [ServerRpc]
    private void SetProfileServerRpc(string name, int avatar)
    {
        playerName.Value = name;
        avatarIndex.Value = avatar;
    }

    public Sprite GetAvatar()
    {
        if (GameDataManager.Instance != null)
        {
            return GameDataManager.Instance.GetAvatar(avatarIndex.Value);
        }

        return null;
    }

    public string GetPlayerName()
    {
        return playerName.Value.ToString();
    }

    public void AddScore(int value)
    {
        if (IsServer)
        {
            score.Value += value;
        }
        else
        {
            AddScoreServerRpc(value);
        }
    }

    [ServerRpc]
    private void AddScoreServerRpc(int value)
    {
        score.Value += value;
    }

    private void TriggerScoreboardRebuild()
    {
        if (ScoreboardUI.Instance != null)
        {
            ScoreboardUI.Instance.RebuildScoreboard();
        }
    }
}