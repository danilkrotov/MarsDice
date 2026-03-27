using UnityEngine;

public class EnergyDice : Dice
{
    [Header("Energy restore by face")]
    [SerializeField] private int face1EnergyRestore = 1;
    [SerializeField] private int face2EnergyRestore = 1;
    [SerializeField] private int face3EnergyRestore = 1;
    [SerializeField] private int face4EnergyRestore = 1;
    [SerializeField] private int face5EnergyRestore = 1;
    [SerializeField] private int face6EnergyRestore = 1;

    public int GetEnergyRestoreByFace(int face)
    {
        switch (face)
        {
            case 1: return face1EnergyRestore;
            case 2: return face2EnergyRestore;
            case 3: return face3EnergyRestore;
            case 4: return face4EnergyRestore;
            case 5: return face5EnergyRestore;
            case 6: return face6EnergyRestore;
            default: return 0;
        }
    }
}
