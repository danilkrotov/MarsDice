using System.Collections.Generic;
using UnityEngine;

public class MShield : Modules
{
    [Header("Щит модуля")]
    [Min(0)]
    [SerializeField] private int currentShield = 10;
    [Min(0)]
    [SerializeField] private int maxShield = 10;

    [Header("Локальная позиция кубиков при сбросе к модулю")]
    [SerializeField] private Vector3 startDiceLocalPosition = Vector3.zero;

    /// <summary>Начальное значение щита (задаётся в инспекторе). Используется Unit.Start для инициализации.</summary>
    public int CurrentShield => currentShield;
    public int MaxShield => maxShield;

    public override bool TryAddDice(Dice dice)
    {
        if (dice != null && !(dice is ShieldDice))
        {
            Debug.LogWarning($"{name}: MShield принимает только ShieldDice.");
            return false;
        }

        return base.TryAddDice(dice);
    }

    protected override void Awake()
    {
        base.Awake();
        type = ModuleType.Shield;
    }

    protected override void OnValidate()
    {
        type = ModuleType.Shield;
        base.OnValidate();

        EnforceDiceType<ShieldDice>();

        if (maxShield < 0)
        {
            maxShield = 0;
        }

        if (currentShield < 0)
        {
            currentShield = 0;
        }

        if (currentShield > maxShield)
        {
            currentShield = maxShield;
        }
    }

    public override void ResetDiceToModuleLocalLayout()
    {
        IReadOnlyList<Dice> list = Dices;
        int idx = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null)
            {
                continue;
            }

            Transform t = list[i].transform;
            t.SetParent(transform, false);
            t.localPosition = startDiceLocalPosition + new Vector3(idx * resetDiceHorizontalStep, 0f, 0f);
            t.localRotation = Quaternion.identity;
            idx++;
        }
    }
}
