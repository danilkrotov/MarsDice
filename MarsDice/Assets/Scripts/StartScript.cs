using UnityEngine;

public class StartScript : MonoBehaviour
{
    [Header("Юниты")]
    [SerializeField] private Unit playerUnit;
    [SerializeField] private Unit npcUnit;

    [Header("Имена дочерних объектов с MGenerator")]
    [SerializeField] private string playerGeneratorChildName = "MGenerator";
    [SerializeField] private string npcGeneratorChildName = "MGenerator";

    [Header("Имена дочерних объектов с MShield")]
    [SerializeField] private string playerShieldChildName = "MShield";
    [SerializeField] private string npcShieldChildName = "MShield";

    // Awake: до любого Start() (в т.ч. BattleController), чтобы в списке модулей уже были модули.
    private void Awake()
    {
        if (playerUnit != null)
        {
            MGenerator gen = CreateGeneratorOnChild(playerUnit.transform, playerGeneratorChildName);
            playerUnit.AddModule(gen);

            MShield shield = CreateShieldOnChild(playerUnit.transform, playerShieldChildName);
            playerUnit.AddModule(shield);
        }

        if (npcUnit != null)
        {
            MGenerator gen = CreateGeneratorOnChild(npcUnit.transform, npcGeneratorChildName);
            npcUnit.AddModule(gen);

            MShield shield = CreateShieldOnChild(npcUnit.transform, npcShieldChildName);
            npcUnit.AddModule(shield);
        }
    }

    private static MGenerator CreateGeneratorOnChild(Transform parent, string childName)
    {
        string name = string.IsNullOrWhiteSpace(childName) ? "MGenerator" : childName.Trim();
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.AddComponent<MGenerator>();
    }

    private static MShield CreateShieldOnChild(Transform parent, string childName)
    {
        string name = string.IsNullOrWhiteSpace(childName) ? "MShield" : childName.Trim();
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.AddComponent<MShield>();
    }
}
