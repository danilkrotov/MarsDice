using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class Modules : MonoBehaviour
{
    public enum ModuleType
    {
        Generator,
        Shield
    }

    public enum ModuleSize
    {
        I,
        S,
        M,
        L
    }

    [Header("Module")]
    [SerializeField] protected ModuleType type = ModuleType.Generator;
    [SerializeField] protected ModuleSize size = ModuleSize.I;

    [Header("Dice amount (min/max)")]
    [FormerlySerializedAs("minDiceSlots")]
    [Min(1)]
    [SerializeField] private int minDiceSlots = 1;
    [FormerlySerializedAs("maxDiceSlots")]
    [Min(1)]
    [SerializeField] private int maxDiceSlots = 1;

    [FormerlySerializedAs("diceSlots")]
    [Min(1)]
    [SerializeField] private int diceSlots = 1;

    [SerializeField] private List<GameObject> diceObjects = new List<GameObject>();

    /// <summary>Источники для повторного Instantiate после Remove+Destroy (совпадает по индексу с diceObjects).</summary>
    [System.NonSerialized]
    private List<GameObject> dicePrefabTemplates;

    public ModuleType Type => type;
    public ModuleSize Size => size;

    public int DiceCount => diceSlots;
    public int MinDiceCount => minDiceSlots;
    public int MaxDiceCount => maxDiceSlots;

    public IReadOnlyList<Dice> Dices => new DiceResolvedList(diceObjects);

    public bool CanAddDice => GetAssignedDiceCount() < diceSlots;

    protected virtual void Awake()
    {
        CacheDiceTemplatesAndClearRuntimeSlots();
    }

    /// <summary>Есть ли настроенные слоты кубиков (шаблон или уже заспавненный экземпляр) — для пропуска юнита в бою.</summary>
    public bool HasConfiguredDiceForBattle()
    {
        if (GetAssignedDiceCount() > 0)
        {
            return true;
        }

        if (dicePrefabTemplates == null)
        {
            return false;
        }

        for (int i = 0; i < dicePrefabTemplates.Count; i++)
        {
            if (dicePrefabTemplates[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Заполняет пустые слоты копиями по шаблонам — вызывай перед раскладкой на экран (бросок).</summary>
    public void ReplenishConsumedDice()
    {
        if (diceObjects == null || dicePrefabTemplates == null)
        {
            return;
        }

        int n = Mathf.Min(diceObjects.Count, dicePrefabTemplates.Count);
        for (int i = 0; i < n; i++)
        {
            if (diceObjects[i] != null)
            {
                continue;
            }

            GameObject template = dicePrefabTemplates[i];
            if (template == null)
            {
                continue;
            }

            diceObjects[i] = Instantiate(template, transform, false);
        }
    }

    public virtual bool TryAddDice(Dice dice)
    {
        if (dice == null)
        {
            return false;
        }

        if (GetAssignedDiceCount() >= diceSlots)
        {
            return false;
        }

        GameObject diceGo = dice.gameObject;
        if (DiceObjectIndex(diceGo) >= 0)
        {
            return false;
        }

        for (int i = 0; i < diceObjects.Count; i++)
        {
            if (diceObjects[i] == null)
            {
                diceObjects[i] = diceGo;
                EnsureDiceTemplateListSize();
                dicePrefabTemplates[i] = diceGo;
                return true;
            }
        }

        diceObjects.Add(diceGo);
        EnsureDiceTemplateListSize();
        dicePrefabTemplates[diceObjects.Count - 1] = diceGo;
        return true;
    }

    /// <summary>
    /// Сбрасывает ссылки на объекты без нужного компонента кубика (для жёсткой привязки типа в наследниках).
    /// </summary>
    protected void EnforceDiceType<TDice>() where TDice : Dice
    {
        if (diceObjects == null)
        {
            return;
        }

        for (int i = 0; i < diceObjects.Count; i++)
        {
            GameObject go = diceObjects[i];
            if (go == null)
            {
                continue;
            }

            TDice typed = go.GetComponentInChildren<TDice>(true);
            if (typed == null)
            {
                Debug.LogWarning($"{name}: на объекте «{go.name}» в слоте {i} допустим только {typeof(TDice).Name}, ссылка сброшена.");
                diceObjects[i] = null;
            }
        }
    }

    public bool RemoveDice(Dice dice)
    {
        if (dice == null)
        {
            return false;
        }

        int idx = DiceObjectIndex(dice.gameObject);
        if (idx < 0)
        {
            return false;
        }

        diceObjects[idx] = null;
        return true;
    }

    public void ClearDices()
    {
        if (diceObjects == null)
        {
            return;
        }

        for (int i = 0; i < diceObjects.Count; i++)
        {
            diceObjects[i] = null;
        }
    }

    [Header("Сброс кубиков с экрана после хода юнита")]
    [SerializeField] protected float resetDiceHorizontalStep = 0.55f;

    /// <summary>
    /// Возвращает кубики под трансформ модуля (убирает с центра экрана после раскладки DiceScreenLayout).
    /// </summary>
    public virtual void ResetDiceToModuleLocalLayout()
    {
        if (diceObjects == null)
        {
            return;
        }

        int slot = 0;
        for (int i = 0; i < diceObjects.Count; i++)
        {
            GameObject go = diceObjects[i];
            if (go == null)
            {
                continue;
            }

            Dice dice = go.GetComponentInChildren<Dice>(true);
            if (dice == null)
            {
                continue;
            }

            Transform t = dice.transform;
            t.SetParent(transform, false);
            t.localPosition = new Vector3(slot * resetDiceHorizontalStep, 0f, 0f);
            t.localRotation = Quaternion.identity;
            slot++;
        }
    }

    protected virtual void OnValidate()
    {
        if (minDiceSlots < 1)
        {
            minDiceSlots = 1;
        }

        if (maxDiceSlots < minDiceSlots)
        {
            maxDiceSlots = minDiceSlots;
        }

        if (diceSlots < minDiceSlots)
        {
            diceSlots = minDiceSlots;
        }
        else if (diceSlots > maxDiceSlots)
        {
            diceSlots = maxDiceSlots;
        }

        if (diceObjects == null)
        {
            diceObjects = new List<GameObject>();
            return;
        }

        if (diceObjects.Count > diceSlots)
        {
            diceObjects.RemoveRange(diceSlots, diceObjects.Count - diceSlots);
        }
    }

    private int GetAssignedDiceCount()
    {
        int assigned = 0;
        for (int i = 0; i < diceObjects.Count; i++)
        {
            if (diceObjects[i] != null)
            {
                assigned++;
            }
        }

        return assigned;
    }

    private int DiceObjectIndex(GameObject diceGo)
    {
        if (diceGo == null)
        {
            return -1;
        }

        for (int i = 0; i < diceObjects.Count; i++)
        {
            if (diceObjects[i] == diceGo)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Сохраняет ссылки из инспектора как шаблоны; экземпляры не создаём до <see cref="ReplenishConsumedDice"/>.</summary>
    private void CacheDiceTemplatesAndClearRuntimeSlots()
    {
        if (diceObjects == null)
        {
            return;
        }

        dicePrefabTemplates = new List<GameObject>(diceObjects.Count);
        for (int i = 0; i < diceObjects.Count; i++)
        {
            GameObject src = diceObjects[i];
            if (src == null)
            {
                dicePrefabTemplates.Add(null);
                continue;
            }

            if (transform.IsChildOf(src.transform))
            {
                Debug.LogWarning($"{name}: кубик «{src.name}» в слоте {i} выше модуля в иерархии — слот очищен.");
                dicePrefabTemplates.Add(null);
                diceObjects[i] = null;
                continue;
            }

            dicePrefabTemplates.Add(src);
            diceObjects[i] = null;
        }
    }

    private void EnsureDiceTemplateListSize()
    {
        if (dicePrefabTemplates == null)
        {
            dicePrefabTemplates = new List<GameObject>();
        }

        while (dicePrefabTemplates.Count < diceObjects.Count)
        {
            dicePrefabTemplates.Add(null);
        }
    }

    private sealed class DiceResolvedList : IReadOnlyList<Dice>
    {
        private readonly List<GameObject> _gameObjects;

        public DiceResolvedList(List<GameObject> gameObjects)
        {
            _gameObjects = gameObjects;
        }

        public int Count => _gameObjects?.Count ?? 0;

        public Dice this[int index]
        {
            get
            {
                if (_gameObjects == null || index < 0 || index >= _gameObjects.Count)
                {
                    return null;
                }

                GameObject go = _gameObjects[index];
                return go != null ? go.GetComponentInChildren<Dice>(true) : null;
            }
        }

        public IEnumerator<Dice> GetEnumerator()
        {
            int n = Count;
            for (int i = 0; i < n; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
