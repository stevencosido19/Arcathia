using UnityEditor;
using UnityEngine;

public static class AvatarMaskFromCharacter
{
    [MenuItem("Tools/Create Avatar Mask From Selected Character")]
    private static void CreateMask()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog(
                "No Object Selected",
                "Select your character (or Player prefab) that has the Animator component, then run this tool again.",
                "OK");
            return;
        }

        Animator anim = selected.GetComponent<Animator>();
        if (anim == null)
        {
            anim = selected.GetComponentInChildren<Animator>();
        }

        if (anim == null)
        {
            EditorUtility.DisplayDialog(
                "No Animator Found",
                "Could not find an Animator component on the selected object or its children.",
                "OK");
            return;
        }

        AvatarMask mask = new AvatarMask();
        mask.AddTransformPath(anim.transform, true);

        // Turn off Humanoid body parts so only Generic transform masking is used
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        }

        // Start with all bones unchecked so you can check only the Upper Body
        for (int i = 0; i < mask.transformCount; i++)
        {
            mask.SetTransformActive(i, false);
        }

        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/UpperBodyMask_Generated.mask");
        AssetDatabase.CreateAsset(mask, path);
        AssetDatabase.SaveAssets();

        Selection.activeObject = mask;
        EditorGUIUtility.PingObject(mask);
        Debug.Log($"[AvatarMask] Created '{path}' with {mask.transformCount} bones from '{anim.name}'.");
    }
}