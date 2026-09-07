# Ultimate Backup Replacement - сборка и запуск

Код прошёл проверку реальным компилятором (mono mcs) против двух стаб-библиотек -
одна повторяет Rage API, вторая RAGENativeUI - это ловит опечатки, несовпадения
типов и пропущенные using'и, но **не гарантирует**, что каждый нативный вызов
(`NativeFunction.Natives.XXX`) и точная сигнатура RAGENativeUI в реальном SDK
совпадают 1:1 - у меня нет доступа к сайтам RPH/RAGENativeUI, чтобы скачать
настоящие DLL и собрать против них. Собрать и проверить в реальной VS с
реальным SDK нужно тебе - но риск мелких правок здесь ощутимо ниже, чем был бы
с WPF: у RAGENativeUI маленький, хорошо задокументированный и стабильный API.

## UI: почему RAGENativeUI, а не WPF

Первая версия UI была на WPF (полноценное окно поверх игры). Технически рабочий
подход, но: требовал отдельный STA-поток, WPF-часть было физически невозможно
проверить компилятором в моём Linux-окружении, и просто больше мороки для
первого теста. Переехали на **RAGENativeUI** - стандартное для LSPDFR/RPH
scroll-меню со списком пунктов и сабменю, тот подход, который используют
большинство подобных плагинов. Три пункта в главном меню - PARTNERS / CODE 2 /
CODE 3, клик открывает сабменю, открытие любого меню ставит игру на паузу.

## Что нужно скачать самому

1. **RAGE Plugin Hook** (https://ragepluginhook.net/, установщик + "Plugin API"
   из раздела Downloads). После установки в папке RPH будет `RAGEPluginHook.exe`.
2. **RAGENativeUI** - весь UI мода на ней. Возьми DLL с
   https://github.com/alexguirre/RAGENativeUI/releases (или актуального форка,
   если оригинальный репозиторий не поддерживается - уточни у сообщества
   LSPDFR/RPH какой сейчас живой).
