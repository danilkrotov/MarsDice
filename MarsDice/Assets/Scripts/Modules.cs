using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class Modules : MonoBehaviour
{
    public enum ModuleType
    {
        Generator
    }

    public enum ModuleSize
    {
        I,
        S,
        M,
        L
    }

    [Header("Module")]
    [SerializeField] protected ModuleType type = ModuleType.Generator;
    [SerializeField] protected ModuleSize size = ModuleSize.I;

    [Header("Dice amount (min/max)")]
    [FormerlySerializedAs("minDiceSlots")]
    [Min(1)]
    [SerializeField] private int minDiceSlots = 1;
    [FormerlySerializedAs("maxDiceSlots")]
    [Min(1)]
    [SerializeField] private int maxDiceSlots = 1;

    [FormerlySerializedAs("diceSlots")]
    [Min(1)]
    [SerializeField] private int diceSlots = 1;

    [SerializeField] private List<Dice> dices = new List<Dice>();

    public ModuleType Type => type;
    public ModuleSize Size => size;

    public int DiceCount => diceSlots;
    public int MinDiceCount => minDiceSlots;
    public int MaxDiceCount => maxDiceSlots;

    public IReadOnlyList<Dice> Dices => dices;

    public bool CanAddDice => GetAssignedDiceCount() < diceSlots;

    public virtual bool TryAddDice(Dice dice)
    {
        if (dice == null)
        {
            return false;
        }

        if (GetAssignedDiceCount() >= diceSlots)
        {
            return false;
        }

        if (dices.Contains(dice))
        {
            return false;
        }

        for (int i = 0; i < dices.Count; i++)
        {
            if (dices[i] == null)
            {
                dices[i] = dice;
                return true;
            }
        }

        dices.Add(dice);
        return true;
    }

    /// <summary>
    /// Сбрасывает ссылки на кубики, которые не являются T (для жёсткой привязки типа в наследниках).
    /// </summary>
    protected void EnforceDiceType<TDice>() where TDice : Dice
    {
        if (dices == null)
        {
            return;
        }

        for (int i = 0; i < dices.Count; i++)
        {
            if (dices[i] != null && !(dices[i] is TDice))
            {
                Debug.LogWarning($"{name}: в слоте {i} допустим только {typeof(TDice).Name}, ссылка сброшена.");
                dices[i] = null;
            }
        }
    }

    public bool RemoveDice(Dice dice)
    {
        if (dice == null)
        {
            return false;
        }

        return dices.Remove(dice);
    }

    public void ClearDices()
    {
        dices.Clear();
    }

    protected virtual void OnValidate()
    {
        if (minDiceSlots < 1)
        {
            minDiceSlots = 1;
        }

        if (maxDiceSlots < minDiceSlots)
        {
            maxDiceSlots = minDiceSlots;
        }

        if (diceSlots < minDiceSlots)
        {
            diceSlots = minDiceSlots;
        }
        else if (diceSlots > maxDiceSlots)
        {
            diceSlots = maxDiceSlots;
        }

        if (dices == null)
        {
            dices = new List<Dice>();
            return;
        }

        if (dices.Count > diceSlots)
        {
            dices.RemoveRange(diceSlots, dices.Count - diceSlots);
        }
    }

    private int GetAssignedDiceCount()
    {
        int assigned = 0;
        for (int i = 0; i < dices.Count; i++)
        {
            if (dices[i] != null)
            {
                assigned++;
            }
        }

        return assigned;
    }
}
