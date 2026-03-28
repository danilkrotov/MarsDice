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
        int diceNumber = 0;
        IReadOnlyList<Modules> modules = unit.Modules;

        for (int mi = 0; mi < modules.Count; mi++)
        {
            Modules module = modules[mi];
            if (module == null)
            {
                continue;
            }

            battleController.LayoutModuleDice(module);

            IReadOnlyList<Dice> dices = module.Dices;
            for (int di = 0; di < dices.Count; di++)
            {
                Dice dice = dices[di];
                if (dice == null)
                {
                    continue;
                }

                diceNumber++;
                yield return dice.RollDice();

                if (!dice.LastFailed)
                {
                    Unit targetUnit = GetNextTargetUnit(unitIndex, unitObjects);
                    if (targetUnit != null)
                    {
                        targetUnit.TakeDamage();
                    }
                }

                Debug.Log($"Unit {unit.gameObject.name}, кубик #{diceNumber}: выпало {dice.LastResult}, failed={dice.LastFailed}");
            }
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
