using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    [Header("Модули юнита")]
    [Min(0)]
    [SerializeField] private int minModules = 1;
    [Min(0)]
    [SerializeField] private int maxModules = 3;
    [SerializeField] private List<Modules> modules = new List<Modules>();

    [Header("HP")]
    [SerializeField] private int maxHealth = 10;

    [Header("Щит (отображение на Interface)")]
    [Min(0)]
    [SerializeField] private int maxShield = 10;

    [Header("Управление")]
    [Tooltip("Если включено, юнит считается под управлением ИИ: BattleController не ждёт ЛКМ между фазами.")]
    [SerializeField] private bool isAI;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public int CurrentShield => currentShield;
    public int MaxShield => maxShield;
    public IReadOnlyList<Modules> Modules => modules;
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

        if (GetNonNullModuleCount() >= maxModules)
        {
            return;
        }

        if (modules.Contains(module))
        {
            return;
        }

        for (int i = 0; i < modules.Count; i++)
        {
            if (modules[i] == null)
            {
                modules[i] = module;
                return;
            }
        }

        modules.Add(module);
    }

    public bool RemoveModule(Modules module)
    {
        if (module == null)
        {
            return false;
        }

        return modules.Remove(module);
    }

    private void Start()
    {
        currentHealth = maxHealth;
        currentShield = 0;
    }

    public void GetDisplayedEnergy(out int current, out int max)
    {
        current = 0;
        max = 0;
        IReadOnlyList<Modules> list = Modules;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is MGenerator gen)
            {
                current = gen.CurrentCharge;
                max = gen.MaxCharge;
                return;
            }
        }
    }

    public void SetShield(int value)
    {
        currentShield = Mathf.Clamp(value, 0, maxShield);
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

        if (modules == null)
        {
            modules = new List<Modules>();
            return;
        }

        // Не удаляем null-элементы: в инспекторе пустые слоты списка — это null, и их вычищение
        // схлопывает массив и снова обнуляет размер (нельзя задать 2–3 модуля подряд).

        if (modules.Count > maxModules)
        {
            modules.RemoveRange(maxModules, modules.Count - maxModules);
        }

        int assigned = GetNonNullModuleCount();
        if (assigned < minModules)
        {
            Debug.LogWarning($"{name}: модулей меньше минимума ({assigned}/{minModules}). Назначь модули в инспекторе.");
        }
    }

    private int GetNonNullModuleCount()
    {
        int n = 0;
        for (int i = 0; i < modules.Count; i++)
        {
            if (modules[i] != null)
            {
                n++;
            }
        }

        return n;
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
}
