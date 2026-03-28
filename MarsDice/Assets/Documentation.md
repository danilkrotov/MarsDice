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
| Фазы боя | `BattleActions/BattleActions.cs`, `EnergyRegen.cs`, `ShieldUp.cs`, `DamageDeal.cs` |

Префабы кубиков подгружаются из **`Resources`** (например `Resources/Dices/DiceEnergy`, `DiceShield`).

---

## Юнит (`Unit`)

- **Модули** — список `Modules` (мин/макс количество в инспекторе).
- **HP** — `maxHealth`, `TakeDamage()`; в `Start()` здоровье выставляется в `maxHealth`.
- **Щит для UI** — `currentShield` / `MaxShield`; в `Start()` начальное значение берётся как **сумма `currentShield` по всем `MShield`** в модулях (не обнуляется). `AddShield` / `SetShield`; полоска щита в `Interface` читает `Unit`.
- **Энергия для UI** — `GetDisplayedEnergy`: **сумма** `currentCharge` и **сумма** `maxCharge` по **всем** `MGenerator` в списке модулей.
- **Кубики** живут только на модулях; `HasAtLeastOneDiceInModules()` проверяет, что хотя бы у одного модуля есть настроенный кубик (`HasConfiguredDiceForBattle`).
- **`ResetModuleDiceToLocalLayout()`** — все `Modules` в иерархии юнита (`GetComponentsInChildren`) возвращают учтённые в слотах кубики под свои трансформы (после раскладки по экрану).

---

## Модули (`Modules`)

Базовый класс для оборудования юнита.

- Параметры слотов кубиков: `minDiceSlots`, `maxDiceSlots`, `diceSlots` (фактический лимит списка `Dices`).
- В **`Awake`** вызывается **`CacheDiceTemplatesAndClearRuntimeSlots()`**: объекты из инспектора сохраняются как **шаблоны** для `Instantiate`, слоты `diceObjects` очищаются — **экземпляры в сцене до боя не создаются**.
- **`ReplenishConsumedDice()`** — заполняет пустые слоты копиями по шаблонам (перед раскладкой кубиков на экран).
- **`HasConfiguredDiceForBattle()`** — есть ли шаблон или уже созданный экземпляр в слотах (для пропуска юнита без кубиков).
- **`TryAddDice` / `RemoveDice` / `Dices`** — список ссылок на кубики в сцене (пустые слоты — `null`).
- **`RemoveDice`** ищет слот и по `GameObject` кубика, и если компонент **`Dice`** лежит на **дочернем** объекте относительно корня слота.
- **`GetSlotRootIfContains(Dice)`** — корень объекта в слоте (`diceObjects[i]`), если этот `Dice` на нём или внутри него; нужен перед `Destroy`, чтобы удалять весь префаб кубика.
- **`ResetDiceToModuleLocalLayout()`** — виртуальный сброс позиций кубиков относительно модуля; шаг по X — **`resetDiceHorizontalStep`**.
- Тип в enum: `ModuleType` — `Generator`, `Shield`.

### `MGenerator`

- **Заряд** — `currentCharge` / `maxCharge`; `AddCharge`, **`SubtractCharge`** (списание, в т.ч. для фазы щита).
- Принимает только **`EnergyDice`** (`TryAddDice`).
- Переопределён **`ResetDiceToModuleLocalLayout()`** с учётом **`startDiceLocalPosition`** и `resetDiceHorizontalStep`.

### `MShield`

- **Щит модуля** — `currentShield` / `maxShield`; `AddShield`, `ReduceShield`, `SetShield`.
- Принимает только **`ShieldDice`** (`TryAddDice` / `EnforceDiceType`).
- В `OnValidate` выставляется `ModuleType.Shield`.
- **`ResetDiceToModuleLocalLayout()`** — как у генератора, со своим `startDiceLocalPosition`.

---

## Кубики (`Dice`)

- Базовый **`Dice`**: грани 1–6, текстуры граней, флаги «провал» по граням, анимация броска, `RollDice()` (корутина), `LastResult`, `LastFailed`.
- Определение верхней грани — относительно **`Camera.main`**; геометрия граней учитывает **`MeshFilter.sharedMesh.bounds`** и масштаб трансформа (корректный вид при неравномерном `localScale`).
- **`EnergyDice`** — на каждую грань своё значение восстановления энергии: `GetEnergyRestoreByFace(face)`.
- **`ShieldDice`** — на каждую грань: восстановление щита и стоимость энергии — `GetShieldRestoreByFace`, `GetEnergyCostByFace`.

---

## Раскладка кубиков (`DiceScreenLayout`)

- Точка в центре экрана: `ViewportToWorldPoint(0.5, 0.5, distanceFromCamera)`.
- Несколько кубиков выстраиваются вдоль **`camera.right`** с шагом **`horizontalSpacing`**, группа центрируется (как выравнивание текста по центру).
- Вызывается из **`BattleController.LayoutModuleDice(module)`** и **`BattleController.LayoutDiceGroupCenteredOnScreen(dices)`** (несколько модулей / одна общая группа).

---

## Контроллер боя (`BattleController`)

