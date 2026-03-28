# MarsDice — документация проекта

Прототип пошагового боя в Unity: юниты с модулями, кубики на модулях, фазы боя (энергия, щит, урон), мировые полоски HP / энергии / щита.

---

## Структура скриптов (`Assets/Scripts`)

| Область | Файлы |
|--------|--------|
| Ядро боя | `BattleController.cs`, `Unit.cs` |
| Модули | `Modules.cs`, `MGenerator.cs`, `MShield.cs` |
| Кубики | `Dices/Dice.cs`, `Dices/EnergyDice.cs`, `Dices/ShieldDice.cs` |
| Раскладка кубиков | `DiceScreenLayout.cs` |
| UI | `Interface.cs` |
| Старт сцены | `StartScript.cs` |
| Фазы боя | `BattleActions/BattleActions.cs`, `EnergyRegen.cs`, `ShieldUp.cs`, `DamageDeal.cs` |

Префабы кубиков подгружаются из **`Resources`** (например `Resources/Dices/DiceEnergy`, `DiceShield`).

---

## Юнит (`Unit`)

- **Модули** — список `Modules` (мин/макс количество в инспекторе).
- **HP** — `maxHealth`, `TakeDamage()`.
- **Щит для UI** — `maxShield`, `currentShield` (в `Start()` щит обнуляется), `AddShield` / `SetShield`. Синяя полоска в `Interface` читает эти значения.
- **Энергия для UI** — `GetDisplayedEnergy`: берётся с **первого** `MGenerator` в списке модулей (если несколько генераторов, полоска показывает только первый).
- **Кубики** живут только на модулях; `HasAtLeastOneDiceInModules()` проверяет наличие непустых `Dice` в любом модуле.
- **`ResetModuleDiceToLocalLayout()`** — все `Modules` на иерархии юнита возвращают кубики под свои трансформы (после раскладки по экрану).

---

## Модули (`Modules`)

Базовый класс для оборудования юнита.

- Параметры слотов кубиков: `minDiceSlots`, `maxDiceSlots`, `diceSlots` (фактический лимит списка `Dices`).
- **`TryAddDice` / `RemoveDice` / `Dices`** — список ссылок на кубики в сцене (пустые слоты в инспекторе допускаются как `null`).
- **`ResetDiceToModuleLocalLayout()`** — виртуальный сброс позиций кубиков относительно модуля; шаг по X задаётся полем **`resetDiceHorizontalStep`**.
- Тип в enum: `ModuleType` — `Generator`, `Shield`.

### `MGenerator`

- **Заряд** — `currentCharge` / `maxCharge`; `AddCharge`, **`SubtractCharge`** (списание, в т.ч. для фазы щита).
- Принимает только **`EnergyDice`**; стартовый кубик создаётся в **`Awake`** из `Resources` по пути **`energyDicePrefabPath`** (по умолчанию `Dices/DiceEnergy`).
- Переопределён сброс позиций кубиков с учётом **`startDiceLocalPosition`**.

### `MShield`

- **Щит модуля** — `currentShield` / `maxShield` (по умолчанию 10/10); `AddShield`, `ReduceShield`, `SetShield`.
- Принимает только **`ShieldDice`**; стартовый кубик — префаб **`Dices/DiceShield`**.
- В `Awake` / `OnValidate` выставляется `ModuleType.Shield`.

---

## Кубики (`Dice`)

- Базовый **`Dice`**: грани 1–6, текстуры граней, флаги «провал» по граням, анимация броска, `RollDice()` (корутина), `LastResult`, `LastFailed`.
- Определение верхней грани — относительно **`Camera.main`**.
- **`EnergyDice`** — на каждую грань своё значение восстановления энергии: `GetEnergyRestoreByFace(face)`.
- **`ShieldDice`** — на каждую грань: восстановление щита и стоимость энергии — `GetShieldRestoreByFace`, `GetEnergyCostByFace`.

---

## Раскладка кубиков (`DiceScreenLayout`)

- Точка в центре экрана: `ViewportToWorldPoint(0.5, 0.5, distanceFromCamera)`.
- Несколько кубиков выстраиваются вдоль **`camera.right`** с шагом **`horizontalSpacing`**, группа центрируется (как выравнивание текста по центру).
- Вызывается из **`BattleController.LayoutModuleDice(module)`** перед бросками в фазах.

