using System.Collections.Generic;
using UnityEngine;

public enum SpellType
{
    Fire,       // Addition (+)
    Water,      // Subtraction (-)
    Lightning   // Multiplication (*)
}

public class MathEquationGenerator : MonoBehaviour
{
    [System.Serializable]
    public class ApprovedEquation
    {
        [Header("Spell Category")]
        public SpellType spellType;

        [Header("Validated Equation Info")]
        public string questionText; // e.g., "8 + _ = 16"
        public int correctAnswer;  // e.g., 8

        [Header("Dial Choices (Must have 4 options)")]
        public List<int> dialOptions = new List<int>() { 0, 0, 0, 0 }; // 1 correct answer + 3 distractors

        [Header("Validation Note")]
        [TextArea(1, 3)]
        public string professorNote; // Optional note/comment from the professor
    }

    [System.Serializable]
    public class EquationData
    {
        public SpellType spellType;
        public string questionText; // e.g., "8 + _ = 16"
        public int correctAnswer;  // e.g., 8
        public List<int> dialOptions; // 4 quadrant numbers
    }

    [Header("Pre-Approved Equation Database")]
    public List<ApprovedEquation> preApprovedDatabase = new List<ApprovedEquation>();

    /// <summary>
    /// Fetches a random pre-approved equation from the inspector database matching the requested SpellType.
    /// </summary>
    public EquationData GenerateProblem(SpellType spellType)
    {
        // 1. Filter database for entries matching the requested spell type
        List<ApprovedEquation> matchingEquations = preApprovedDatabase.FindAll(eq => eq.spellType == spellType);

        if (matchingEquations.Count == 0)
        {
            Debug.LogError($"[MathDatabase ERROR] No pre-approved equations found for SpellType '{spellType}' in the Inspector database!");
            return null;
        }

        // 2. Pick a random approved equation
        ApprovedEquation selected = matchingEquations[Random.Range(0, matchingEquations.Count)];

        // 3. Construct and format EquationData output
        EquationData data = new EquationData();
        data.spellType = selected.spellType;
        data.questionText = selected.questionText;
        data.correctAnswer = selected.correctAnswer;

        // Clone dial options and shuffle quadrant positions
        data.dialOptions = new List<int>(selected.dialOptions);
        ShuffleList(data.dialOptions);

        return data;
    }

    /// <summary>
    /// Utility to shuffle quadrant positions so the answer moves randomly around the dial.
    /// </summary>
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