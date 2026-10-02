using System;
using System.Collections.Generic;
using UnityEngine;

public enum CosmeticCategory { Hat, Face, Accessory, Robe, Wand }

[CreateAssetMenu(fileName = "New Cosmetic", menuName = "Shop/Cosmetic Item")]
public class CosmeticItem : ScriptableObject
{
    public string itemID;
    public CosmeticCategory category;
    public int price;
    public Sprite icon;
    public int modelIndex; // The child index to toggle active in the 3D holder
}

[Serializable]
public class PlayerSaveData
{
    public int currency = 1000;
    public List<string> unlockedItems = new List<string>();
    
    // Storing equipped IDs by category
    public string equippedHat = "";
    public string equippedFace = "";
    public string equippedAccessory = "";
    public string equippedRobe = "";
    public string equippedWand = "";
}