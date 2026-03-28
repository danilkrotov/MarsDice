using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageDeal : BattleActions
{
    private const string PhaseTitle = "Нанесение урона";

    public override string PhaseName => PhaseTitle;

    public override bool UsesManualAdvanceClick => false;

    private bool _skipPhaseRequested;
    private bool _showSkipUi;

    private void OnGUI()
    {
        if (!_showSkipUi)
        {
            return;
        }

        Rect r = GetSkipButtonScreenRect();
        if (GUI.Button(r, "Пропустить"))
        {
            _skipPhaseRequested = true;
        }
    }

    private static Rect GetSkipButtonScreenRect()
    {
        const float w = 160f;
        const float h = 40f;
        return new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height - 95f, w, h);
    }

    private static List<MTurret> CollectMTurrets(Unit unit)
    {
        var list = new List<MTurret>(4);
        IReadOnlyList<Modules> modules = unit.Modules;
        for (int i = 0; i < modules.Count; i++)
        {
            if (modules[i] is MTurret t)
            {
                list.Add(t);
            }
        }

        return list;
    }

    private static void ReplenishTurretDiceOnUnit(Unit unit)
    {
        List<MTurret> turrets = CollectMTurrets(unit);
        for (int ti = 0; ti < turrets.Count; ti++)
        {
            if (turrets[ti] != null)
            {
                turrets[ti].ReplenishConsumedDice();
            }
        }
    }

    private static List<Dice> CollectAllTurretDice(IReadOnlyList<MTurret> turrets)
    {
        var all = new List<Dice>(8);
        for (int ti = 0; ti < turrets.Count; ti++)
        {
            MTurret tur = turrets[ti];
            if (tur == null)
            {
                continue;
            }

            IReadOnlyList<Dice> list = tur.Dices;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    all.Add(list[i]);
                }
            }
        }

        return all;
    }

    private static void RelayoutRemainingTurretDice(BattleController battleController, IReadOnlyList<MTurret> turrets)
    {
        List<Dice> remaining = CollectAllTurretDice(turrets);
        if (remaining.Count > 0)
        {
            battleController.LayoutDiceGroupCenteredOnScreen(remaining);
        }
    }

    private static void DestroyRemainingTurretDiceOnModules(IReadOnlyList<MTurret> turrets)
    {
        if (turrets == null)
        {
            return;
        }

        for (int ti = 0; ti < turrets.Count; ti++)
        {
            MTurret tur = turrets[ti];
            if (tur == null)
            {
                continue;
            }

            IReadOnlyList<Dice> list = tur.Dices;
            var snapshot = new List<Dice>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                Dice d = list[i];
                if (d != null)
                {
                    snapshot.Add(d);
                }
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                Dice d = snapshot[i];
                if (d == null)
                {
                    continue;
                }

                GameObject root = tur.GetSlotRootIfContains(d);
                tur.RemoveDice(d);
                Object.Destroy(root != null ? root : d.gameObject);
            }
        }
    }

    public override IEnumerator Action(BattleController battleController, int unitIndex, Unit unit)
    {
        if (battleController == null || unit == null)
        {
            yield break;
        }

        ReplenishTurretDiceOnUnit(unit);

        List<MTurret> turrets = CollectMTurrets(unit);
        if (!UnitHasAnyTurretDice(turrets))
        {
            yield break;
        }

        _skipPhaseRequested = false;
        _showSkipUi = !unit.IsAI;

        try
        {
            List<Dice> allDice = CollectAllTurretDice(turrets);
            if (allDice.Count == 0)
            {
                yield break;
            }

            battleController.LayoutDiceGroupCenteredOnScreen(allDice);
            yield return RollAllDiceInParallel(allDice);

            if (_skipPhaseRequested)
            {
                yield break;
            }

            while (!_skipPhaseRequested && HasAnyTurretDice(turrets))
            {
                if (unit.IsAI)
                {
                    if (!TryGetFirstTurretDice(turrets, out MTurret turretModule, out Dice autoDice) || autoDice == null)
                    {
                        break;
                    }

                    ApplyTurretDiceChoice(turretModule, autoDice, unit, unitIndex, battleController);
                    RelayoutRemainingTurretDice(battleController, turrets);
                }
                else
                {
                    yield return WaitForPlayerAnyTurretDice(unit, battleController, turrets, unitIndex);
                    if (_skipPhaseRequested)
                    {
                        break;
                    }

                    RelayoutRemainingTurretDice(battleController, turrets);
                }
            }
        }
        finally
        {
            _showSkipUi = false;
            if (_skipPhaseRequested)
            {
                DestroyRemainingTurretDiceOnModules(turrets);
            }
        }
    }

    private IEnumerator WaitForPlayerAnyTurretDice(
        Unit unit,
        BattleController battleController,
        IReadOnlyList<MTurret> turrets,
        int unitIndex)
    {
        while (!_skipPhaseRequested && HasAnyTurretDice(turrets))
        {
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 guiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (!GetSkipButtonScreenRect().Contains(guiMouse))
                {
                    if (TryGetClickedUnitTurretDice(unit, turrets, out Dice picked, out MTurret owner) &&
                        picked != null &&
                        !picked.IsRolling)
                    {
                        ApplyTurretDiceChoice(owner, picked, unit, unitIndex, battleController);
                        yield break;
                    }
                }
            }

            yield return null;
        }
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
            if (_skipPhaseRequested)
            {
                yield break;
            }

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

    private void ApplyTurretDiceChoice(MTurret turretModule, Dice dice, Unit unit, int unitIndex, BattleController battleController)
    {
        int cost = turretModule.EnergyCostPerShot;
        int damage = turretModule.BaseDamagePerShot;

        if (!dice.LastFailed)
        {
            if (TrySpendEnergyFromGenerators(unit, cost))
            {
                Unit target = GetNextTargetUnit(unitIndex, battleController.UnitObjects);
                if (target != null && damage > 0)
                {
                    target.TakeDamage(damage);
                }

                Debug.Log($"{unit.name} / {turretModule.name}: грань {dice.LastResult}, failed={dice.LastFailed}, −{cost} энергии, урон по цели {damage}.");
            }
            else
            {
                Debug.Log($"{unit.name} / {turretModule.name}: грань {dice.LastResult}, не хватает энергии ({cost}) для выстрела.");
            }
        }
        else
        {
            Debug.Log($"{unit.name} / {turretModule.name}: грань {dice.LastResult}, провал — урон и энергия не тратятся.");
        }

        GameObject root = turretModule.GetSlotRootIfContains(dice);
        turretModule.RemoveDice(dice);
        Object.Destroy(root != null ? root : dice.gameObject);
    }

    private static bool TryGetFirstTurretDice(IReadOnlyList<MTurret> turrets, out MTurret module, out Dice dice)
    {
        for (int ti = 0; ti < turrets.Count; ti++)
        {
            MTurret tur = turrets[ti];
            if (tur == null)
            {
                continue;
            }

            dice = GetFirstNonNullDice(tur);
            if (dice != null)
            {
                module = tur;
                return true;
            }
        }

        module = null;
        dice = null;
        return false;
    }

    private static bool TryGetClickedUnitTurretDice(
        Unit unit,
        IReadOnlyList<MTurret> turrets,
        out Dice dice,
        out MTurret ownerModule)
    {
        dice = null;
        ownerModule = null;
        Camera cam = Camera.main;
        if (cam == null)
        {
            return false;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 120f);
        float best = float.MaxValue;
        Dice bestDice = null;
        MTurret bestTurret = null;

        for (int h = 0; h < hits.Length; h++)
        {
            Dice d = hits[h].collider.GetComponentInParent<Dice>();
            if (d == null)
            {
                continue;
            }

            for (int ti = 0; ti < turrets.Count; ti++)
            {
                MTurret tur = turrets[ti];
                if (tur == null || !IsDiceOnUnitTurretModule(d, unit, tur))
                {
                    continue;
                }

                if (hits[h].distance < best)
                {
                    best = hits[h].distance;
                    bestDice = d;
                    bestTurret = tur;
                }
            }
        }

        if (bestDice == null)
        {
            return false;
        }

        dice = bestDice;
        ownerModule = bestTurret;
        return true;
    }

    private static bool IsDiceOnUnitTurretModule(Dice dice, Unit unit, MTurret turretModule)
    {
        if (dice == null || turretModule == null)
        {
            return false;
        }

        Transform t = dice.transform;
        while (t != null)
        {
            if (t == turretModule.transform)
            {
                break;
            }

            t = t.parent;
        }

        if (t == null)
        {
            return false;
        }

        IReadOnlyList<Dice> list = turretModule.Dices;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == dice)
            {
                return true;
            }
        }

        return false;
    }

    private static bool UnitHasAnyTurretDice(IReadOnlyList<MTurret> turrets)
    {
        for (int ti = 0; ti < turrets.Count; ti++)
        {
            if (turrets[ti] != null && GetNonNullDiceCount(turrets[ti]) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnyTurretDice(IReadOnlyList<MTurret> turrets)
    {
        return UnitHasAnyTurretDice(turrets);
    }

    private static int GetNonNullDiceCount(MTurret turretModule)
    {
        int n = 0;
        IReadOnlyList<Dice> list = turretModule.Dices;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
            {
                n++;
            }
        }

        return n;
    }

    private static Dice GetFirstNonNullDice(MTurret turretModule)
    {
        IReadOnlyList<Dice> list = turretModule.Dices;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
            {
                return list[i];
            }
        }

        return null;
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
