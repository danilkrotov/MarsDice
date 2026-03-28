using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnergyRegen : BattleActions
{
    private const string PhaseTitle = "Восстановление энергии";

    public override string PhaseName => PhaseTitle;

    public override IEnumerator Action(BattleController battleController, int unitIndex, Unit unit)
    {
        if (battleController == null || unit == null)
        {
            yield break;
        }

        MGenerator[] generators = unit.GetComponentsInChildren<MGenerator>(true);
        for (int gi = 0; gi < generators.Length; gi++)
        {
            MGenerator generator = generators[gi];
            if (generator == null)
            {
                continue;
            }

            battleController.LayoutModuleDice(generator);

            IReadOnlyList<Dice> dices = generator.Dices;
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

                if (dice is EnergyDice energyDice)
                {
                    int restore = energyDice.GetEnergyRestoreByFace(dice.LastResult);
                    generator.AddCharge(restore);
                    Debug.Log($"{unit.name} / {generator.name}: грань {dice.LastResult}, +{restore} энергии (заряд {generator.CurrentCharge}/{generator.MaxCharge}).");
                }
            }
        }
    }
}
