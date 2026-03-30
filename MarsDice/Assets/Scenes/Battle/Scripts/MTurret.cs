using System.Collections.Generic;
using UnityEngine;

/// <summary>Модуль турели: урон за выстрел и расход энергии за выстрел (логика боя подключается отдельно).</summary>
public class MTurret : Modules
{
    [Header("Турель")]
    [Min(0)]
    [SerializeField] private int baseDamagePerShot = 1;

    [Min(0)]
    [SerializeField] private int energyCostPerShot = 1;

    [Header("Локальная позиция кубиков при сбросе к модулю")]
    [SerializeField] private Vector3 startDiceLocalPosition = Vector3.zero;

    public int BaseDamagePerShot => baseDamagePerShot;
    public int EnergyCostPerShot => energyCostPerShot;

    protected override void Awake()
    {
        base.Awake();
        type = ModuleType.Turret;
    }

    protected override void OnValidate()
    {
        type = ModuleType.Turret;
        base.OnValidate();

        if (baseDamagePerShot < 0)
        {
            baseDamagePerShot = 0;
        }

        if (energyCostPerShot < 0)
        {
            energyCostPerShot = 0;
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
