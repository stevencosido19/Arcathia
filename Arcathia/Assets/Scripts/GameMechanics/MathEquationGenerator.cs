using System.Collections.Generic;
using UnityEngine;

public enum SpellType
{
    Fire,       
    Water,      
    Lightning   
}

public class MathEquationGenerator : MonoBehaviour
{
    [System.Serializable]
    public class ApprovedEquation
    {
        [Header("Spell Category")]
        public SpellType spellType;

        [Header("Validated Equation Info")]
        public string questionText; 
        public int correctAnswer;  

        [Header("Dial Choices (Must have 4 options)")]
        public List<int> dialOptions = new List<int>() { 0, 0, 0, 0 }; 

        [Header("Validation Note")]
        [TextArea(1, 3)]
        public string professorNote; 
    }

    [System.Serializable]
    public class EquationData
    {
        public SpellType spellType;
        public string questionText; 
        public int correctAnswer;  
        public List<int> dialOptions; 
    }

    [Header("Pre-Approved Equation Database")]
    public List<ApprovedEquation> preApprovedDatabase = new List<ApprovedEquation>();

    
    public EquationData GenerateProblem(SpellType spellType)
    {
        
        List<ApprovedEquation> matchingEquations = preApprovedDatabase.FindAll(eq => eq.spellType == spellType);

        if (matchingEquations.Count == 0)
        {
            Debug.LogError($"[MathDatabase ERROR] No pre-approved equations found for SpellType '{spellType}' in the Inspector database!");
            return null;
        }

        
        ApprovedEquation selected = matchingEquations[Random.Range(0, matchingEquations.Count)];

        
        EquationData data = new EquationData();
        data.spellType = selected.spellType;
        data.questionText = selected.questionText;
        data.correctAnswer = selected.correctAnswer;

        
        data.dialOptions = new List<int>(selected.dialOptions);
        ShuffleList(data.dialOptions);

        return data;
    }

    
    private void ShuffleList(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}