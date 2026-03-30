using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShieldUp : BattleActions
{
    private const string PhaseTitle = "Пополнение щита";

    public override string PhaseName => PhaseTitle;

    public override bool UsesManualAdvanceClick => false;

    private bool _skipPhaseRequested;
    private bool _showSkipUi;
    private int _totalShieldRestored;

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

    private static List<MShield> CollectMShields(Unit unit)
    {
        var list = new List<MShield>(4);
        IReadOnlyList<Modules> modules = unit.Modules;
        for (int i = 0; i < modules.Count; i++)
        {
            if (modules[i] is MShield sh)
            {
                list.Add(sh);
            }
        }

        return list;
    }

    private static void ReplenishShieldDiceOnUnit(Unit unit)
    {
        List<MShield> shields = CollectMShields(unit);
        for (int si = 0; si < shields.Count; si++)
        {
            if (shields[si] != null)
            {
                shields[si].ReplenishConsumedDice();
            }
        }
    }

    private static List<Dice> CollectAllShieldDice(IReadOnlyList<MShield> shields)
    {
        var all = new List<Dice>(8);
        for (int si = 0; si < shields.Count; si++)
        {
            MShield shieldModule = shields[si];
            if (shieldModule == null)
            {
                continue;
            }

            IReadOnlyList<Dice> list = shieldModule.Dices;
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

    private static void RelayoutRemainingShieldDice(BattleController battleController, IReadOnlyList<MShield> shields)
    {
        List<Dice> remaining = CollectAllShieldDice(shields);
        if (remaining.Count > 0)
        {
            battleController.LayoutDiceGroupCenteredOnScreen(remaining);
        }
    }

    private static void DestroyRemainingShieldDiceOnModules(IReadOnlyList<MShield> shields)
    {
        if (shields == null)
        {
            return;
        }

        for (int si = 0; si < shields.Count; si++)
        {
            MShield sh = shields[si];
            if (sh == null)
            {
                continue;
            }

            IReadOnlyList<Dice> list = sh.Dices;
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

                GameObject root = sh.GetSlotRootIfContains(d);
                sh.RemoveDice(d);
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

        if (unit.MaxShield > 0 && unit.CurrentShield >= unit.MaxShield)
        {
            Chat.Push($"Фаза {PhaseName} пропущена т.к. у вас полные щиты");
            yield break;
        }

        ReplenishShieldDiceOnUnit(unit);

        List<MShield> shields = CollectMShields(unit);
        if (!UnitHasAnyShieldDice(shields))
        {
            yield break;
        }

        Chat.Push($"Началась фаза {PhaseName}");

        _totalShieldRestored = 0;
        _skipPhaseRequested = false;
        _showSkipUi = !unit.IsAI;

        try
        {
            List<Dice> allDice = CollectAllShieldDice(shields);
            if (allDice.Count == 0)
            {
                yield break;
            }

            battleController.LayoutDiceGroupCenteredOnScreen(allDice);
            yield return RollAllDiceInParallel(allDice, () => _skipPhaseRequested);

            if (_skipPhaseRequested)
            {
                yield break;
            }

            while (!_skipPhaseRequested && HasAnyShieldDice(shields))
            {
                if (unit.IsAI)
                {
                    if (!TryGetFirstShieldDice(shields, out MShield shieldModule, out Dice autoDice) || autoDice == null)
                    {
                        break;
                    }

                    ApplyShieldDiceChoice(shieldModule, autoDice, unit);
                    RelayoutRemainingShieldDice(battleController, shields);
                }
                else
                {
                    yield return WaitForPlayerAnyShieldDice(unit, battleController, shields);
                    if (_skipPhaseRequested)
                    {
                        break;
                    }

                    RelayoutRemainingShieldDice(battleController, shields);
                }
            }

            if (_totalShieldRestored > 0)
            {
                Chat.Push($"{unit.name} восстановил {_totalShieldRestored} ед щита.");
            }
        }
        finally
        {
            _showSkipUi = false;
            if (_skipPhaseRequested)
            {
                DestroyRemainingShieldDiceOnModules(shields);
            }
        }
    }

    private IEnumerator WaitForPlayerAnyShieldDice(Unit unit, BattleController battleController, IReadOnlyList<MShield> shields)
    {
        while (!_skipPhaseRequested && HasAnyShieldDice(shields))
        {
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 guiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (!GetSkipButtonScreenRect().Contains(guiMouse))
                {
                    if (TryGetClickedUnitShieldDice(unit, shields, out Dice picked, out MShield owner) &&
                        picked != null &&
                        !picked.IsRolling)
                    {
                        ApplyShieldDiceChoice(owner, picked, unit);
                        yield break;
                    }
                }
            }

            yield return null;
        }
    }

    private static bool TryGetFirstShieldDice(IReadOnlyList<MShield> shields, out MShield module, out Dice dice)
    {
        for (int si = 0; si < shields.Count; si++)
        {
            MShield sh = shields[si];
            if (sh == null)
            {
                continue;
            }

            dice = GetFirstNonNullDice(sh);
            if (dice != null)
            {
                module = sh;
                return true;
            }
        }

        module = null;
        dice = null;
        return false;
    }

    private static bool TryGetClickedUnitShieldDice(Unit unit, IReadOnlyList<MShield> shields, out Dice dice, out MShield ownerModule)
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
        MShield bestShield = null;

        for (int h = 0; h < hits.Length; h++)
        {
            Dice d = hits[h].collider.GetComponentInParent<Dice>();
            if (d == null)
            {
                continue;
            }

            for (int si = 0; si < shields.Count; si++)
            {
                MShield sh = shields[si];
                if (sh == null || !IsDiceOnUnitShieldModule(d, sh))
                {
                    continue;
                }

                if (hits[h].distance < best)
                {
                    best = hits[h].distance;
                    bestDice = d;
                    bestShield = sh;
                }
            }
        }

        if (bestDice == null)
        {
            return false;
        }

        dice = bestDice;
        ownerModule = bestShield;
        return true;
    }

    private void ApplyShieldDiceChoice(MShield shieldModule, Dice dice, Unit unit)
    {
        if (dice is ShieldDice shieldDice)
        {
            int cost = shieldDice.GetEnergyCostByFace(dice.LastResult);
            int restore = shieldDice.GetShieldRestoreByFace(dice.LastResult);

            if (TrySpendEnergyFromGenerators(unit, cost))
            {
                if (restore > 0)
                {
                    unit.AddShield(restore);
                    _totalShieldRestored += restore;
                }

                Debug.Log($"{unit.name} / {shieldModule.name}: грань {dice.LastResult}, failed={dice.LastFailed}, −{cost} энергии, +{restore} щита → {unit.CurrentShield}/{unit.MaxShield}.");
            }
            else
            {
                Debug.Log($"{unit.name} / {shieldModule.name}: не хватает энергии ({cost}), кубик всё равно израсходован.");
            }
        }

        GameObject root = shieldModule.GetSlotRootIfContains(dice);
        shieldModule.RemoveDice(dice);
        Object.Destroy(root != null ? root : dice.gameObject);
    }

    private static bool IsDiceOnUnitShieldModule(Dice dice, MShield shieldModule)
    {
        if (dice == null || shieldModule == null)
        {
            return false;
        }

        Transform t = dice.transform;
        while (t != null)
        {
            if (t == shieldModule.transform)
            {
                break;
            }

            t = t.parent;
        }

        if (t == null)
        {
            return false;
        }

        IReadOnlyList<Dice> list = shieldModule.Dices;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == dice)
            {
                return true;
            }
        }

        return false;
    }

    private static bool UnitHasAnyShieldDice(IReadOnlyList<MShield> shields)
    {
        for (int si = 0; si < shields.Count; si++)
        {
            if (shields[si] != null && GetNonNullDiceCount(shields[si]) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnyShieldDice(IReadOnlyList<MShield> shields)
    {
        return UnitHasAnyShieldDice(shields);
    }

    private static int GetNonNullDiceCount(MShield shieldModule)
    {
        int n = 0;
        IReadOnlyList<Dice> list = shieldModule.Dices;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
            {
                n++;
            }
        }

        return n;
    }

    private static Dice GetFirstNonNullDice(MShield shieldModule)
    {
        IReadOnlyList<Dice> list = shieldModule.Dices;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
            {
                return list[i];
            }
        }

        return null;
    }
}
