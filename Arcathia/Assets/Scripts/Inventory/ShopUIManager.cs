using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class ShopUIManager : MonoBehaviour
{
    [Header("Data")]
    public List<CosmeticItem> allItems;
    private PlayerSaveData saveData;

    [Header("UI References")]
    public TextMeshProUGUI moneyText;
    public Transform itemGrid;
    public GameObject slotPrefab;
    public Button actionButton;
    public TextMeshProUGUI actionButtonText;
    public Button backToMenuButton; // Renamed from playButton

    [Header("3D Preview Holders")]
    public Transform hatHolder;
    public Transform faceHolder;
    public Transform accessoryHolder;
    public Transform robeHolder;
    public Transform wandHolder;

    private CosmeticCategory currentCategory = CosmeticCategory.Hat;
    private CosmeticItem selectedItem = null;

    void Start()
    {
        saveData = SaveManager.LoadData();
        UpdateMoneyDisplay();
        RefreshPreviewModel();
        PopulateGrid(currentCategory);

        // Link back to main menu
        backToMenuButton.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));
        actionButton.onClick.AddListener(OnActionClicked);
    }

    // --- NEW MOBILE PROTOTYPE METHODS ---
    public void AddPrototypeMoney()
    {
        saveData.currency += 500;
        SaveManager.SaveData(saveData);
        UpdateMoneyDisplay();
        UpdateActionButtonState();
    }

    public void ResetPrototypeData()
    {
        saveData = new PlayerSaveData(); 
        SaveManager.SaveData(saveData);
        UpdateMoneyDisplay();
        UpdateActionButtonState();
        RefreshPreviewModel();
    }
    // ------------------------------------

    public void SelectCategory(int categoryIndex)
    {
        currentCategory = (CosmeticCategory)categoryIndex;
        selectedItem = null;
        actionButton.gameObject.SetActive(false);
        PopulateGrid(currentCategory);
    }

    void PopulateGrid(CosmeticCategory category)
    {
        foreach (Transform child in itemGrid)
        {
            Destroy(child.gameObject);
        }

        foreach (var item in allItems)
        {
            if (item.category == category)
            {
                GameObject slot = Instantiate(slotPrefab, itemGrid);
                slot.transform.GetChild(0).GetComponent<Image>().sprite = item.icon;
                
                Button btn = slot.GetComponent<Button>();
                btn.onClick.AddListener(() => SelectItem(item));
            }
        }
    }

    void SelectItem(CosmeticItem item)
    {
        selectedItem = item;
        actionButton.gameObject.SetActive(true);
        UpdateActionButtonState();
    }

    void UpdateActionButtonState()
    {
        if (selectedItem == null) return;

        bool isUnlocked = saveData.unlockedItems.Contains(selectedItem.itemID);
        string currentEquipped = GetEquippedIdForCategory(selectedItem.category);

        if (!isUnlocked)
            actionButtonText.text = "$" + selectedItem.price + " BUY";
        else if (currentEquipped == selectedItem.itemID)
            actionButtonText.text = "UNEQUIP";
        else
            actionButtonText.text = "EQUIP";
    }

    void OnActionClicked()
    {
        if (selectedItem == null) return;

        bool isUnlocked = saveData.unlockedItems.Contains(selectedItem.itemID);
        string currentEquipped = GetEquippedIdForCategory(selectedItem.category);

        if (!isUnlocked)
        {
            if (saveData.currency >= selectedItem.price)
            {
                saveData.currency -= selectedItem.price;
                saveData.unlockedItems.Add(selectedItem.itemID);
                UpdateMoneyDisplay();
            }
            else return; 
        }
        else if (currentEquipped == selectedItem.itemID)
        {
            SetEquippedIdForCategory(selectedItem.category, ""); 
        }
        else
        {
            SetEquippedIdForCategory(selectedItem.category, selectedItem.itemID); 
        }

        SaveManager.SaveData(saveData);
        UpdateActionButtonState();
        RefreshPreviewModel();
    }

    void RefreshPreviewModel()
    {
        ToggleCosmetic(hatHolder, saveData.equippedHat);
        ToggleCosmetic(faceHolder, saveData.equippedFace);
        ToggleCosmetic(accessoryHolder, saveData.equippedAccessory);
        ToggleCosmetic(robeHolder, saveData.equippedRobe);
        ToggleCosmetic(wandHolder, saveData.equippedWand);
    }

    void ToggleCosmetic(Transform holder, string equippedID)
    {
        for (int i = 0; i < holder.childCount; i++)
        {
            holder.GetChild(i).gameObject.SetActive(false);
        }

        if (string.IsNullOrEmpty(equippedID)) return;

        CosmeticItem item = allItems.Find(x => x.itemID == equippedID);
        if (item != null && item.modelIndex < holder.childCount)
        {
            holder.GetChild(item.modelIndex).gameObject.SetActive(true);
        }
    }

    private void UpdateMoneyDisplay() => moneyText.text = "$" + saveData.currency;

    private string GetEquippedIdForCategory(CosmeticCategory cat)
    {
        switch (cat)
        {
            case CosmeticCategory.Hat: return saveData.equippedHat;
            case CosmeticCategory.Face: return saveData.equippedFace;
            case CosmeticCategory.Accessory: return saveData.equippedAccessory;
            case CosmeticCategory.Robe: return saveData.equippedRobe;
            case CosmeticCategory.Wand: return saveData.equippedWand;
            default: return "";
        }
    }

    private void SetEquippedIdForCategory(CosmeticCategory cat, string id)
    {
        switch (cat)
        {
            case CosmeticCategory.Hat: saveData.equippedHat = id; break;
            case CosmeticCategory.Face: saveData.equippedFace = id; break;
            case CosmeticCategory.Accessory: saveData.equippedAccessory = id; break;
            case CosmeticCategory.Robe: saveData.equippedRobe = id; break;
            case CosmeticCategory.Wand: saveData.equippedWand = id; break;
        }
    }
}