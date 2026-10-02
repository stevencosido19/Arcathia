using Unity.Netcode;
using UnityEngine;

public class PlayerScore : NetworkBehaviour
{
    [Header("Player Score Settings")]
    public NetworkVariable<int> score = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Player Identity Visuals")]
    public Sprite playerAvatar;

    private static Sprite fallbackCircleSprite;

    public Sprite GetAvatar()
    {
        if (playerAvatar != null) return playerAvatar;

        if (fallbackCircleSprite == null)
        {
            Texture2D tex = new Texture2D(32, 32);
            Color[] colors = new Color[32 * 32];
            Vector2 center = new Vector2(16, 16);

            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    colors[y * 32 + x] = dist <= 14 ? Color.green : Color.clear;
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            fallbackCircleSprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
        }

        return fallbackCircleSprite;
    }

    public override void OnNetworkSpawn()
    {
        TriggerScoreboardRebuild();
    }

    public override void OnNetworkDespawn()
    {
        TriggerScoreboardRebuild();
    }

    private void TriggerScoreboardRebuild()
    {
        if (ScoreboardUI.Instance != null)
        {
            ScoreboardUI.Instance.RebuildScoreboard();
        }
        else
        {
            ScoreboardUI ui = FindFirstObjectByType<ScoreboardUI>();
            if (ui != null)
            {
                ui.RebuildScoreboard();
            }
        }
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
}