---

## Контроллер боя (`BattleController`)

1. В **`Start`** запускается корутина **`PlayTurnSequence`**.
2. Первый кадр — **`yield return null`**, чтобы успели выполниться **`Awake`** у других объектов (в т.ч. `StartScript`, спавн кубиков на модулях).
3. По очереди **юниты** из `unitObjects` (нужны `Unit` и хотя бы один кубик в модулях).
4. Для каждого юнита по порядку элементы списка **`battleActions`** (компоненты, наследующие **`BattleActions`**):
   - показывается имя фазы (`PhaseName`);
   - **`yield return action.Action(this, unitIndex, unit)`**;
   - ожидание **ЛКМ** по игровому окну;
   - **`unit.ResetModuleDiceToLocalLayout()`** — кубики убираются с центра экрана.
5. После всех фаз юнита — ещё один сброс кубиков (на случай пропущенных `null`-слотов в списке фаз).

Настройки в инспекторе: **`diceViewDistanceFromCamera`**, **`diceHorizontalSpacing`**, список **`battleActions`**.

Текущая фаза дублируется в **`OnGUI`** («Фаза: …»).

---

## Фазы боя (`BattleActions`)

Базовый класс: абстрактные **`PhaseName`** и **`Action(BattleController, int unitIndex, Unit unit)`**.

| Класс | Назначение |
|--------|------------|
| **EnergyRegen** | Все **`MGenerator`** на юните: раскладка кубиков, бросок каждого `EnergyDice`, при успехе — `AddCharge` на своём генераторе. |
| **ShieldUp** | Все **`MShield`**: раскладка, бросок `ShieldDice`; при успехе проверяется суммарный заряд всех **`MGenerator`**; если хватает энергии по стоимости грани — списание с генераторов по порядку и **`unit.AddShield(restore)`** (синяя полоска). |
| **DamageDeal** | По модулям из **`unit.Modules`**: раскладка кубиков модуля, бросок; при успехе — урон следующему юниту в `unitObjects` через **`TakeDamage()`**. |

Компоненты фаз вешаются на объекты в сцене; в **`BattleController`** в список перетаскиваются **именно ссылки на компоненты** фаз.

---

## Старт сцены (`StartScript`)

- Выполняется в **`Awake`** (раньше **`Start`** у `BattleController`), чтобы модули уже были в списке юнита.
- Для **player** и **npc** создаются дочерние объекты с **`MGenerator`** и **`MShield`**, вызывается **`AddModule`**.

Имена дочерних объектов настраиваются отдельно для генератора и щита.

---

## Интерфейс (`Interface`)

- Висит на **том же GameObject**, что и **`Unit`**.
- Три мировые полоски (billboard к камере): **HP**, **энергия** (первый `MGenerator`), **щит** (`Unit`).
- Позиция стека: `transform.position + stackOffset`.

---

## Рекомендуемый порядок настройки в Unity

1. Объекты юнитов: **`Unit`**, при необходимости **`Interface`**.
2. Объект с **`StartScript`**: назначить `playerUnit` / `npcUnit` (или оставить только одного — по задаче).
3. Объект с **`BattleController`**: список **`unitObjects`**, порядок = порядок ходов; список **`battleActions`** (например EnergyRegen → ShieldUp → DamageDeal).
4. На сцене должны быть компоненты фаз и ссылки на них в контроллере.
5. Префабы **`DiceEnergy`**, **`DiceShield`** лежат в **`Resources/Dices/`** с нужными компонентами **`EnergyDice`** / **`ShieldDice`**.
6. **`Camera.main`** должен быть задан (тег MainCamera).

---

## Зависимости и порядок выполнения

- Спавн кубиков на **`MGenerator`** / **`MShield`** — в **`Awake`** модулей.
- Проверка «есть ли кубики» в **`BattleController`** выполняется после одного кадра ожидания, чтобы не было гонки со **`Start`** других скриптов.

---

## Возможные доработки (идеи)

- Показ энергии с нескольких генераторов агрегированно или выбором модуля.
- Синхронизация щита **`MShield`** с **`Unit`** для боя, если нужен единый пул.
- Повтор цикла ходов вместо одного прохода по всем юнитам.

---

*Документ соответствует состоянию кода в репозитории; при изменении API обновляйте разделы вручную.*
