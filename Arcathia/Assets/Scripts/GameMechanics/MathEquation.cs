using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMathEquation", menuName = "Scriptable Objects/Math Equation")]
public class MathEquation : ScriptableObject
{
    [Header("Spell Category")]
    public SpellType spellType;

    [Header("Validated Equation Info")]
    public string questionText; // e.g., "8 + _ = 16"
    public int correctAnswer;  // e.g., 8

    [Header("Dial / Quadrant Choices")]
    public List<int> dialOptions = new List<int>(); // Exactly 4 options (1 correct, 3 distractors)

    [Header("Professor Verification Note")]
    [TextArea(2, 4)]
    public string professorNote; // Optional notes/comments from the math professor
}