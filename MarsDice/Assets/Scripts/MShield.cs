using System.Collections.Generic;
using UnityEngine;

public class MShield : Modules
{
    [Header("Щит модуля")]
    [Min(0)]
    [SerializeField] private int currentShield = 10;
    [Min(0)]
    [SerializeField] private int maxShield = 10;

    [Header("Стартовый кубик")]
    [SerializeField] private string shieldDicePrefabPath = "Dices/DiceShield";
    [SerializeField] private Vector3 startDiceLocalPosition = Vector3.zero;

    public int CurrentShield => currentShield;
    public int MaxShield => maxShield;

    public void AddShield(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentShield = Mathf.Min(maxShield, currentShield + amount);
    }

    public void ReduceShield(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentShield = Mathf.Max(0, currentShield - amount);
    }

    public void SetShield(int value)
    {
        currentShield = Mathf.Clamp(value, 0, maxShield);
    }

    public override bool TryAddDice(Dice dice)
    {
        if (dice != null && !(dice is ShieldDice))
        {
            Debug.LogWarning($"{name}: MShield принимает только ShieldDice.");
            return false;
        }

        return base.TryAddDice(dice);
    }

    private void Awake()
    {
        type = ModuleType.Shield;
        SpawnStartShieldDice();
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

    private void SpawnStartShieldDice()
    {
        GameObject prefab = Resources.Load<GameObject>(shieldDicePrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"{name}: не найден префаб по пути Resources/{shieldDicePrefabPath}");
            return;
        }

        GameObject diceObject = Instantiate(prefab, transform);
        diceObject.transform.localPosition = startDiceLocalPosition;
        diceObject.transform.localRotation = Quaternion.identity;

        ShieldDice shieldDice = diceObject.GetComponent<ShieldDice>();
        if (shieldDice == null)
        {
            Debug.LogWarning($"{name}: префаб {prefab.name} не содержит компонент ShieldDice.");
            Destroy(diceObject);
            return;
        }

        if (!TryAddDice(shieldDice))
        {
            Destroy(diceObject);
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
