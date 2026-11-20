using System.Collections.Generic;
using UnityEngine;

public class RobeEquipper : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Drag the character's first animated bone here (usually named 'Hips' or 'Pelvis')")]
    [SerializeField] private Transform characterRootBone;

    [Tooltip("Drag your Robe Prefab here")]
    public GameObject robePrefab;

    private GameObject currentRobe;
    private Dictionary<string, Transform> boneMap = new Dictionary<string, Transform>();

    private void Awake()
    {
        // 1. Map all animated bones in the character's skeleton by name
        Transform[] allBones = characterRootBone.GetComponentsInChildren<Transform>(true);
        foreach (Transform bone in allBones)
        {
            if (!boneMap.ContainsKey(bone.name))
            {
                boneMap.Add(bone.name, bone);
            }
        }
    }

    private void Start()
    {
        // Automatically equip the robe when the game starts
        if (robePrefab != null)
        {
            EquipRobe(robePrefab);
        }
    }

    public void EquipRobe(GameObject newRobePrefab)
    {
        // Remove existing robe if there is one
        if (currentRobe != null)
        {
            Destroy(currentRobe);
        }

        // 1. Spawn the robe under the player
        currentRobe = Instantiate(newRobePrefab, transform);
        currentRobe.transform.localPosition = Vector3.zero;
        currentRobe.transform.localRotation = Quaternion.identity;

        // 2. Find the SkinnedMeshRenderer on the robe
        SkinnedMeshRenderer robeRenderer = currentRobe.GetComponentInChildren<SkinnedMeshRenderer>();
        if (robeRenderer == null)
        {
            Debug.LogError("The robe prefab doesn't have a SkinnedMeshRenderer!");
            return;
        }

        // 3. Re-link the robe's bones to the character's animated bones
        Transform[] newBones = new Transform[robeRenderer.bones.Length];
        for (int i = 0; i < robeRenderer.bones.Length; i++)
        {
            string boneName = robeRenderer.bones[i].name;

            if (boneMap.TryGetValue(boneName, out Transform targetBone))
            {
                newBones[i] = targetBone;
            }
            else
            {
                Debug.LogWarning($"Bone '{boneName}' not found on character!");
            }
        }

        // 4. Assign the player's bones to the robe
        robeRenderer.bones = newBones;

        // 5. Match the root bone (Hips/Pelvis)
        if (boneMap.TryGetValue(robeRenderer.rootBone.name, out Transform rootBone))
        {
            robeRenderer.rootBone = rootBone;
        }
    }
}