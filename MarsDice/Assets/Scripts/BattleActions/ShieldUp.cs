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

    public override IEnumerator Action(BattleController battleController, int unitIndex, Unit unit)
    {
        if (battleController == null || unit == null)
        {
            yield break;
        }

        if (!UnitHasAnyShieldDice(unit))
        {
            yield break;
        }

        _skipPhaseRequested = false;
        _showSkipUi = !unit.IsAI;

        try
        {
            MShield[] shields = unit.GetComponentsInChildren<MShield>(true);
            for (int si = 0; si < shields.Length; si++)
            {
                MShield shieldModule = shields[si];
                if (shieldModule == null)
                {
                    continue;
                }

                if (GetNonNullDiceCount(shieldModule) == 0)
                {
                    continue;
                }

                battleController.LayoutModuleDice(shieldModule);
                yield return RollAllShieldDiceInModule(shieldModule);
                battleController.LayoutModuleDice(shieldModule);

                if (_skipPhaseRequested)
                {
                    break;
                }

                if (unit.IsAI)
                {
                    while (!_skipPhaseRequested && GetNonNullDiceCount(shieldModule) > 0)
                    {
                        Dice autoDice = GetFirstNonNullDice(shieldModule);
                        if (autoDice == null)
                        {
                            break;
                        }

                        ApplyShieldDiceChoice(shieldModule, autoDice, unit);
                        battleController.LayoutModuleDice(shieldModule);
                    }
                }
                else
                {
                    yield return WaitForPlayerShieldChoice(unit, shieldModule, battleController);
                }

                if (_skipPhaseRequested)
                {
                    break;
                }
            }
        }
        finally
        {
            _showSkipUi = false;
        }
    }

    private IEnumerator WaitForPlayerShieldChoice(Unit unit, MShield shieldModule, BattleController battleController)
    {
        while (!_skipPhaseRequested && GetNonNullDiceCount(shieldModule) > 0)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 guiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (!GetSkipButtonScreenRect().Contains(guiMouse))
                {
                    if (TryGetClickedShieldDice(unit, shieldModule, out Dice picked) && !picked.IsRolling)
                    {
                        ApplyShieldDiceChoice(shieldModule, picked, unit);
                        battleController.LayoutModuleDice(shieldModule);
                    }
                }
            }

            yield return null;
        }
    }

    private IEnumerator RollAllShieldDiceInModule(MShield shieldModule)
    {
        IReadOnlyList<Dice> list = shieldModule.Dices;
        for (int i = 0; i < list.Count; i++)
        {
            if (_skipPhaseRequested)
            {
                yield break;
            }

            Dice dice = list[i];
            if (dice == null)
            {
                continue;
            }

            yield return dice.RollDice();
        }
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
                }

                Debug.Log($"{unit.name} / {shieldModule.name}: грань {dice.LastResult}, failed={dice.LastFailed}, −{cost} энергии, +{restore} щита → {unit.CurrentShield}/{unit.MaxShield}.");
            }
            else
            {
                Debug.Log($"{unit.name} / {shieldModule.name}: не хватает энергии ({cost}), кубик всё равно израсходован.");
            }
        }

        shieldModule.RemoveDice(dice);
        Object.Destroy(dice.gameObject);
    }

    private static bool TryGetClickedShieldDice(Unit unit, MShield shieldModule, out Dice dice)
    {
        dice = null;
        Camera cam = Camera.main;
        if (cam == null)
        {
            return false;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 120f);
        float best = float.MaxValue;
        Dice bestDice = null;
        for (int i = 0; i < hits.Length; i++)
        {
            Dice d = hits[i].collider.GetComponentInParent<Dice>();
            if (d == null)
            {
                continue;
            }

            if (!IsDiceOnUnitShieldModule(d, unit, shieldModule))
            {
                continue;
            }

            if (hits[i].distance < best)
            {
                best = hits[i].distance;
                bestDice = d;
            }
        }

        if (bestDice == null)
        {
            return false;
        }

        dice = bestDice;
        return true;
    }

    private static bool IsDiceOnUnitShieldModule(Dice dice, Unit unit, MShield shieldModule)
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

    private static bool UnitHasAnyShieldDice(Unit unit)
    {
        MShield[] shields = unit.GetComponentsInChildren<MShield>(true);
        for (int si = 0; si < shields.Length; si++)
        {
            if (shields[si] != null && GetNonNullDiceCount(shields[si]) > 0)
            {
                return true;
            }
        }

        return false;
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
