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
    [Tooltip("Префаб модуля из Project или объект из Hierarchy. В Awake префаб клонируется под этот Unit; объекты сцены только привязываются как дочерние.")]
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
    /// <summary>Сумма <see cref="MShield.MaxShield"/> по всем модулям щита.</summary>
    public int MaxShield => SumShieldMaxFromModules();
    /// <summary>Кешированный список модулей. Пересобирается при добавлении/удалении модулей.</summary>
    public IReadOnlyList<Modules> Modules => _resolvedModules;
    public int MinModules => minModules;
    public int MaxModules => maxModules;
    public bool IsAI => isAI;

    private int currentHealth;
    private int currentShield;
    private BattleController _battleController;
    private readonly List<Modules> _resolvedModules = new List<Modules>();

    /// <summary>
    /// Регистрирует ссылку на контроллер боя, чтобы не использовать FindObjectOfType при каждом уроне.
    /// Вызывается из <see cref="BattleController"/> в начале боя.
    /// </summary>
    public void RegisterBattleController(BattleController bc)
    {
        _battleController = bc;
    }

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

        GameObject underUnit = EnsureModuleUnderUnit(go);
        if (underUnit == null)
        {
            return;
        }

        if (ModuleObjectIndex(underUnit) >= 0)
        {
            return;
        }

        for (int i = 0; i < moduleObjects.Count; i++)
        {
            if (moduleObjects[i] == null)
            {
                moduleObjects[i] = underUnit;
                RebuildModuleCache();
                return;
            }
        }

        moduleObjects.Add(underUnit);
        RebuildModuleCache();
    }

    public bool RemoveModule(Modules module)
    {
        if (module == null)
        {
            return false;
        }

        bool removed = moduleObjects.Remove(module.gameObject);
        if (removed)
        {
            RebuildModuleCache();
        }

        return removed;
    }

    private void Awake()
    {
        SetupAllModuleObjects();
        RebuildModuleCache();
    }

    /// <summary>Перестраивает кеш <see cref="_resolvedModules"/> по текущему состоянию <see cref="moduleObjects"/>.</summary>
    private void RebuildModuleCache()
    {
        _resolvedModules.Clear();
        for (int i = 0; i < moduleObjects.Count; i++)
        {
            GameObject go = moduleObjects[i];
            if (go == null)
            {
                continue;
            }

            Modules m = go.GetComponentInChildren<Modules>(true);
            if (m != null)
            {
                _resolvedModules.Add(m);
            }
        }
    }

    /// <summary>Префабы клонирует под юнит; объекты сцены делает дочерними (мировые позиции сохраняются).</summary>
    private void SetupAllModuleObjects()
    {
        if (moduleObjects == null)
        {
            return;
        }

        for (int i = 0; i < moduleObjects.Count; i++)
        {
            GameObject go = moduleObjects[i];
            if (go == null)
            {
                continue;
            }

            GameObject underUnit = EnsureModuleUnderUnit(go);
            if (underUnit != null && !ReferenceEquals(underUnit, go))
            {
                moduleObjects[i] = underUnit;
            }
        }
    }

    /// <returns>Корень модуля под юнитом (копия префаба или тот же GO после SetParent), либо null.</returns>
    private GameObject EnsureModuleUnderUnit(GameObject go)
    {
        if (go == null || go == gameObject)
        {
            return null;
        }

        if (transform.IsChildOf(go.transform))
        {
            Debug.LogWarning($"{name}: модуль «{go.name}» выше юнита в иерархии — пропуск.");
            return null;
        }

        if (IsPrefabSourceForSpawn(go))
        {
            return Instantiate(go, transform, false);
        }

        go.transform.SetParent(transform, true);
        return go;
    }

    private static bool IsPrefabSourceForSpawn(GameObject go)
    {
#if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(go))
        {
            return true;
        }
#endif
        return !go.scene.IsValid() || !go.scene.isLoaded;
    }

    private void Start()
    {
        currentHealth = maxHealth;
        SetShield(SumShieldCurrentFromModules());
    }

    /// <summary>Сумма заряда и максимумов по всем <see cref="MGenerator"/> из <see cref="Modules"/> (полоска энергии в Interface).</summary>
    public void GetDisplayedEnergy(out int current, out int max)
    {
        current = 0;
        max = 0;
        for (int i = 0; i < _resolvedModules.Count; i++)
        {
            if (_resolvedModules[i] is MGenerator gen)
            {
                current += gen.CurrentCharge;
                max += gen.MaxCharge;
            }
        }
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
        TakeDamage(1);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        int absorbedByShield = Mathf.Min(currentShield, amount);
        if (absorbedByShield > 0)
        {
            SetShield(currentShield - absorbedByShield);
        }

        int damageToHealth = amount - absorbedByShield;
        if (damageToHealth > 0)
        {
            currentHealth = Mathf.Max(0, currentHealth - damageToHealth);
            _battleController?.NotifyHealthChangedAfterDamage(this);
        }

        Debug.Log($"{name}: урон {amount} (щит −{absorbedByShield}, HP −{damageToHealth}). HP: {currentHealth}/{maxHealth}, щит: {currentShield}/{MaxShield}.");
    }

    public bool HasAtLeastOneDiceInModules()
    {
        for (int m = 0; m < _resolvedModules.Count; m++)
        {
            if (_resolvedModules[m] != null && _resolvedModules[m].HasConfiguredDiceForBattle())
            {
                return true;
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

    private int SumShieldMaxFromModules()
    {
        int sum = 0;
        for (int i = 0; i < _resolvedModules.Count; i++)
        {
            if (_resolvedModules[i] is MShield sh)
            {
                sum += sh.MaxShield;
            }
        }

        return sum;
    }

    private int SumShieldCurrentFromModules()
    {
        int sum = 0;
        for (int i = 0; i < _resolvedModules.Count; i++)
        {
            if (_resolvedModules[i] is MShield sh)
            {
                sum += sh.CurrentShield;
            }
        }

        return sum;
    }
}