1. В **`Start`** запускается корутина **`PlayTurnSequence`**.
2. Первый кадр — **`yield return null`**, чтобы успели выполниться **`Awake`** у других объектов (модули, юниты и т.д.).
3. По очереди **юниты** в порядке **`firstTeam`**, затем **`secondTeam`** (нужны `Unit` и хотя бы один настроенный кубик в модулях).
4. Для каждого юнита по порядку элементы списка **`battleActions`** (компоненты **`BattleActions`**):
   - показывается имя фазы (`PhaseName`);
   - **`yield return action.Action(this, unitIndex, unit)`**;
   - если **`!unit.IsAI`** и у фазы **`UsesManualAdvanceClick == true`** — ожидание **ЛКМ** по игровому окну (после фазы фазы вроде `EnergyRegen` / `ShieldUp` сами ждут клик внутри корутины и ставят **`UsesManualAdvanceClick => false`**);
   - **`unit.ResetModuleDiceToLocalLayout()`** — кубики убираются с центра экрана под модули.
5. После всех фаз юнита — ещё один сброс кубиков.

Настройки в инспекторе: **`diceViewDistanceFromCamera`**, **`diceHorizontalSpacing`** (расстояние между центрами кубиков при групповой раскладке), список **`battleActions`**, **`diceSpawnOffset`** (для других сценариев).

Текущая фаза дублируется в **`OnGUI`** («Фаза: …»).

---

## Фазы боя (`BattleActions`)

Базовый класс: **`PhaseName`**, **`Action(...)`**, опционально **`UsesManualAdvanceClick`** (по умолчанию `true` — контроллер ждёт ЛКМ после фазы).

| Класс | Назначение |
|--------|------------|
| **EnergyRegen** | Все **`MGenerator`**: `ReplenishConsumedDice`, общая раскладка всех `EnergyDice` по центру экрана, **параллельный** бросок; начисление заряда по результату; для игрока — ожидание ЛКМ; затем **`RemoveDice`** и **`Destroy`** для каждого выложенного кубика (уничтожается **корень слота**), в конце **`unit.ResetModuleDiceToLocalLayout()`**. **`UsesManualAdvanceClick => false`**. |
| **ShieldUp** | Все **`MShield`**: пополнение слотов, общая раскладка, **параллельный** бросок; игрок кликает по кубику щита (ЛКМ), ИИ забирает кубики по очереди; при успехе проверяется пул энергии по всем **`MGenerator`**, списание и **`unit.AddShield`**. Кнопка **`OnGUI` «Пропустить»** — досрочный выход: **все оставшиеся кубики щита** снимаются с модулей и **уничтожаются** (слоты пустые; на следующем заходе в фазу `ReplenishConsumedDice` создаст новые). **`UsesManualAdvanceClick => false`**. |
| **DamageDeal** | По модулям из **`unit.Modules`**: раскладка кубиков модуля, бросок; при успехе — урон выбранной цели (противник из другой команды, порядок по объединённому списку команд) через **`TakeDamage()`**; снятие кубика с **`RemoveDice`** и **`Destroy`** корня слота. |

Компоненты фаз вешаются на объекты в сцене; в **`BattleController`** в список перетаскиваются **ссылки на компоненты** фаз.

Модули (`MGenerator`, `MShield` и др.) задаются **в сцене или префабе юнита** и попадают в **`Unit`** через список модулей в инспекторе (`AddModule` / дочерние объекты с компонентом `Modules` — по вашей схеме сборки сцены).

---

## Интерфейс (`Interface`)

- Висит на **том же GameObject**, что и **`Unit`**.
- Три мировые полоски (ориентация как у камеры): **HP**, **энергия** (агрегат по всем `MGenerator` через `Unit.GetDisplayedEnergy`), **щит** (`Unit`).
- Позиция стека: `transform.position + stackOffset`.
- **`verticalBarStep`** — расстояние по вертикали между центрами полосок (настраивается в инспекторе).
- Над каждой полоской — **`TextMesh`** с текущим/максимумом; за цифрами — тёмная **подложка** (unlit-куб), текст ближе к камере по локальному Z.

---

## Рекомендуемый порядок настройки в Unity

1. Объекты юнитов: **`Unit`**, дочерние (или ссылки на) **`Modules`** с префабами кубиков в слотах; при необходимости **`Interface`** на том же объекте, что и `Unit`.
2. Объект с **`BattleController`**: списки **`firstTeam`** и **`secondTeam`** (порядок хода: сначала вся первая команда, затем вторая); список **`battleActions`** (типично **EnergyRegen → ShieldUp**; при необходимости **DamageDeal** и др.).
3. На сцене должны быть компоненты фаз и ссылки на них в контроллере.
4. Префабы **`DiceEnergy`**, **`DiceShield`** лежат в **`Resources/Dices/`** с компонентами **`EnergyDice`** / **`ShieldDice`**; в префабах модулей в слотах указываются ссылки на эти префабы (рантайм-копии создаёт **`ReplenishConsumedDice`**).
5. **`Camera.main`** должен быть задан (тег MainCamera).

---

## Зависимости и порядок выполнения

- Шаблоны кубиков кэшируются в **`Modules.Awake`**; экземпляры для боя появляются при **`ReplenishConsumedDice`** в начале соответствующих фаз.
- Проверка «есть ли кубики» в **`BattleController`** выполняется после одного кадра ожидания, чтобы не было гонки со **`Start`** других скриптов.

---

## Возможные доработки (идеи)

- Отдельный UI для энергии по каждому генератору (сейчас сумма на одной полоске).
- Повтор цикла ходов вместо одного прохода по всем юнитам.
- Кнопка «Пропустить» не через `OnGUI`, а через uGUI/Input System.

---

*Документ соответствует состоянию кода в репозитории; при изменении API обновляйте разделы вручную.*
