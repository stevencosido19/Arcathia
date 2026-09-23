using UnityEngine;
using TMPro;

public enum ElementType { Flame, Water, Electric }

[System.Serializable]
public class ElementPageData
{
    public ElementType elementType;
    public string elementTitle;
    public int num1;
    public int num2;
    public int missingAnswer;
    public string mathOperator;
    public int ammoReward = 3;
    public bool isSolved = false;

    public string GetEquationString()
    {
        return $"{num1} {mathOperator} _ = {num2}";
    }
}

public class ElementalSpellBook : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI elementTitleText;
    public TextMeshProUGUI equationDisplayText;
    public RuneDialController runeDial;

    [Header("Player Reference")]
    public MagicShooter shooter;

    [Header("Active Pages")]
    public ElementPageData flamePage = new ElementPageData { elementType = ElementType.Flame, elementTitle = "FLAME RUNE (+)" };
    public ElementPageData waterPage = new ElementPageData { elementType = ElementType.Water, elementTitle = "WATER RUNE (-)" };
    public ElementPageData electricPage = new ElementPageData { elementType = ElementType.Electric, elementTitle = "ELECTRIC RUNE (x)" };

    private int currentPageIndex = 0; // 0 = Flame (+), 1 = Water (-), 2 = Electric (x)
    private ElementPageData activePage;

    public ElementType CurrentElementType => activePage != null ? activePage.elementType : ElementType.Flame;

    private void Start()
    {
        GenerateFixedEquation(flamePage);
        GenerateFixedEquation(waterPage);
        GenerateFixedEquation(electricPage);

        UpdateActivePageDisplay();
    }

    public void SwitchPage(int direction)
    {
        currentPageIndex = (currentPageIndex + direction + 3) % 3;
        UpdateActivePageDisplay();
    }

    public void UpdateActivePageDisplay()
    {
        switch (currentPageIndex)
        {
            case 0: activePage = flamePage; break;
            case 1: activePage = waterPage; break;
            case 2: activePage = electricPage; break;
        }

        // Check if current magic capacity is full
        bool isFull = shooter != null && shooter.IsElementFull(activePage.elementType);

        if (isFull)
        {
            // Hide or disable Element Title text when full
            if (elementTitleText != null)
            {
                elementTitleText.gameObject.SetActive(false);
            }

            if (equationDisplayText != null) equationDisplayText.text = "<color=#FF3333>MAX MAGIC REACHED!</color>";
            if (runeDial != null)
            {
                runeDial.SetDialInteractable(false);
            }
        }
        else
        {
            // Enable and show Element Title text when not full
            if (elementTitleText != null)
            {
                elementTitleText.gameObject.SetActive(true);
                elementTitleText.text = activePage.elementTitle;
            }

            if (equationDisplayText != null) equationDisplayText.text = activePage.GetEquationString();
            if (runeDial != null)
            {
                runeDial.RegenerateDialChoices(activePage.missingAnswer);
                runeDial.SetDialInteractable(true);
            }
        }
    }

    public void GenerateFixedEquation(ElementPageData page)
    {
        switch (page.elementType)
        {
            case ElementType.Flame:
                // FLAME: Always Addition (+)
                int flameA = Random.Range(1, 15);
                int flameAns = Random.Range(1, 12);
                page.num1 = flameA;
                page.num2 = flameA + flameAns;
                page.missingAnswer = flameAns;
                page.mathOperator = "+";
                break;

            case ElementType.Water:
                // WATER: Always Subtraction (-)
                int waterA = Random.Range(10, 30);
                int waterAns = Random.Range(1, waterA);
                page.num1 = waterA;
                page.num2 = waterA - waterAns;
                page.missingAnswer = waterAns;
                page.mathOperator = "-";
                break;

            case ElementType.Electric:
                // ELECTRIC: Always Multiplication (x)
                int elecA = Random.Range(2, 10);
                int elecAns = Random.Range(2, 10);
                page.num1 = elecA;
                page.num2 = elecA * elecAns;
                page.missingAnswer = elecA * elecAns;
                page.mathOperator = "x";
                break;
        }

        page.isSolved = false;
    }

    public void SubmitRuneAnswer(int playerAnswer)
    {
        // Block answering if capacity is full
        if (shooter != null && shooter.IsElementFull(activePage.elementType))
        {
            return;
        }

        if (playerAnswer == activePage.missingAnswer)
        {
            if (shooter != null)
            {
                shooter.AddElementAmmo(activePage.elementType, activePage.ammoReward);
            }

            GenerateFixedEquation(activePage);
            UpdateActivePageDisplay();
        }
    }
}