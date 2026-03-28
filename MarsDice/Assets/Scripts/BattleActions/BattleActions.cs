using System.Collections;
using UnityEngine;

public abstract class BattleActions : MonoBehaviour
{
    public abstract string PhaseName { get; }

    /// <summary>
    /// Если false — после фазы BattleController не ждёт ЛКМ (фаза сама управляет переходом).
    /// </summary>
    public virtual bool UsesManualAdvanceClick => true;

    public abstract IEnumerator Action(BattleController battleController, int unitIndex, Unit unit);
}
