using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageDeal : BattleActions
{
    private string phaseName = "Нанесение урона";

    public override string PhaseName => phaseName;

    public override IEnumerator Action(BattleController battleController, int unitIndex, Unit unit)
    {
        if (battleController == null || unit == null)
        {
            yield break;
        }

        IReadOnlyList<GameObject> unitObjects = battleController.UnitObjects;
        for (int diceIndex = 0; diceIndex < unit.DicePrefabs.Count; diceIndex++)
        {
            GameObject dicePrefab = unit.DicePrefabs[diceIndex];
            if (dicePrefab == null)
            {
                continue;
            }

            Vector3 spawnPosition = unit.transform.position + battleController.DiceSpawnOffset;
            GameObject diceObject = Object.Instantiate(dicePrefab, spawnPosition, Quaternion.identity);
            unit.AddSpawnedDice(diceObject);

            Dice dice = diceObject.GetComponent<Dice>();
            if (dice == null)
            {
                Debug.LogWarning($"Префаб {dicePrefab.name} не содержит компонент Dice.");
                Object.Destroy(diceObject);
                continue;
            }

            yield return dice.RollDice();

            if (!dice.LastFailed)
            {
                Unit targetUnit = GetNextTargetUnit(unitIndex, unitObjects);
                if (targetUnit != null)
                {
                    targetUnit.TakeDamage();
                }
            }

            Debug.Log($"Unit {unit.gameObject.name}, кубик #{diceIndex + 1}: выпало {dice.LastResult}, failed={dice.LastFailed}");
        }
    }

    private static Unit GetNextTargetUnit(int attackerIndex, IReadOnlyList<GameObject> unitObjects)
    {
        if (unitObjects == null || unitObjects.Count < 2)
        {
            return null;
        }

        for (int offset = 1; offset < unitObjects.Count; offset++)
        {
            int targetIndex = (attackerIndex + offset) % unitObjects.Count;
            GameObject targetObject = unitObjects[targetIndex];
            if (targetObject == null)
            {
                continue;
            }

            Unit targetUnit = targetObject.GetComponent<Unit>();
            if (targetUnit != null)
            {
                return targetUnit;
            }
        }

        return null;
    }
}