3. **LSPDFR** (https://www.lcpdfr.com/) - мод рассчитан на него, без LSPDFR
   часть команд (Request ID/License/Registration, Arrest через LSPDFR API)
   просто тихо упадёт на голый нативный фоллбек.
4. **Visual Studio 2019/2022** (подойдёт бесплатная Community) с компонентом
   ".NET desktop development".

## Сборка

1. Открой `UltimateBackupReplacement.csproj` в Visual Studio (.NET Framework
   4.8 таргет уже прописан).
2. Положи `RAGEPluginHook.exe` и `RAGENativeUI.dll` в папку `lib/` рядом с
   `.csproj` (пути уже прописаны в `<HintPath>`).
3. Выбери конфигурацию Release, платформу x64, Build → Build Solution.
4. Готовый `UltimateBackupReplacement.dll` (папка `bin/Release`) скопируй в
   `Grand Theft Auto V/plugins/` (у некоторых версий RPH - в
   `plugins/Managed/`, смотри доки под свою версию).
5. Запускай GTA V через ярлык RAGE Plugin Hook, не через обычный лаунчер.

## Управление

- **B** - открыть/закрыть главное меню (PARTNERS / CODE 2 / CODE 3), открытие
  ставит игру на паузу.
- **T** - если ты за рулём и меню закрыто: все партнёры с назначенной машиной
  садятся за руль. Если только что нажал "Go To" в меню - подтверждает точку,
  куда наведена камера. **Escape** в этот момент - отмена.
- **O** - Force Arrest на ближайшего/aimed ped'а силами доступных партнёров.
- **U** - Open Fire в точку прицела: тап - один партнёр, зажатие - все.

Все три бинда (T/O/U) не участвуют, пока открыто меню (`NativeMenu.IsOpen`),
чтобы не было конфликтов с навигацией по пунктам.

## Быстрый тест без возни с loadout

В меню Partners первый пункт - **New Partner** - спавнит напарника с дефолтным
набором (Pistol/Carbine Rifle/LSPD Patrol машина, без copy-outfit) одним кликом,
без необходимости лезть в детали экипировки. Дальше сразу доступны Select All,
Force Arrest (O), Open Fire (U) - весь основной геймплейный цикл проверяется
за пару минут.

## Интеграция с LSPDFR SDK и Stop The Ped

### LSPDFR (`Compatibility/LspdfrBridge.cs`)
Через reflection на загруженную сборку LSPDFR (без жёсткой зависимости на
компиляции - если LSPDFR не установлен, методы просто молча возвращают false):

- `RequestDriverLicense` / `RequestVehicleRegistration` / `RequestPedId` / `Arrest` -
  вызывают настоящую логику LSPDFR, с фоллбеком на voice line, если LSPDFR не загружен.
- `SetCopAsBusy(ped, true)` - вызывается при спавне **любого** юнита (Partner в
  `PartnerSpawner`, Code2/Code3 в `SpawnDispatcher`) сразу при создании ped'а.
  Без этого LSPDFR может параллельно выдавать нашим юнитам свои задачи, и два
  мода будут дёргать Task одного и того же ped'а одновременно.
- `IsPedAPoliceOfficer` - используется в `TargetingHelper.IsValidSuspectTarget`,
  чтобы Arrest/Force Arrest/Search Ped не могли случайно схватить другого
  офицера вместо подозреваемого.

### Stop The Ped (`Compatibility/StopThePedCompatibility.cs`)
`IsPedBeingHandled(ped)` проверяется в `TargetingHelper.IsValidSuspectTarget`,
которую вызывают `ArrestDispatcher.Arrest/ForceArrest`, `SearchDispatcher.SearchPed`
и все suspect-команды в меню. Если STP прямо сейчас ведёт traffic stop с этим
ped'ом - наши команды тихо отменяются.

**Честно про надёжность детекта**: у STP нет документированной публичной API,
поэтому класс перебирает несколько вероятных имён метода (`IsPedBeingStopped`,
`IsPedStopped`, `IsBeingStopped`, `IsPedHandled`, `IsPedBusy`), статических и
инстансных (`Main.Instance`). Если у твоей версии STP метод называется иначе -
открой её DLL в ILSpy/dnSpy, найди реальное имя, добавь в `MethodNameCandidates`.

### Хард-зависимость vs reflection
Оба моста через reflection специально, чтобы плагин собирался и работал даже
без LSPDFR/STP установленных. Если предпочитаешь жёсткую зависимость - добавь
`<Reference>` на `LSPD First Response.dll` в `.csproj` и замени вызовы в
`LspdfrBridge` на прямые `LSPD_First_Response.Mod.API.Functions.XXX(...)`.

## Что почти наверняка придётся поправить руками

- **`ArrestDispatcher`** - voice line ID (`WarningVoiceContext1/2` в начале
  класса) придуманы по аналогии с именованием GTA V speech-контекстов, не
  взяты из реального дампа файлов игры. Открой OpenIV → audio → speech,
  найди voice set своего ped'а, посмотри реальные контексты.
- **`SpawnDispatcher`** - модели машин (`"riot"`, `"riot2"`, `"fbi2"`) под
  замену на аддон-транспорт при желании. Посадка в 8-местные фургоны уже
  сделана через прямой натив `SET_PED_INTO_VEHICLE` с числовым индексом места
  (не через угадывание `VehicleSeat` enum), это должно работать надёжно.
- **`PartnerSpawner.GetPlayerWeaponInSameCategory`** - копирует любое оружие
  в руках игрока, а не именно из handgun/rifle-слота отдельно - для точного
  соответствия нужно фильтровать `player.Inventory.Weapons` по группе оружия
  через `GET_WEAPONTYPE_GROUP`.
- **RAGENativeUI API** - `MenuPool.AddSubMenu`, `UIMenu.OnMenuOpen`,
  `UIMenuListItem.IndexToItem` и подобное написаны по памяти реального API
  и не проверены против настоящей DLL. Если что-то не найдётся при сборке -
  ищи актуальный аналог в документации/примерах RAGENativeUI, обычно это
  небольшая правка имени метода, а не структурная проблема.

## Реализовано в меню

- **PARTNERS**: New Partner (дефолтный loadout), Select All, чекбоксы юнитов,
  Movement (Go To/Follow Me/Patrol с настраиваемым радиусом/Convoy/Get-Exit
  Vehicle/Regroup/Hold Position/Take Cover/Cover Me), Suspect (Stop Ped/Request
  ID-License-Registration-Exit Vehicle), Arrest (Arrest/Force Arrest/Guard
  Suspect), Search (Ped/Vehicle) - все подключены к реальным диспетчерам.
- **CODE 2 / CODE 3**: кнопки вызова по каждому типу юнита (LSPD/LSSD/State
  Patrol/SWAT/NOOSE/NOOSE SWAT/FIB/FIB HRT/EMS), список активных юнитов с
  чекбоксами, базовые командные (Follow Me/Patrol/Regroup/Hold Position) плюс
  EMS-раздел (Treat Ped/Officer/Suspect, Load/Unload Patient) - обработчики
  сами находят первого выбранного юнита типа Ems среди отмеченных чекбоксами.
- **Go To** - прячет меню, ждёт подтверждения точки клавишей T, не выходя из паузы.

## Не реализовано / известные упрощения

- Экипировка для New Partner - только дефолт, нет UI-пикера handgun/rifle/car
  (сама логика `PartnerLoadout`/`PartnerSpawner` поддерживает кастомные
  пресеты, просто нет меню для их выбора - можно добавить как
  `UIMenuListItem` с вариантами оружия по аналогии с Patrol radius).
- Transport Patient (доставка в госпиталь) есть в `EmsDispatcher`, но кнопки
  в меню нет - нужна отдельная точка назначения, добавляется по аналогии с Go To.
- Convoy - для 2-3 машин рабочая leader-follower цепочка, для больших колонн
  не тестировалось.

## Changelog

### Переход с WPF на RAGENativeUI
Убраны `UI/TabletHost.cs`, `UI/TabletUI.cs`, `UI/Views/TabletWindow.xaml(.cs)` -
весь функционал перенесён в `UI/NativeMenu.cs`. По пути найден и исправлен
реальный баг ещё на этапе написания: `menu.OnItemSelect = null` для сброса
подписки при пересборке меню не скомпилировался бы, если `OnItemSelect` -
настоящий C# `event` (события разрешают только `+=`/`-=` снаружи класса).
Исправлено на подписку один раз в `BuildMenus()` с матчингом пункта по
`item.Text` вместо сравнения по ссылке на пересоздаваемый объект.

### Найденные и исправленные баги (более ранняя ревизия)
- **Спам команд при удержании клавиши.** B/O/T не имели edge-detection - пока
  клавиша была зажата, команды запускались каждый кадр. Добавлено сравнение
  с состоянием предыдущего кадра.
- **Covering-юниты застревали навечно.** После Force Arrest/Search Ped юниты
  в роли covering/guard держали прицел бесконечно. Теперь следят за
  `primary.State` и возвращаются в Idle после завершения.
- **Детект "по нам стреляют" не работал.** `GET_PED_SOURCE_OF_DEATH` реагирует
  только на смерть, не на входящий урон. Заменено на отслеживание падения
  `Health` + поиск ближайшего реально стреляющего ped'а (`IS_PED_SHOOTING`).
- **Неверный натив для road node.** `GET_CLOSEST_VEHICLE_NODE` не возвращает
  heading - нужен `GET_CLOSEST_VEHICLE_NODE_WITH_HEADING`.
- **Race condition в детекте побега при Force Arrest.** Заменено на поллинг
  каждые 300мс вместо разовой проверки сразу после запуска flee-таска.
- **8-местные фургоны сажали пассажиров неправильно** - было угадывание по
  несуществующим значениям `VehicleSeat` enum, заменено на прямой натив
  `SET_PED_INTO_VEHICLE` с числовым индексом места.

## Оптимизация

`Main.cs` держит два независимых цикла:
- **FastLoop** (каждый кадр) - `NativeMenu.Tick()` (обработка меню) и
  `KeyBindings.Tick()` (нужен per-frame ради точного hold-vs-tap на U и
  edge-detection на B/O/T).
- **SlowLoop** (раз в 200мс) - вся AI-логика по юнитам (`CheckUnderFire`/
  `UpdateCoverBehavior`/`WatchdogTick`) + `PurgeDead()`. Эта логика не нуждается
  в частоте 60 раз/сек - при 8+ NOOSE, 8+ SWAT и партнёрах разом гонять её
  каждый кадр реально нагружает CPU без пользы.

Таймеры (`StuckTimer`, `TimeInVehicleCover`) инкрементируются константой шага
SlowLoop (0.2 сек), а не через `Game.FrameTime` - иначе накопление занижалось
бы примерно в 12 раз при переносе логики в более редкий тик.
