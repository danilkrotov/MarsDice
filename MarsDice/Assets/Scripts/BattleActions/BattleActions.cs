using System.Collections;
using UnityEngine;

public abstract class BattleActions : MonoBehaviour
{
    public abstract string PhaseName { get; }
    public abstract IEnumerator Action(BattleController battleController, int unitIndex, Unit unit);
}
