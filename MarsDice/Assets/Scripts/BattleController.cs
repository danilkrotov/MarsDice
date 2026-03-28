using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleController : MonoBehaviour
{
    [SerializeField] private List<GameObject> unitObjects = new List<GameObject>();
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

    /// <summary>Вызывается из <see cref="Unit.TakeDamage(int)"/> после изменения HP — обновляет анонс фазы при исходе боя.</summary>
    public void NotifyHealthChangedAfterDamage()
    {
        if (!turnInProgress || battleConcluded)
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

        if (unitObjects != null)
        {
            for (int i = 0; i < unitObjects.Count; i++)
            {
                GameObject go = unitObjects[i];
                if (go == null)
                {
                    continue;
                }

                Unit u = go.GetComponent<Unit>();
                if (u == null || u.CurrentHealth <= 0)
                {
                    continue;
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
        }

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

    public IReadOnlyList<GameObject> UnitObjects => unitObjects;
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

        if (unitObjects == null || unitObjects.Count == 0)
        {
            Debug.LogWarning("Заполни список unitObjects в BattleController.");
            yield break;
        }

        turnInProgress = true;
        battleConcluded = false;

        while (!battleConcluded)
        {
            bool anyUnitProcessedThisRound = false;

            for (int unitIndex = 0; unitIndex < unitObjects.Count; unitIndex++)
            {
                if (battleConcluded)
                {
                    break;
                }

                GameObject unitObject = unitObjects[unitIndex];
                if (unitObject == null)
                {
                    Debug.LogWarning($"Элемент #{unitIndex + 1} в unitObjects пустой.");
                    continue;
                }

                Unit unit = unitObject.GetComponent<Unit>();
                if (unit == null)
                {
                    Debug.LogWarning($"На объекте {unitObject.name} не найден компонент Unit.");
                    continue;
                }

                if (!unit.HasAtLeastOneDiceInModules())
                {
                    Debug.LogWarning($"У Unit на объекте {unitObject.name} нет кубиков в модулях.");
                    continue;
                }

                anyUnitProcessedThisRound = true;

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
                    yield return action.Action(this, unitIndex, unit);
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
