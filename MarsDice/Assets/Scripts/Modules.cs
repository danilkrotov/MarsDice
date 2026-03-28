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

    public ModuleType Type => type;
    public ModuleSize Size => size;

    public int DiceCount => diceSlots;
    public int MinDiceCount => minDiceSlots;
    public int MaxDiceCount => maxDiceSlots;

    public IReadOnlyList<Dice> Dices => new DiceResolvedList(diceObjects);

    public bool CanAddDice => GetAssignedDiceCount() < diceSlots;

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
                return true;
            }
        }

        diceObjects.Add(diceGo);
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

        return diceObjects.Remove(dice.gameObject);
    }

    public void ClearDices()
    {
        diceObjects.Clear();
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
