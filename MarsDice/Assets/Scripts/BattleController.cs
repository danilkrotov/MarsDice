using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleController : MonoBehaviour
{
    [SerializeField] private List<GameObject> unitObjects = new List<GameObject>();
    [SerializeField] private Vector3 diceSpawnOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Раскладка кубиков по центру экрана (как текст по центру)")]
    [SerializeField] private float diceViewDistanceFromCamera = 8f;
    [SerializeField] private float diceHorizontalSpacing = 1.25f;

    [SerializeField] private List<BattleActions> battleActions = new List<BattleActions>();
    private string currentPhaseName = "-";
    private bool turnInProgress;

    private void Start()
    {
        StartCoroutine(PlayTurnSequence());
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(20f, 20f, 400f, 30f), $"Фаза: {currentPhaseName}");
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
        // Один кадр — все Awake/Start успевают (модули, кубики на MGenerator, StartScript).
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

        for (int unitIndex = 0; unitIndex < unitObjects.Count; unitIndex++)
        {
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

            for (int actionIndex = 0; actionIndex < battleActions.Count; actionIndex++)
            {
                BattleActions action = battleActions[actionIndex];
                if (action == null)
                {
                    continue;
                }

                currentPhaseName = string.IsNullOrWhiteSpace(action.PhaseName) ? "-" : action.PhaseName;
                yield return action.Action(this, unitIndex, unit);
                if (!unit.IsAI && action.UsesManualAdvanceClick)
                {
                    yield return WaitForLeftClickAnywhere();
                }

                unit.ResetModuleDiceToLocalLayout();
            }

            unit.ResetModuleDiceToLocalLayout();
        }

        currentPhaseName = "-";
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
