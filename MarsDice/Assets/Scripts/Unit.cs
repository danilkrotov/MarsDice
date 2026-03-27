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
    [SerializeField] private List<Modules> modules = new List<Modules>();

    [Header("Префабы кубиков этого юнита")]
    [SerializeField] private List<GameObject> dicePrefabs = new List<GameObject>();

    [Header("HP")]
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 1.5f, 0f);

    public IReadOnlyList<GameObject> DicePrefabs => dicePrefabs;
    public int CurrentHealth => currentHealth;
    public IReadOnlyList<GameObject> LastSpawnedDices => lastSpawnedDices;
    public IReadOnlyList<Modules> Modules => modules;
    public int MinModules => minModules;
    public int MaxModules => maxModules;

    private int currentHealth;
    private HealthBarVisual healthBar;
    private readonly List<GameObject> lastSpawnedDices = new List<GameObject>();

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
        CreateHealthBar();
        RefreshHealthBar();
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

    private void Update()
    {
        if (healthBar != null)
        {
            healthBar.root.position = transform.position + healthBarOffset;
        }
    }

    public void TakeDamage()
    {
        currentHealth = Mathf.Max(0, currentHealth - 1);
        RefreshHealthBar();
        Debug.Log($"{name} получил 1 урона. HP: {currentHealth}/{maxHealth}");
    }

    public bool HasAtLeastOneDicePrefab()
    {
        for (int i = 0; i < dicePrefabs.Count; i++)
        {
            if (dicePrefabs[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    public void AddSpawnedDice(GameObject diceObject)
    {
        if (diceObject != null)
        {
            lastSpawnedDices.Add(diceObject);
        }
    }

    public void ClearSpawnedDices()
    {
        for (int i = 0; i < lastSpawnedDices.Count; i++)
        {
            if (lastSpawnedDices[i] != null)
            {
                Destroy(lastSpawnedDices[i]);
            }
        }

        lastSpawnedDices.Clear();
    }

    private void CreateHealthBar()
    {
        GameObject root = new GameObject($"{name}_HealthBar");
        root.transform.position = transform.position + healthBarOffset;

        GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
        background.name = "Background";
        background.transform.SetParent(root.transform, false);
        background.transform.localScale = new Vector3(1.2f, 0.15f, 0.05f);
        background.GetComponent<Renderer>().material.color = Color.red;
        Destroy(background.GetComponent<Collider>());

        GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fill.name = "Fill";
        fill.transform.SetParent(root.transform, false);
        fill.transform.localScale = new Vector3(1.2f, 0.1f, 0.03f);
        fill.GetComponent<Renderer>().material.color = Color.green;
        Destroy(fill.GetComponent<Collider>());

        TextMesh textMesh = new GameObject("Text").AddComponent<TextMesh>();
        textMesh.transform.SetParent(root.transform, false);
        textMesh.transform.localPosition = new Vector3(0f, 0.18f, 0f);
        textMesh.characterSize = 0.1f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;
        textMesh.fontSize = 64;

        healthBar = new HealthBarVisual(root.transform, fill.transform, textMesh);
    }

    private void RefreshHealthBar()
    {
        if (healthBar == null || maxHealth <= 0)
        {
            return;
        }

        float ratio = Mathf.Clamp01((float)currentHealth / maxHealth);
        Vector3 scale = healthBar.fill.localScale;
        scale.x = 1.2f * ratio;
        healthBar.fill.localScale = scale;
        healthBar.fill.localPosition = new Vector3(-0.6f + (scale.x * 0.5f), 0f, -0.01f);
        healthBar.text.text = $"{currentHealth}/{maxHealth}";
    }

    private class HealthBarVisual
    {
        public readonly Transform root;
        public readonly Transform fill;
        public readonly TextMesh text;

        public HealthBarVisual(Transform root, Transform fill, TextMesh text)
        {
            this.root = root;
            this.fill = fill;
            this.text = text;
        }
    }
}
