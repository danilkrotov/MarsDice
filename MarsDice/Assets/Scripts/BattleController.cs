using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleController : MonoBehaviour
{
    [Header("Команды")]
    [Tooltip("Порядок хода в раунде: сначала все слоты первой команды сверху вниз, затем вторая команда. Юниты не бьют свою команду.")]
    [SerializeField] private List<GameObject> firstTeam = new List<GameObject>();

    [SerializeField] private List<GameObject> secondTeam = new List<GameObject>();

    [SerializeField] private Vector3 diceSpawnOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Раскладка кубиков по центру экрана (как текст по центру)")]
    [SerializeField] private float diceViewDistanceFromCamera = 8f;
    [SerializeField] private float diceHorizontalSpacing = 1.65f;

    [SerializeField] private List<BattleActions> battleActions = new List<BattleActions>();
    private string currentPhaseName = "-";
    private bool turnInProgress;
    private bool battleConcluded;

    private void Start()
    {
        StartCoroutine(PlayTurnSequence());
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(20f, 20f, 520f, 40f), $"Фаза: {currentPhaseName}");
    }

    /// <summary>Вызывается из <see cref="Unit.TakeDamage(int)"/> после изменения HP — убирает погибшего с поля, затем анонс исхода боя.</summary>
    public void NotifyHealthChangedAfterDamage(Unit damagedUnit)
    {
        if (!turnInProgress)
        {
            return;
        }

        if (damagedUnit != null && damagedUnit.CurrentHealth <= 0)
        {
            RemoveUnitFromTeams(damagedUnit);
            Destroy(damagedUnit.gameObject);
        }

        if (battleConcluded)
        {
            return;
        }

        string outcome = ComputeBattleOutcomeMessage();
        if (outcome != null)
        {
            currentPhaseName = outcome;
            battleConcluded = true;
        }
    }

    /// <returns>Сообщение для UI, если бой завершён; иначе null.</returns>
    private string ComputeBattleOutcomeMessage()
    {
        bool playerAlive = false;
        bool aiAlive = false;

        void ConsiderAliveSide(Unit u)
        {
            if (u == null || u.CurrentHealth <= 0)
            {
                return;
            }

            if (u.IsAI)
            {
                aiAlive = true;
            }
            else
            {
                playerAlive = true;
            }
        }

        ScanTeamForOutcome(firstTeam, ConsiderAliveSide);
        ScanTeamForOutcome(secondTeam, ConsiderAliveSide);

        if (playerAlive && !aiAlive)
        {
            return "Победа игрока";
        }

        if (!playerAlive && aiAlive)
        {
            return "Победа компьютера";
        }

        if (!playerAlive && !aiAlive)
        {
            return "Ничья";
        }

        return null;
    }

    private void RemoveUnitFromTeams(Unit unit)
    {
        RemoveUnitFromTeamList(firstTeam, unit);
        RemoveUnitFromTeamList(secondTeam, unit);
    }

    private static void RemoveUnitFromTeamList(List<GameObject> team, Unit unit)
    {
        if (team == null || unit == null)
        {
            return;
        }

        Transform ut = unit.transform;
        team.RemoveAll(go =>
        {
            if (go == null)
            {
                return false;
            }

            return go == unit.gameObject || ut.IsChildOf(go.transform);
        });
    }

    /// <summary>Индекс корня в актуальном порядке хода (после удалений из команд в раунде).</summary>
    private int ResolveUnitIndexInCurrentTurnOrder(GameObject unitRoot)
    {
        if (unitRoot == null)
        {
            return 0;
        }

        List<GameObject> order = GetUnitRootsInTurnOrder();
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i] == unitRoot)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>Все корни юнитов в порядке хода: <see cref="firstTeam"/>, затем <see cref="secondTeam"/> (null пропускаются).</summary>
    public List<GameObject> GetUnitRootsInTurnOrder()
    {
        int cap = (firstTeam?.Count ?? 0) + (secondTeam?.Count ?? 0);
        var list = new List<GameObject>(cap);
        AppendTeamRoots(firstTeam, list);
        AppendTeamRoots(secondTeam, list);
        return list;
    }

    private static void AppendTeamRoots(IReadOnlyList<GameObject> team, List<GameObject> into)
    {
        if (team == null || into == null)
        {
            return;
        }

        for (int i = 0; i < team.Count; i++)
        {
            if (team[i] != null)
            {
                into.Add(team[i]);
            }
        }
    }

    private static void ScanTeamForOutcome(IReadOnlyList<GameObject> team, System.Action<Unit> considerAliveSide)
    {
        if (team == null || considerAliveSide == null)
        {
            return;
        }

        for (int i = 0; i < team.Count; i++)
        {
            GameObject go = team[i];
            if (go == null)
            {
                continue;
            }

            Unit u = go.GetComponent<Unit>();
            if (u == null)
            {
                u = go.GetComponentInChildren<Unit>(true);
            }

            considerAliveSide(u);
        }
    }

    /// <summary>0 — первая команда, 1 — вторая, −1 если юнит ни в одном списке (в фазе урона не будет целей).</summary>
    public int GetTeamIndex(Unit unit)
    {
        if (unit == null)
        {
            return -1;
        }

        if (TeamListContainsUnit(firstTeam, unit))
        {
            return 0;
        }

        if (TeamListContainsUnit(secondTeam, unit))
        {
            return 1;
        }

        return -1;
    }

    /// <summary>Живые юниты на противоположной команде. Пустой список — атакующий не в списках команд или нет живых противников.</summary>
    public List<Unit> GetAliveEnemyUnitsForAttacker(Unit attacker)
    {
        if (attacker == null)
        {
            return new List<Unit>();
        }

        int team = GetTeamIndex(attacker);
        if (team < 0)
        {
            return new List<Unit>();
        }

        IReadOnlyList<GameObject> enemyRoots = team == 0 ? secondTeam : firstTeam;
        return CollectAliveUnitsFromTeamRoots(enemyRoots);
    }

    private static bool TeamListContainsUnit(IReadOnlyList<GameObject> team, Unit unit)
    {
        if (team == null || unit == null)
        {
            return false;
        }

        Transform ut = unit.transform;
        for (int i = 0; i < team.Count; i++)
        {
            GameObject go = team[i];
            if (go == null)
            {
                continue;
            }

            if (go == unit.gameObject)
            {
                return true;
            }

            if (ut.IsChildOf(go.transform))
            {
                return true;
            }
        }

        return false;
    }

    private static List<Unit> CollectAliveUnitsFromTeamRoots(IReadOnlyList<GameObject> roots)
    {
        var list = new List<Unit>(4);
        if (roots == null)
        {
            return list;
        }

        for (int i = 0; i < roots.Count; i++)
        {
            GameObject go = roots[i];
            if (go == null)
            {
                continue;
            }

            Unit u = go.GetComponent<Unit>();
            if (u == null)
            {
                u = go.GetComponentInChildren<Unit>(true);
            }

            if (u != null && u.CurrentHealth > 0)
            {
                list.Add(u);
            }
        }

        return list;
    }

    public Vector3 DiceSpawnOffset => diceSpawnOffset;
    public float DiceViewDistanceFromCamera => diceViewDistanceFromCamera;
    public float DiceHorizontalSpacing => diceHorizontalSpacing;

    public void LayoutModuleDice(Modules module)
    {
        if (module == null)
        {
            return;
        }

        DiceScreenLayout.LayoutDiceCenteredOnScreen(
            Camera.main,
            diceViewDistanceFromCamera,
            diceHorizontalSpacing,
            module.Dices);
    }

    /// <summary>Одна группа кубиков по центру экрана (как строка текста по центру), вдоль camera.right.</summary>
    public void LayoutDiceGroupCenteredOnScreen(IReadOnlyList<Dice> dices)
    {
        DiceScreenLayout.LayoutDiceCenteredOnScreen(
            Camera.main,
            diceViewDistanceFromCamera,
            diceHorizontalSpacing,
            dices);
    }

    private IEnumerator PlayTurnSequence()
    {
        // Один кадр — все Awake/Start успевают (модули, юниты).
        yield return null;

        if (turnInProgress)
        {
            yield break;
        }

        turnInProgress = true;
        battleConcluded = false;

        while (!battleConcluded)
        {
            bool anyUnitProcessedThisRound = false;
            List<GameObject> turnOrder = GetUnitRootsInTurnOrder();
            if (turnOrder.Count == 0)
            {
                Debug.LogWarning("BattleController: списки firstTeam / secondTeam пусты — нечего обрабатывать.");
                break;
            }

            for (int unitIndex = 0; unitIndex < turnOrder.Count; unitIndex++)
            {
                if (battleConcluded)
                {
                    break;
                }

                GameObject unitObject = turnOrder[unitIndex];
                if (unitObject == null)
                {
                    continue;
                }

                Unit unit = unitObject.GetComponent<Unit>();
                if (unit == null)
                {
                    Debug.LogWarning($"На объекте {unitObject.name} не найден компонент Unit.");
                    continue;
                }

                if (unit.CurrentHealth <= 0)
                {
                    continue;
                }

                if (!unit.HasAtLeastOneDiceInModules())
                {
                    Debug.LogWarning($"У Unit на объекте {unitObject.name} нет кубиков в модулях.");
                    continue;
                }

                anyUnitProcessedThisRound = true;

                int unitIndexForActions = ResolveUnitIndexInCurrentTurnOrder(unitObject);

                for (int actionIndex = 0; actionIndex < battleActions.Count; actionIndex++)
                {
                    if (battleConcluded)
                    {
                        break;
                    }

                    BattleActions action = battleActions[actionIndex];
                    if (action == null)
                    {
                        continue;
                    }

                    currentPhaseName = string.IsNullOrWhiteSpace(action.PhaseName) ? "-" : action.PhaseName;
                    yield return action.Action(this, unitIndexForActions, unit);
                    if (battleConcluded)
                    {
                        break;
                    }

                    if (!unit.IsAI && action.UsesManualAdvanceClick)
                    {
                        yield return WaitForLeftClickAnywhere();
                    }

                    unit.ResetModuleDiceToLocalLayout();
                }

                unit.ResetModuleDiceToLocalLayout();
            }

            if (!anyUnitProcessedThisRound)
            {
                Debug.LogWarning("BattleController: ни один юнит не прошёл фазы в раунде — цикл остановлен.");
                break;
            }
        }

        if (!battleConcluded)
        {
            currentPhaseName = "-";
        }

        turnInProgress = false;
    }

    private static IEnumerator WaitForLeftClickAnywhere()
    {
        while (!Input.GetMouseButtonDown(0))
        {
            yield return null;
        }
    }

}
