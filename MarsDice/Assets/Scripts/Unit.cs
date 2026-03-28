using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    [Header("Модули юнита")]
    [Min(0)]
    [SerializeField] private int minModules = 1;
    [Min(0)]
    [SerializeField] private int maxModules = 3;
    [Tooltip("Объекты сцены; на каждом ищется компонент Modules (на себе или на дочерних).")]
    [SerializeField] private List<GameObject> moduleObjects = new List<GameObject>();

    [Header("HP")]
    [SerializeField] private int maxHealth = 10;

    [Header("Управление")]
    [Tooltip("Если включено, юнит считается под управлением ИИ: BattleController не ждёт ЛКМ между фазами.")]
    [SerializeField] private bool isAI;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    /// <summary>Накопленный щит юнита (игровая логика). Верхняя граница — <see cref="MaxShield"/> из модулей MShield.</summary>
    public int CurrentShield => currentShield;

    /// <summary>Сумма <see cref="MShield.MaxShield"/> по всем модулям щита из <see cref="Modules"/>.</summary>
    public int MaxShield => SumShieldMaxFromModules();
    public IReadOnlyList<Modules> Modules => new ResolvedModulesList(moduleObjects);
    public int MinModules => minModules;
    public int MaxModules => maxModules;
    public bool IsAI => isAI;

    private int currentHealth;
    private int currentShield;

    public void AddModule(Modules module)
    {
        if (module == null)
        {
            return;
        }

        if (GetAssignedSlotCount() >= maxModules)
        {
            return;
        }

        GameObject go = module.gameObject;
        if (ModuleObjectIndex(go) >= 0)
        {
            return;
        }

        for (int i = 0; i < moduleObjects.Count; i++)
        {
            if (moduleObjects[i] == null)
            {
                moduleObjects[i] = go;
                return;
            }
        }

        moduleObjects.Add(go);
    }

    public bool RemoveModule(Modules module)
    {
        if (module == null)
        {
            return false;
        }

        return moduleObjects.Remove(module.gameObject);
    }

    private void Start()
    {
        currentHealth = maxHealth;
        // Щит на UI ведёт Unit; начальное значение берём из MShield (префаб/инспектор), а не обнуляем.
        SetShield(SumShieldCurrentFromModules());
    }

    /// <summary>Сумма заряда и максимумов по всем <see cref="MGenerator"/> из <see cref="Modules"/> (полоска энергии в Interface).</summary>
    public void GetDisplayedEnergy(out int current, out int max)
    {
        SumGeneratorEnergyFromModules(Modules, out current, out max);
    }

    public void SetShield(int value)
    {
        currentShield = Mathf.Clamp(value, 0, MaxShield);
    }

    public void AddShield(int delta)
    {
        SetShield(currentShield + delta);
    }

    private void OnValidate()
    {
        if (minModules < 0)
        {
            minModules = 0;
        }

        if (maxModules < minModules)
        {
            maxModules = minModules;
        }

        if (moduleObjects == null)
        {
            moduleObjects = new List<GameObject>();
            return;
        }

        // Не удаляем null-элементы: в инспекторе пустые слоты списка — это null, и их вычищение
        // схлопывает массив и снова обнуляет размер (нельзя задать 2–3 модуля подряд).

        if (moduleObjects.Count > maxModules)
        {
            moduleObjects.RemoveRange(maxModules, moduleObjects.Count - maxModules);
        }

        for (int i = 0; i < moduleObjects.Count; i++)
        {
            GameObject go = moduleObjects[i];
            if (go == null)
            {
                continue;
            }

            if (go.GetComponentInChildren<Modules>(true) == null)
            {
                Debug.LogWarning($"{name}: на объекте «{go.name}» (слот модулей {i}) нет компонента Modules.");
            }
        }

        int assigned = GetResolvedModuleCount();
        if (assigned < minModules)
        {
            Debug.LogWarning($"{name}: модулей меньше минимума ({assigned}/{minModules}). Назначь объекты с Modules в инспекторе.");
        }
    }

    /// <summary>Непустые слоты списка (назначенный GameObject).</summary>
    private int GetAssignedSlotCount()
    {
        int n = 0;
        for (int i = 0; i < moduleObjects.Count; i++)
        {
            if (moduleObjects[i] != null)
            {
                n++;
            }
        }

        return n;
    }

    /// <summary>Сколько слотов дают реальный компонент Modules.</summary>
    private int GetResolvedModuleCount()
    {
        int n = 0;
        for (int i = 0; i < moduleObjects.Count; i++)
        {
            GameObject go = moduleObjects[i];
            if (go == null)
            {
                continue;
            }

            if (go.GetComponentInChildren<Modules>(true) != null)
            {
                n++;
            }
        }

        return n;
    }

    private int ModuleObjectIndex(GameObject go)
    {
        if (go == null)
        {
            return -1;
        }

        for (int i = 0; i < moduleObjects.Count; i++)
        {
            if (moduleObjects[i] == go)
            {
                return i;
            }
        }

        return -1;
    }

    public void TakeDamage()
    {
        currentHealth = Mathf.Max(0, currentHealth - 1);
        Debug.Log($"{name} получил 1 урона. HP: {currentHealth}/{maxHealth}");
    }

    public bool HasAtLeastOneDiceInModules()
    {
        IReadOnlyList<Modules> list = Modules;
        for (int m = 0; m < list.Count; m++)
        {
            if (list[m] == null)
            {
                continue;
            }

            IReadOnlyList<Dice> dices = list[m].Dices;
            for (int d = 0; d < dices.Count; d++)
            {
                if (dices[d] != null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Убирает кубики с центра экрана — возвращает под соответствующие модули (перед следующим юнитом).
    /// </summary>
    public void ResetModuleDiceToLocalLayout()
    {
        Modules[] allModules = GetComponentsInChildren<Modules>(true);
        for (int i = 0; i < allModules.Length; i++)
        {
            if (allModules[i] != null)
            {
                allModules[i].ResetDiceToModuleLocalLayout();
            }
        }
    }

    private sealed class ResolvedModulesList : IReadOnlyList<Modules>
    {
        private readonly List<GameObject> _gameObjects;

        public ResolvedModulesList(List<GameObject> gameObjects)
        {
            _gameObjects = gameObjects;
        }

        public int Count => _gameObjects?.Count ?? 0;

        public Modules this[int index]
        {
            get
            {
                if (_gameObjects == null || index < 0 || index >= _gameObjects.Count)
                {
                    return null;
                }

                GameObject go = _gameObjects[index];
                return go != null ? go.GetComponentInChildren<Modules>(true) : null;
            }
        }

        public IEnumerator<Modules> GetEnumerator()
        {
            int n = Count;
            for (int i = 0; i < n; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private int SumShieldMaxFromModules()
    {
        IReadOnlyList<Modules> list = Modules;
        int sum = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is MShield sh)
            {
                sum += sh.MaxShield;
            }
        }

        return sum;
    }

    /// <summary>Сумма <see cref="MShield.CurrentShield"/> по модулям (стартовый щит с префаба).</summary>
    private int SumShieldCurrentFromModules()
    {
        IReadOnlyList<Modules> list = Modules;
        int sum = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is MShield sh)
            {
                sum += sh.CurrentShield;
            }
        }

        return sum;
    }

    private static void SumGeneratorEnergyFromModules(IReadOnlyList<Modules> modules, out int current, out int max)
    {
        current = 0;
        max = 0;
        if (modules == null)
        {
            return;
        }

        for (int i = 0; i < modules.Count; i++)
        {
            if (modules[i] is MGenerator gen)
            {
                current += gen.CurrentCharge;
                max += gen.MaxCharge;
            }
        }
    }
}
