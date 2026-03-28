using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShieldUp : BattleActions
{
    private const string PhaseTitle = "Пополнение щита";

    public override string PhaseName => PhaseTitle;

    public override IEnumerator Action(BattleController battleController, int unitIndex, Unit unit)
    {
        if (battleController == null || unit == null)
        {
            yield break;
        }

        MShield[] shields = unit.GetComponentsInChildren<MShield>(true);
        for (int si = 0; si < shields.Length; si++)
        {
            MShield shieldModule = shields[si];
            if (shieldModule == null)
            {
                continue;
            }

            battleController.LayoutModuleDice(shieldModule);

            IReadOnlyList<Dice> dices = shieldModule.Dices;
            for (int di = 0; di < dices.Count; di++)
            {
                Dice dice = dices[di];
                if (dice == null)
                {
                    continue;
                }

                yield return dice.RollDice();

                if (dice.LastFailed)
                {
                    continue;
                }

                if (dice is ShieldDice shieldDice)
                {
                    int cost = shieldDice.GetEnergyCostByFace(dice.LastResult);
                    int restore = shieldDice.GetShieldRestoreByFace(dice.LastResult);

                    if (!TrySpendEnergyFromGenerators(unit, cost))
                    {
                        Debug.Log($"{unit.name} / {shieldModule.name}: не хватает энергии ({cost}) для грани {dice.LastResult}.");
                        continue;
                    }

                    if (restore > 0)
                    {
                        unit.AddShield(restore);
                        Debug.Log($"{unit.name} / {shieldModule.name}: грань {dice.LastResult}, −{cost} энергии, +{restore} щита (юнит {unit.CurrentShield}/{unit.MaxShield}).");
                    }
                }
            }
        }
    }

    private static bool TrySpendEnergyFromGenerators(Unit unit, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        MGenerator[] generators = unit.GetComponentsInChildren<MGenerator>(true);
        int pool = 0;
        for (int i = 0; i < generators.Length; i++)
        {
            if (generators[i] != null)
            {
                pool += generators[i].CurrentCharge;
            }
        }

        if (pool < amount)
        {
            return false;
        }

        int remaining = amount;
        for (int i = 0; i < generators.Length; i++)
        {
            if (generators[i] == null)
            {
                continue;
            }

            int take = Mathf.Min(generators[i].CurrentCharge, remaining);
            if (take > 0)
            {
                generators[i].SubtractCharge(take);
                remaining -= take;
            }

            if (remaining <= 0)
            {
                return true;
            }
        }

        return remaining <= 0;
    }
}
