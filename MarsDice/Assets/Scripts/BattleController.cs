using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleController : MonoBehaviour
{
    [SerializeField] private List<GameObject> unitObjects = new List<GameObject>();
    [SerializeField] private Vector3 diceSpawnOffset = new Vector3(0f, 1.2f, 0f);
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

    private IEnumerator PlayTurnSequence()
    {
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
        ClearAllSpawnedDice();

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

            if (!unit.HasAtLeastOneDicePrefab())
            {
                Debug.LogWarning($"У Unit на объекте {unitObject.name} не назначены префабы кубиков.");
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
            }
        }

        currentPhaseName = "-";
        turnInProgress = false;
    }

    private void ClearAllSpawnedDice()
    {
        for (int i = 0; i < unitObjects.Count; i++)
        {
            if (unitObjects[i] == null)
            {
                continue;
            }

            Unit unit = unitObjects[i].GetComponent<Unit>();
            if (unit != null)
            {
                unit.ClearSpawnedDices();
            }
        }
    }
}
