using System.Collections.Generic;
using UnityEngine;

public class StartScript : MonoBehaviour
{
    [Header("Юниты")]
    [SerializeField] private Unit playerUnit;
    [SerializeField] private Unit npcUnit;

    [Header("Модули игрока (GameObject с компонентом Modules)")]
    [SerializeField] private List<GameObject> playerModuleObjects = new List<GameObject>();

    [Header("Модули NPC (GameObject с компонентом Modules)")]
    [SerializeField] private List<GameObject> npcModuleObjects = new List<GameObject>();

    // Awake: до любого Start() (в т.ч. BattleController), чтобы в списке модулей уже были модули.
    private void Awake()
    {
        RegisterModules(playerUnit, playerModuleObjects);
        RegisterModules(npcUnit, npcModuleObjects);
    }

    private static void RegisterModules(Unit unit, List<GameObject> roots)
    {
        if (unit == null || roots == null)
        {
            return;
        }

        for (int i = 0; i < roots.Count; i++)
        {
            GameObject go = roots[i];
            if (go == null)
            {
                continue;
            }

            Modules module = go.GetComponent<Modules>();
            if (module == null)
            {
                Debug.LogWarning($"StartScript: на «{go.name}» нет компонента Modules — пропуск.");
                continue;
            }

            unit.AddModule(module);
        }
    }
}
