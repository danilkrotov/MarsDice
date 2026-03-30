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
    private Unit _playerPickedAttackTarget;

    [Header("Подсветка цели (как наведение на грань кубика)")]
    [SerializeField] private Color targetHoverHighlightColor = new Color(1f, 0.92f, 0.45f, 1f);
    [SerializeField] [Range(0f, 1f)] private float targetHoverBlend = 0.4f;
    [SerializeField] private float targetPickRaycastDistance = 120f;

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

        List<MTurret> turrets = CollectMTurrets(unit);
        if (!UnitHasAnyTurretWithConfiguredDice(turrets))
        {
            yield break;
        }

        List<Unit> enemies = battleController.GetAliveEnemyUnitsForAttacker(unit);

        Unit attackTarget;
        if (unit.IsAI)
        {
            attackTarget = GetNextAliveTargetUnit(battleController, unitIndex, unit);
        }
        else
        {
            if (enemies.Count >= 2)
            {
                _playerPickedAttackTarget = null;
                Chat.Push("Пожалуйста выбирите цель для атаки");
                yield return WaitForPlayerPickAttackTarget(enemies);
                attackTarget = _playerPickedAttackTarget;
            }
            else if (enemies.Count == 1)
            {
                attackTarget = enemies[0];
            }
            else
            {
                attackTarget = null;
            }
        }

        if (attackTarget == null)
        {
            yield break;
        }

        Chat.Push($"Началась фаза {PhaseName}");

        ReplenishTurretDiceOnUnit(unit);

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

                    ApplyTurretDiceChoice(turretModule, autoDice, unit, attackTarget, battleController);
                    RelayoutRemainingTurretDice(battleController, turrets);
                }
                else
                {
                    yield return WaitForPlayerAnyTurretDice(unit, battleController, turrets, attackTarget);
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

    private IEnumerator WaitForPlayerPickAttackTarget(List<Unit> candidates)
    {
        List<RendererTintCache> caches = BuildTargetRendererCaches(candidates);
        try
        {
            while (true)
            {
                Unit hovered = GetHoveredCandidateUnit(candidates);
                ApplyTargetHighlights(caches, hovered);

                if (Input.GetMouseButtonDown(0))
                {
                    if (TryGetClickedCandidateUnit(candidates, out Unit picked) && picked != null)
                    {
                        _playerPickedAttackTarget = picked;
                        yield break;
                    }
                }

                yield return null;
            }
        }
        finally
        {
            RestoreTargetRendererTints(caches);
        }
    }

    private IEnumerator WaitForPlayerAnyTurretDice(
        Unit unit,
        BattleController battleController,
        IReadOnlyList<MTurret> turrets,
        Unit attackTarget)
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
                        ApplyTurretDiceChoice(owner, picked, unit, attackTarget, battleController);
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

    private void ApplyTurretDiceChoice(MTurret turretModule, Dice dice, Unit unit, Unit attackTarget, BattleController battleController)
    {
        int cost = turretModule.EnergyCostPerShot;
        int damage = turretModule.BaseDamagePerShot;

        if (!dice.LastFailed)
        {
            if (TrySpendEnergyFromGenerators(unit, cost))
            {
                if (attackTarget != null && attackTarget.CurrentHealth > 0 && damage > 0)
                {
                    attackTarget.TakeDamage(damage);
                    Chat.Push($"{unit.name} нанёс {attackTarget.name} {damage} ед урона");
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

    /// <summary>Есть ли турель с настроенными слотами кубиков (шаблоны), ещё до <see cref="ReplenishTurretDiceOnUnit"/>.</summary>
    private static bool UnitHasAnyTurretWithConfiguredDice(IReadOnlyList<MTurret> turrets)
    {
        for (int ti = 0; ti < turrets.Count; ti++)
        {
            MTurret t = turrets[ti];
            if (t != null && t.HasConfiguredDiceForBattle())
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

    private static Unit GetNextAliveTargetUnit(BattleController battleController, int attackerIndex, Unit attacker)
    {
        if (battleController == null || attacker == null)
        {
            return null;
        }

        List<Unit> enemies = battleController.GetAliveEnemyUnitsForAttacker(attacker);
        if (enemies.Count == 0)
        {
            return null;
        }

        var enemySet = new HashSet<Unit>();
        for (int e = 0; e < enemies.Count; e++)
        {
            if (enemies[e] != null)
            {
                enemySet.Add(enemies[e]);
            }
        }

        List<GameObject> objs = battleController.GetUnitRootsInTurnOrder();
        if (objs != null && objs.Count > 0)
        {
            for (int offset = 1; offset <= objs.Count; offset++)
            {
                int targetIndex = (attackerIndex + offset) % objs.Count;
                GameObject targetObject = objs[targetIndex];
                if (targetObject == null || targetObject == attacker.gameObject)
                {
                    continue;
                }

                Unit targetUnit = targetObject.GetComponent<Unit>();
                if (targetUnit != null && targetUnit.CurrentHealth > 0 && enemySet.Contains(targetUnit))
                {
                    return targetUnit;
                }
            }
        }

        return enemies[0];
    }

    private sealed class RendererTintCache
    {
        public Renderer Renderer;
        public Material Material;
        public Color BaseColor;
    }

    private List<RendererTintCache> BuildTargetRendererCaches(List<Unit> candidates)
    {
        var list = new List<RendererTintCache>(16);
        for (int c = 0; c < candidates.Count; c++)
        {
            Unit u = candidates[c];
            if (u == null)
            {
                continue;
            }

            Renderer[] rends = u.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < rends.Length; r++)
            {
                Renderer ren = rends[r];
                if (ren == null)
                {
                    continue;
                }

                Material m = ren.material;
                list.Add(new RendererTintCache
                {
                    Renderer = ren,
                    Material = m,
                    BaseColor = GetMaterialTint(m),
                });
            }
        }

        return list;
    }

    private void RestoreTargetRendererTints(List<RendererTintCache> caches)
    {
        if (caches == null)
        {
            return;
        }

        for (int i = 0; i < caches.Count; i++)
        {
            RendererTintCache e = caches[i];
            if (e?.Material != null)
            {
                SetMaterialTint(e.Material, e.BaseColor);
            }
        }
    }

    private void ApplyTargetHighlights(List<RendererTintCache> caches, Unit hovered)
    {
        if (caches == null)
        {
            return;
        }

        for (int i = 0; i < caches.Count; i++)
        {
            RendererTintCache e = caches[i];
            if (e?.Renderer == null || e.Material == null)
            {
                continue;
            }

            Unit owner = e.Renderer.GetComponentInParent<Unit>();
            bool isHover = hovered != null && owner == hovered;
            Color c = isHover
                ? Color.Lerp(e.BaseColor, targetHoverHighlightColor, targetHoverBlend)
                : e.BaseColor;
            SetMaterialTint(e.Material, c);
        }
    }

    private Unit GetHoveredCandidateUnit(List<Unit> candidates)
    {
        Camera cam = Camera.main;
        if (cam == null || candidates == null || candidates.Count == 0)
        {
            return null;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, targetPickRaycastDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        Unit u = hit.collider.GetComponentInParent<Unit>();
        if (u == null)
        {
            return null;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] == u)
            {
                return u;
            }
        }

        return null;
    }

    private bool TryGetClickedCandidateUnit(List<Unit> candidates, out Unit picked)
    {
        picked = null;
        Camera cam = Camera.main;
        if (cam == null || candidates == null)
        {
            return false;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, targetPickRaycastDistance, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Unit bestUnit = null;

        for (int h = 0; h < hits.Length; h++)
        {
            Unit u = hits[h].collider.GetComponentInParent<Unit>();
            if (u == null)
            {
                continue;
            }

            bool inList = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == u)
                {
                    inList = true;
                    break;
                }
            }

            if (!inList)
            {
                continue;
            }

            if (hits[h].distance < best)
            {
                best = hits[h].distance;
                bestUnit = u;
            }
        }

        if (bestUnit == null)
        {
            return false;
        }

        picked = bestUnit;
        return true;
    }

    private static Color GetMaterialTint(Material mat)
    {
        if (mat == null)
        {
            return Color.white;
        }

        if (mat.HasProperty("_Color"))
        {
            return mat.GetColor("_Color");
        }

        if (mat.HasProperty("_BaseColor"))
        {
            return mat.GetColor("_BaseColor");
        }

        if (mat.HasProperty("_TintColor"))
        {
            return mat.GetColor("_TintColor");
        }

        return Color.white;
    }

    private static void SetMaterialTint(Material mat, Color color)
    {
        if (mat == null)
        {
            return;
        }

        if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }

        if (mat.HasProperty("_TintColor"))
        {
            mat.SetColor("_TintColor", color);
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
