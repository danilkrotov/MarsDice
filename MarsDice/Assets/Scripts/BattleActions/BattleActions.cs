using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BattleActions : MonoBehaviour
{
    public abstract string PhaseName { get; }

    /// <summary>
    /// Если false — после фазы BattleController не ждёт ЛКМ (фаза сама управляет переходом).
    /// </summary>
    public virtual bool UsesManualAdvanceClick => true;

    public abstract IEnumerator Action(BattleController battleController, int unitIndex, Unit unit);

    // ─── Общие утилиты для всех фаз ────────────────────────────────────────────

    /// <summary>
    /// Кидает все кубики параллельно. Необязательный <paramref name="shouldStop"/> опрашивается
    /// каждый кадр: если вернул true — выходим досрочно (кубики продолжают крутиться фоном,
    /// вызывающая фаза сама уничтожает их в finally).
    /// </summary>
    protected IEnumerator RollAllDiceInParallel(List<Dice> diceList, System.Func<bool> shouldStop = null)
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
            StartCoroutine(RollOne(dice, batch));
        }

        while (batch.Remaining > 0)
        {
            if (shouldStop != null && shouldStop())
            {
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator RollOne(Dice dice, ParallelRollBatch batch)
    {
        yield return StartCoroutine(dice.RollDice());
        batch.Remaining--;
    }

    protected sealed class ParallelRollBatch
    {
        public int Remaining;
    }

    protected static IEnumerator WaitForLeftClickAnywhere()
    {
        while (!Input.GetMouseButtonDown(0))
        {
            yield return null;
        }
    }

    /// <summary>
    /// Списывает <paramref name="amount"/> энергии с генераторов юнита (поровну по генераторам).
    /// Возвращает true, если заряда хватило; false — нехватка, заряд не тронут.
    /// </summary>
    protected static bool TrySpendEnergyFromGenerators(Unit unit, int amount)
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
