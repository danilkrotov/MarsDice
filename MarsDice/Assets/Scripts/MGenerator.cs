using System.Collections.Generic;
using UnityEngine;

public class MGenerator : Modules
{
    [Header("Generator charge")]
    [Min(0)]
    [SerializeField] private int currentCharge = 3;
    [Min(0)]
    [SerializeField] private int maxCharge = 3;

    [Header("Start dice")]
    [SerializeField] private string energyDicePrefabPath = "Dices/DiceEnergy";
    [SerializeField] private Vector3 startDiceLocalPosition = Vector3.zero;

    public int CurrentCharge => currentCharge;
    public int MaxCharge => maxCharge;

    public void AddCharge(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentCharge = Mathf.Min(maxCharge, currentCharge + amount);
    }

    /// <returns>Сколько реально списано (не больше запрошенного и не больше текущего заряда).</returns>
    public int SubtractCharge(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        int take = Mathf.Min(currentCharge, amount);
        currentCharge -= take;
        return take;
    }

    public override bool TryAddDice(Dice dice)
    {
        if (dice != null && !(dice is EnergyDice))
        {
            Debug.LogWarning($"{name}: MGenerator принимает только EnergyDice.");
            return false;
        }

        return base.TryAddDice(dice);
    }

    // Awake: кубики есть до Start() у других скриптов (BattleController проверяет Dices в том же кадре).
    private void Awake()
    {
        SpawnStartEnergyDice();
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        EnforceDiceType<EnergyDice>();

        if (maxCharge < 0)
        {
            maxCharge = 0;
        }

        if (currentCharge < 0)
        {
            currentCharge = 0;
        }

        if (currentCharge > maxCharge)
        {
            currentCharge = maxCharge;
        }
    }

    private void SpawnStartEnergyDice()
    {
        GameObject prefab = Resources.Load<GameObject>(energyDicePrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"{name}: не найден префаб по пути Resources/{energyDicePrefabPath}");
            return;
        }

        GameObject diceObject = Instantiate(prefab, transform);
        diceObject.transform.localPosition = startDiceLocalPosition;
        diceObject.transform.localRotation = Quaternion.identity;

        EnergyDice energyDice = diceObject.GetComponent<EnergyDice>();
        if (energyDice == null)
        {
            Debug.LogWarning($"{name}: префаб {prefab.name} не содержит компонент EnergyDice.");
            Destroy(diceObject);
            return;
        }

        if (!TryAddDice(energyDice))
        {
            Destroy(diceObject);
            return;
        }

        // Не вызываем раскладку по экрану здесь: кубик остаётся у модуля до боевой фазы
        // (EnergyRegen / DamageDeal вызывают BattleController.LayoutModuleDice перед броском).
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
