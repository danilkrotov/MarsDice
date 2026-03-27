using UnityEngine;

public class MGenerator : Modules
{
    [Header("Generator charge")]
    [Min(0)]
    [SerializeField] private int currentCharge = 3;
    [Min(0)]
    [SerializeField] private int maxCharge = 3;

    [Header("Start dice")]
    [SerializeField] private string energyDicePrefabPath = "Dices/DiceEnergy";
    [SerializeField] private Vector3 startDiceLocalPosition = Vector3.zero;

    public int CurrentCharge => currentCharge;
    public int MaxCharge => maxCharge;

    public override bool TryAddDice(Dice dice)
    {
        if (dice != null && !(dice is EnergyDice))
        {
            Debug.LogWarning($"{name}: MGenerator принимает только EnergyDice.");
            return false;
        }

        return base.TryAddDice(dice);
    }

    private void Start()
    {
        SpawnStartEnergyDice();
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        EnforceDiceType<EnergyDice>();

        if (maxCharge < 0)
        {
            maxCharge = 0;
        }

        if (currentCharge < 0)
        {
            currentCharge = 0;
        }

        if (currentCharge > maxCharge)
        {
            currentCharge = maxCharge;
        }
    }

    private void SpawnStartEnergyDice()
    {
        GameObject prefab = Resources.Load<GameObject>(energyDicePrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"{name}: не найден префаб по пути Resources/{energyDicePrefabPath}");
            return;
        }

        GameObject diceObject = Instantiate(prefab, transform);
        diceObject.transform.localPosition = startDiceLocalPosition;
        diceObject.transform.localRotation = Quaternion.identity;

        EnergyDice energyDice = diceObject.GetComponent<EnergyDice>();
        if (energyDice == null)
        {
            Debug.LogWarning($"{name}: префаб {prefab.name} не содержит компонент EnergyDice.");
            Destroy(diceObject);
            return;
        }

        if (!TryAddDice(energyDice))
        {
            Destroy(diceObject);
        }
    }
}
