using UnityEngine;

public class StartScript : MonoBehaviour
{
    [Header("Юниты")]
    [SerializeField] private Unit playerUnit;
    [SerializeField] private Unit npcUnit;

    [Header("Имена дочерних объектов с MGenerator")]
    [SerializeField] private string playerGeneratorChildName = "MGenerator";
    [SerializeField] private string npcGeneratorChildName = "MGenerator";

    private void Start()
    {
        if (playerUnit != null)
        {
            MGenerator gen = CreateGeneratorOnChild(playerUnit.transform, playerGeneratorChildName);
            playerUnit.AddModule(gen);
        }

        if (npcUnit != null)
        {
            MGenerator gen = CreateGeneratorOnChild(npcUnit.transform, npcGeneratorChildName);
            npcUnit.AddModule(gen);
        }
    }

    private static MGenerator CreateGeneratorOnChild(Transform parent, string childName)
    {
        string name = string.IsNullOrWhiteSpace(childName) ? "MGenerator" : childName.Trim();
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.AddComponent<MGenerator>();
    }
}
