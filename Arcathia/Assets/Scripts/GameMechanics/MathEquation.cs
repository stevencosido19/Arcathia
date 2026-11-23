using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMathEquation", menuName = "Scriptable Objects/Math Equation")]
public class MathEquation : ScriptableObject
{
    [Header("Spell Category")]
    public SpellType spellType;

    [Header("Validated Equation Info")]
    public string questionText; 
    public int correctAnswer;  

    [Header("Dial / Quadrant Choices")]
    public List<int> dialOptions = new List<int>(); 

    [Header("Professor Verification Note")]
    [TextArea(2, 4)]
    public string professorNote; 
}