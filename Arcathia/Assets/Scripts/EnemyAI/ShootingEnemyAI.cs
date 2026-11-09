using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ShootingEnemyAI : NetworkBehaviour
{
    private enum AIState
    {
        Wandering,
        Chasing,
        Shooting
    }

    [Header("Stats")]
    public float maxHealth = 100f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Wander Settings")]
    [Tooltip("Radius around the enemy's starting area to pick random wander spots.")]
    public float wanderRadius = 25f;
    [Tooltip("How many seconds the enemy waits at each wander spot before moving to the next.")]
    public float wanderWaitTime = 3f;

    [Header("Combat Settings")]
    public float shootingRange = 15f;
    public float fireRate = 2f;
    public Transform firePoint;
    public GameObject projectilePrefab;
    [Tooltip("How many seconds to search the last known area after the player leaves the detection collider.")]
    public float loseTargetDelay = 3f;

    [Header("Jumping / Elevated Area Settings")]
    public float jumpDuration = 0.6f;
    public float jumpHeight = 2.5f;

    private NavMeshAgent agent;
    private Transform playerTarget;
    private AIState currentState = AIState.Wandering;

    private float nextFireTime;
    private bool isTraversingLink = false;
    private float waitTimer;
    private float targetLostTimer;
    private Vector3 originPosition;
    private PlayerScore lastAttacker;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.autoTraverseOffMeshLink = false;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            agent.enabled = false;
            return;
        }

        currentHealth.Value = maxHealth;
        originPosition = transform.position;
        SnapToNavMesh();

        currentState = AIState.Wandering;
        PickNewWanderDestination();
    }

    private void SnapToNavMesh()
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10.0f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
        else
        {
            Debug.LogError($"[EnemyAI] Could not find a baked NavMesh near {transform.position}!");
        }
    }

    private void Update()
    {
        if (!IsServer) return;
        if (agent == null || !agent.isOnNavMesh) return;
        if (isTraversingLink) return;

        
        if (agent.isOnOffMeshLink)
        {
            StartCoroutine(JumpOverLinkRoutine());
            return;
        }

        switch (currentState)
        {
            case AIState.Wandering:
                UpdateWanderingState();
                break;

            case AIState.Chasing:
                UpdateChasingState();
                break;

            case AIState.Shooting:
                UpdateShootingState();
                break;
        }
    }

   

    public void OnPlayerDetected(Transform player)
    {
        playerTarget = player;
        targetLostTimer = 0f;
        currentState = AIState.Chasing;
        Debug.Log("<color=yellow>[EnemyAI] Player entered detection collider!</color>");
    }

    public void OnPlayerLost(Transform player)
    {
        if (playerTarget == player)
        {
            targetLostTimer = loseTargetDelay;
        }
    }

    

    private void UpdateWanderingState()
    {
        
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
        {
            waitTimer += Time.deltaTime;

            if (waitTimer >= wanderWaitTime)
            {
                PickNewWanderDestination();
                waitTimer = 0f;
            }
        }
    }

    private void PickNewWanderDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += originPosition; 

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
        {
            if (agent.isStopped) agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
    }

    private void UpdateChasingState()
    {
        if (CheckIfPlayerLost()) return;

        float distance = Vector3.Distance(transform.position, playerTarget.position);

        if (distance <= shootingRange)
        {
            currentState = AIState.Shooting;
            return;
        }

        if (agent.isStopped) agent.isStopped = false;
        agent.SetDestination(playerTarget.position);
    }

    private void UpdateShootingState()
    {
        if (CheckIfPlayerLost()) return;

        float distance = Vector3.Distance(transform.position, playerTarget.position);

        if (distance > shootingRange)
        {
            currentState = AIState.Chasing;
            return;
        }

        
        if (!agent.isStopped) agent.isStopped = true;
        RotateTowards(playerTarget.position);

        if (HasLineOfSight() && Time.time >= nextFireTime)
        {
            ShootProjectile();
            nextFireTime = Time.time + fireRate;
        }
    }

    private bool CheckIfPlayerLost()
    {
        if (playerTarget == null)
        {
            currentState = AIState.Wandering;
            PickNewWanderDestination();
            return true;
        }

        
        if (targetLostTimer > 0f)
        {
            targetLostTimer -= Time.deltaTime;
            if (targetLostTimer <= 0f)
            {
                playerTarget = null;
                currentState = AIState.Wandering;
                PickNewWanderDestination();
                Debug.Log("<color=grey>[EnemyAI] Lost player. Resuming wander.</color>");
                return true;
            }
        }

        return false;
    }

    private void RotateTowards(Vector3 targetPos)
    {
        Vector3 direction = (targetPos - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 6f);
        }
    }

    private bool HasLineOfSight()
    {
        Vector3 eyePos = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.5f;
        Vector3 targetPos = playerTarget.position + Vector3.up * 1.0f; // Aim at chest height
        Vector3 dir = (targetPos - eyePos).normalized;
        float dist = Vector3.Distance(eyePos, targetPos);

        if (Physics.Raycast(eyePos, dir, out RaycastHit hit, dist))
        {
            return hit.collider.GetComponentInParent<PlayerController>() != null;
        }

        return false;
    }

    private void ShootProjectile()
    {
        if (projectilePrefab == null) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + transform.forward + Vector3.up;
        Vector3 aimDirection = ((playerTarget.position + Vector3.up * 1.0f) - spawnPos).normalized;
        Quaternion rotation = Quaternion.LookRotation(aimDirection);

        GameObject projectile = Instantiate(projectilePrefab, spawnPos, rotation);

        NetworkObject netObj = projectile.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
        }
    }

    private IEnumerator JumpOverLinkRoutine()
    {
        isTraversingLink = true;

        OffMeshLinkData linkData = agent.currentOffMeshLinkData;
        Vector3 startPos = agent.transform.position;
        Vector3 endPos = linkData.endPos + Vector3.up * agent.baseOffset;

        float timer = 0f;

        while (timer < jumpDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / jumpDuration;

            
            Vector3 currentPos = Vector3.Lerp(startPos, endPos, progress);
            currentPos.y += 4f * jumpHeight * progress * (1f - progress);

            transform.position = currentPos;
            yield return null;
        }

        transform.position = endPos;

        if (agent.isOnNavMesh)
        {
            agent.CompleteOffMeshLink();
        }

        isTraversingLink = false;
    }

    

    public void TakeDamage(float amount, PlayerScore attacker = null)
    {
        if (!IsServer) return;

        if (attacker != null)
        {
            lastAttacker = attacker;
        }

        currentHealth.Value -= amount;

        if (currentHealth.Value <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        
        if (lastAttacker != null)
        {
            lastAttacker.AddScore(1);
        }
        else
        {
            
            PlayerScore singlePlayer = Object.FindFirstObjectByType<PlayerScore>();
            if (singlePlayer != null)
            {
                singlePlayer.AddScore(1);
            }
        }

        
        GetComponent<NetworkObject>().Despawn(true);
    }

    

    private void OnDrawGizmosSelected()
    {
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, shootingRange);

        
        Gizmos.color = Color.cyan;
        Vector3 center = Application.isPlaying ? originPosition : transform.position;
        Gizmos.DrawWireSphere(center, wanderRadius);
    }
}