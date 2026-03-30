using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnergyRegen : BattleActions
{
    private const string PhaseTitle = "Восстановление энергии";

    public override string PhaseName => PhaseTitle;

    /// <summary>Ожидание ЛКМ делаем внутри фазы, пока кубик ещё на экране; иначе BattleController ждал бы уже после Destroy.</summary>
    public override bool UsesManualAdvanceClick => false;

    public override IEnumerator Action(BattleController battleController, int unitIndex, Unit unit)
    {
        if (battleController == null || unit == null)
        {
            yield break;
        }

        var generators = new List<MGenerator>(4);
        IReadOnlyList<Modules> modules = unit.Modules;
        for (int mi = 0; mi < modules.Count; mi++)
        {
            if (modules[mi] is MGenerator gen)
            {
                generators.Add(gen);
            }
        }

        if (generators.Count == 0)
        {
            yield break;
        }

        if (AllGeneratorsFullyCharged(generators))
        {
            Chat.Push($"Фаза {PhaseName} пропущена т.к. у вас полная электроэнергия");
            yield break;
        }

        Chat.Push($"Началась фаза {PhaseName}");

        for (int gi = 0; gi < generators.Count; gi++)
        {
            generators[gi].ReplenishConsumedDice();
        }

        int totalEnergyRestored = 0;

        var allDice = new List<Dice>(8);
        for (int gi = 0; gi < generators.Count; gi++)
        {
            IReadOnlyList<Dice> dices = generators[gi].Dices;
            for (int di = 0; di < dices.Count; di++)
            {
                Dice dice = dices[di];
                if (dice != null)
                {
                    allDice.Add(dice);
                }
            }
        }

        if (allDice.Count == 0)
        {
            yield break;
        }

        battleController.LayoutDiceGroupCenteredOnScreen(allDice);

        yield return RollAllDiceInParallel(allDice);

        for (int i = 0; i < allDice.Count; i++)
        {
            Dice dice = allDice[i];
            if (dice == null)
            {
                continue;
            }

            MGenerator generator = dice.GetComponentInParent<MGenerator>();
            if (generator != null && !dice.LastFailed && dice is EnergyDice energyDice)
            {
                int restore = energyDice.GetEnergyRestoreByFace(dice.LastResult);
                generator.AddCharge(restore);
                totalEnergyRestored += restore;
                Debug.Log($"{unit.name} / {generator.name}: грань {dice.LastResult}, +{restore} энергии (заряд {generator.CurrentCharge}/{generator.MaxCharge}).");
            }
        }

        if (totalEnergyRestored > 0)
        {
            Chat.Push($"{unit.name} восстановил {totalEnergyRestored} ед энергии.");
        }

        if (!unit.IsAI && allDice.Count > 0)
        {
            yield return WaitForLeftClickAnywhere();
        }

        // Уничтожаем ровно те кубики, что выводили на экран (как rolledDice, но без расхождения со списком).
        for (int i = 0; i < allDice.Count; i++)
        {
            Dice dice = allDice[i];
            if (dice == null)
            {
                continue;
            }

            MGenerator gen = dice.GetComponentInParent<MGenerator>();
            GameObject root = gen != null ? gen.GetSlotRootIfContains(dice) : null;
            if (gen != null)
            {
                gen.RemoveDice(dice);
            }

            Object.Destroy(root != null ? root : dice.gameObject);
        }

        unit.ResetModuleDiceToLocalLayout();
    }

    private static bool AllGeneratorsFullyCharged(List<MGenerator> generators)
    {
        for (int i = 0; i < generators.Count; i++)
        {
            MGenerator g = generators[i];
            if (g == null)
            {
                continue;
            }

            if (g.CurrentCharge < g.MaxCharge)
            {
                return false;
            }
        }

        return true;
    }

    private IEnumerator RollAllDiceInParallel(List<Dice> diceList)
    {
        var batch = new ParallelRollBatch();
        for (int i = 0; i < diceList.Count; i++)
        {
            Dice dice = diceList[i];
            if (dice == null)
            {
                continue;
            }

            batch.Remaining++;
            StartCoroutine(ParallelRollOne(dice, batch));
        }

        while (batch.Remaining > 0)
        {
            yield return null;
        }
    }

    private IEnumerator ParallelRollOne(Dice dice, ParallelRollBatch batch)
    {
        yield return StartCoroutine(dice.RollDice());
        batch.Remaining--;
    }

    private sealed class ParallelRollBatch
    {
        public int Remaining;
    }

    private static IEnumerator WaitForLeftClickAnywhere()
    {
        while (!Input.GetMouseButtonDown(0))
        {
            yield return null;
        }
    }
}
