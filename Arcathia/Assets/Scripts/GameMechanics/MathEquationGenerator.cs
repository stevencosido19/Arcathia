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
    public class EquationData
    {
        public SpellType spellType;
        public string questionText; // e.g., "8 + _ = 16"
        public int correctAnswer;  // e.g., 8
        public List<int> dialOptions; // 4 quadrant numbers
    }

    /// <summary>
    /// Generates a randomized equation based on chosen spell type.
    /// </summary>
    public EquationData GenerateProblem(SpellType spellType)
    {
        EquationData data = new EquationData();
        data.spellType = spellType;

        int a = 0, b = 0, result = 0;
        string symbol = "";

        switch (spellType)
        {
            case SpellType.Fire: // Addition
                a = Random.Range(2, 12);
                b = Random.Range(2, 12);
                result = a + b;
                symbol = "+";
                break;

            case SpellType.Water: // Subtraction
                b = Random.Range(2, 10);
                result = Random.Range(2, 10);
                a = result + b; // Ensures positive subtraction (e.g., 8 - _ = 4)
                symbol = "-";
                break;

            case SpellType.Lightning: // Multiplication
                a = Random.Range(2, 9);
                b = Random.Range(2, 9);
                result = a * b;
                symbol = "×";
                break;
        }

        data.questionText = $"{a} {symbol} _ = {result}";
        data.correctAnswer = b;

        // Generate 3 distractors
        List<int> options = new List<int> { b };
        while (options.Count < 4)
        {
            int distractor = b + Random.Range(-4, 5);
            if (distractor <= 0) distractor = Random.Range(1, 20);

            if (!options.Contains(distractor))
            {
                options.Add(distractor);
            }
        }

        // Shuffle quadrant positions
        for (int i = 0; i < options.Count; i++)
        {
            int temp = options[i];
            int randomIndex = Random.Range(i, options.Count);
            options[i] = options[randomIndex];
            options[randomIndex] = temp;
        }

        data.dialOptions = options;
        return data;
    }
}