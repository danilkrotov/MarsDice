using UnityEngine;

public class ShieldDice : Dice
{
    [Header("Пополнение щита по грани")]
    [Min(0)]
    [SerializeField] private int face1ShieldRestore = 1;
    [Min(0)]
    [SerializeField] private int face2ShieldRestore = 1;
    [Min(0)]
    [SerializeField] private int face3ShieldRestore = 1;
    [Min(0)]
    [SerializeField] private int face4ShieldRestore = 1;
    [Min(0)]
    [SerializeField] private int face5ShieldRestore = 1;
    [Min(0)]
    [SerializeField] private int face6ShieldRestore = 1;

    [Header("Затрата энергии по грани")]
    [Min(0)]
    [SerializeField] private int face1EnergyCost = 0;
    [Min(0)]
    [SerializeField] private int face2EnergyCost = 0;
    [Min(0)]
    [SerializeField] private int face3EnergyCost = 0;
    [Min(0)]
    [SerializeField] private int face4EnergyCost = 0;
    [Min(0)]
    [SerializeField] private int face5EnergyCost = 0;
    [Min(0)]
    [SerializeField] private int face6EnergyCost = 0;

    public int GetShieldRestoreByFace(int face)
    {
        switch (face)
        {
            case 1: return face1ShieldRestore;
            case 2: return face2ShieldRestore;
            case 3: return face3ShieldRestore;
            case 4: return face4ShieldRestore;
            case 5: return face5ShieldRestore;
            case 6: return face6ShieldRestore;
            default: return 0;
        }
    }

    public int GetEnergyCostByFace(int face)
    {
        switch (face)
        {
            case 1: return face1EnergyCost;
            case 2: return face2EnergyCost;
            case 3: return face3EnergyCost;
            case 4: return face4EnergyCost;
            case 5: return face5EnergyCost;
            case 6: return face6EnergyCost;
            default: return 0;
        }
    }
}
