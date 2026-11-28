using Unity.Netcode;
using UnityEngine;

public class RunePickup : NetworkBehaviour
{
    [Header("Rune Settings")]
    public SpellType spellType = SpellType.Water;

    [Header("Fortnite-Style Floating")]
    [Tooltip("Optional: Drag the child 3D mesh here so the collider stays steady while the visual floats. Leave empty to float the whole object.")]
    public Transform visualModel;
    public float hoverHeight = 0.5f;
    public float bobAmplitude = 0.2f;
    public float bobSpeed = 2.5f;
    public float rotateSpeed = 75f;

    private Vector3 startLocalPos;
    private float randomOffset;
    private bool hasPickedUp = false;

    private void Awake()
    {
        randomOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Start()
    {
        InitializeFloatPosition();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        hasPickedUp = false;

        if (visualModel != null)
        {
            visualModel.gameObject.SetActive(true);
        }

        InitializeFloatPosition();
    }

    private void InitializeFloatPosition()
    {
        if (visualModel != null)
        {
            startLocalPos = visualModel.localPosition;
            startLocalPos.y = hoverHeight;
        }
        else
        {
            startLocalPos = transform.localPosition;
        }
    }

    private void Update()
    {
        if (hasPickedUp) return;

        Transform target = visualModel != null ? visualModel : transform;

        // 1. Smooth up-and-down floating
        float yOffset = Mathf.Sin((Time.time + randomOffset) * bobSpeed) * bobAmplitude;
        Vector3 pos = startLocalPos;
        pos.y += yOffset;
        target.localPosition = pos;

        // 2. Smooth spin (Space.World keeps tilted meshes spinning cleanly like Fortnite loot)
        target.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasPickedUp) return;

        PlayerMagicNetwork playerMagic = other.GetComponentInParent<PlayerMagicNetwork>();

        if (playerMagic != null && playerMagic.IsOwner)
        {
            hasPickedUp = true;

            // Hide visual immediately on pickup for instant feedback
            if (visualModel != null)
            {
                visualModel.gameObject.SetActive(false);
            }

            SpellbookUI spellbook = FindFirstObjectByType<SpellbookUI>();
            if (spellbook != null)
            {
                spellbook.AddRuneToInventory(spellType);
                Debug.Log($"Picked up {spellType} Rune!");
            }

            if (IsSpawned)
            {
                DespawnRuneRpc();
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void DespawnRuneRpc()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }
}