using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Rage;
using UltimateBackupReplacement.Commands;
using UltimateBackupReplacement.Compatibility;
using UltimateBackupReplacement.Core;
using UltimateBackupReplacement.Units;

namespace UltimateBackupReplacement.UI
{
    /// <summary>Отображаемая строка в списке юнитов (обёртка над Unit для ListBox).</summary>
    public class UnitListItem
    {
        public Unit Unit;
        public string Label => $"{Unit.Type} #{Unit.Id.ToString().Substring(0, 4)}  [{Unit.Status}]  {Unit.Lifecycle}";
        public override string ToString() => Label;

        /// <summary>Цвет индикатора статуса - зелёный/серый Idle, синий - занят задачей,
        /// янтарный - выполняет команду (арест/обыск/лечение), красный - бой.</summary>
        public System.Windows.Media.Brush StatusColor
        {
            get
            {
                switch (Unit.State)
                {
                    case UnitState.SeekingCover:
                    case UnitState.InCover:
                    case UnitState.CombatOpen:
                    case UnitState.OpenFire:
                        return System.Windows.Media.Brushes.OrangeRed;

                    case UnitState.RunningToTakedown:
                    case UnitState.TaseringSuspect:
                    case UnitState.Searching:
                    case UnitState.Treating:
                    case UnitState.LoadingPatient:
                    case UnitState.Transporting:
                    case UnitState.CoveringAimed:
                    case UnitState.Guarding:
                        return System.Windows.Media.Brushes.Goldenrod;

                    case UnitState.Idle:
                        return System.Windows.Media.Brushes.MediumSeaGreen;

                    default:
                        return System.Windows.Media.Brushes.SteelBlue;
                }
            }
        }
    }

    public partial class TabletWindow : Window
    {
        private readonly System.Windows.Threading.DispatcherTimer _clockTimer;

        public TabletWindow()
        {
            InitializeComponent();

            _clockTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();

            InitLoadoutCombos();
        }

        // ---------------------------------------------------------------
        // New Partner
        // ---------------------------------------------------------------

        private readonly List<(string Label, WeaponChoice Choice)> _handgunOptions = new List<(string, WeaponChoice)>
        {
            ("Copy from player", WeaponChoice.CopyFromPlayer()),
            ("Pistol", WeaponChoice.Fixed(WeaponHash.Pistol)),
        };

        private readonly List<(string Label, WeaponChoice Choice)> _rifleOptions = new List<(string, WeaponChoice)>
        {
            ("Copy from player", WeaponChoice.CopyFromPlayer()),
            ("Carbine Rifle", WeaponChoice.Fixed(WeaponHash.CarbineRifle)),
        };

        private void InitLoadoutCombos()
        {
            CmbHandgun.ItemsSource = _handgunOptions.Select(o => o.Label).ToList();
            CmbHandgun.SelectedIndex = 0;

            CmbRifle.ItemsSource = _rifleOptions.Select(o => o.Label).ToList();
            CmbRifle.SelectedIndex = 0;

            var carLabels = new List<string> { "Copy from player" };
            carLabels.AddRange(PartnerLoadout.KnownCarPresets.Select(p => p.Label));
            CmbCar.ItemsSource = carLabels;
            CmbCar.SelectedIndex = 0;
        }

        private void BtnCreatePartner_Click(object sender, RoutedEventArgs e)
        {
            var loadout = new PartnerLoadout
            {
                Handgun = _handgunOptions[CmbHandgun.SelectedIndex].Choice,
                Rifle = _rifleOptions[CmbRifle.SelectedIndex].Choice,
                CopyPlayerOutfit = ChkCopyOutfit.IsChecked == true
            };

            if (CmbCar.SelectedIndex == 0)
            {
                loadout.Car = VehicleChoice.CopyFromPlayer();
            }
            else
            {
                var preset = PartnerLoadout.KnownCarPresets[CmbCar.SelectedIndex - 1];
                loadout.Car = VehicleChoice.Fixed(new Model(preset.ModelName));
            }

            var player = Game.LocalPlayer.Character;
            // Спавним чуть в стороне от игрока, чтобы не телепортироваться друг в друга.
            var spawnPos = player.Position + (player.RightVector * 2.5f);
            PartnerSpawner.Spawn(loadout, spawnPos, player.Heading);

            RefreshAllTabs();
        }

        // ---------------------------------------------------------------
        // Вкладки
        // ---------------------------------------------------------------

        private void BtnTabPartners_Click(object sender, RoutedEventArgs e) => SwitchTab(TabletTab.Partners);
        private void BtnTabCode2_Click(object sender, RoutedEventArgs e) => SwitchTab(TabletTab.Code2);
        private void BtnTabCode3_Click(object sender, RoutedEventArgs e) => SwitchTab(TabletTab.Code3);

        private void SwitchTab(TabletTab tab)
        {
            TabletUI.SwitchTab(tab);

            PartnersPanel.Visibility = tab == TabletTab.Partners ? Visibility.Visible : Visibility.Collapsed;
            Code2Panel.Visibility = tab == TabletTab.Code2 ? Visibility.Visible : Visibility.Collapsed;
            Code3Panel.Visibility = tab == TabletTab.Code3 ? Visibility.Visible : Visibility.Collapsed;

            BtnTabPartners.Style = (Style)FindResource(tab == TabletTab.Partners ? "NavButtonActiveStyle" : "NavButtonStyle");
            BtnTabCode2.Style = (Style)FindResource(tab == TabletTab.Code2 ? "NavButtonActiveStyle" : "NavButtonStyle");
            BtnTabCode3.Style = (Style)FindResource(tab == TabletTab.Code3 ? "NavButtonActiveStyle" : "NavButtonStyle");

            RefreshAllTabs();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => TabletUI.Close();

        // ---------------------------------------------------------------
        // Обновление содержимого
        // ---------------------------------------------------------------

        public void RefreshAllTabs()
        {
            RefreshUnitList(PartnersUnitsList, UnitManager.Partners);
            RefreshUnitList(Code2UnitsList, UnitManager.Code2Units);
            RefreshUnitList(Code3UnitsList, UnitManager.Code3Units);

            BtnTabPartners.Content = $"PARTNERS ({UnitManager.Partners.Count()})";
            BtnTabCode2.Content = $"CODE 2 ({UnitManager.Code2Units.Count()})";
            BtnTabCode3.Content = $"CODE 3 ({UnitManager.Code3Units.Count()})";

            RefreshCallPanel(Code2CallPanel, UnitSource.Code2);
            RefreshCallPanel(Code3CallPanel, UnitSource.Code3);

            RefreshContextualCommands(Code2CommandsPanel, UnitSource.Code2);
            RefreshContextualCommands(Code3CommandsPanel, UnitSource.Code3);
        }

        private void RefreshUnitList(ListBox listBox, IEnumerable<Unit> units)
        {
            var items = units.Select(u => new UnitListItem { Unit = u }).ToList();
            listBox.ItemsSource = items;
            foreach (var item in items)
                if (item.Unit.IsSelected)
                    listBox.SelectedItems.Add(item);
        }

        private static readonly (UnitType Type, string Label)[] CallableUnitTypes =
        {
            (UnitType.LspdPatrol, "LSPD PATROL"),
            (UnitType.LssdPatrol, "LSSD PATROL"),
            (UnitType.StatePatrol, "STATE PATROL"),
            (UnitType.Swat, "SWAT"),
            (UnitType.Noose, "NOOSE"),
            (UnitType.NooseSwat, "NOOSE SWAT"),
            (UnitType.Fib, "FIB"),
            (UnitType.FibHrt, "FIB HRT"),
            (UnitType.Ems, "EMS"),
        };

        private void RefreshCallPanel(WrapPanel panel, UnitSource source)
        {
            if (panel.Children.Count > 0) return; // строим один раз, дальше просто переиспользуем

            foreach (var (type, label) in CallableUnitTypes)
            {
                var btn = new Button
                {
                    Content = label,
                    Style = (Style)FindResource("CallButtonStyle"),
                    Tag = type
                };
                btn.Click += (s, e) => SpawnDispatcher.Dispatch((UnitType)((Button)s).Tag, source);
                panel.Children.Add(btn);
            }
        }

        /// <summary>Команды под Code2/Code3 зависят от типа выбранных юнитов (contextual commands из концепта).</summary>
        private void RefreshContextualCommands(WrapPanel panel, UnitSource source)
        {
            panel.Children.Clear();

            var selected = UnitManager.Selected(source).ToList();
            if (selected.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "Select a unit first",
                    Foreground = (System.Windows.Media.Brush)FindResource("TextMuted"),
                    Margin = new Thickness(4)
                });
                return;
            }

            var firstType = selected[0].Type;
            bool allSameType = selected.All(u => u.Type == firstType);

            AddCmd(panel, "GO TO", () => StartGoTo(selected));
            AddCmd(panel, "FOLLOW ME", () => MovementDispatcher.FollowMe(selected));
            AddCmd(panel, "PATROL [100M]", () => MovementDispatcher.Patrol(selected));
            AddCmd(panel, "REGROUP", () => MovementDispatcher.Regroup(selected));
            AddCmd(panel, "HOLD POSITION", () => MovementDispatcher.HoldPosition(selected));

            if (!allSameType) return; // остальное - только если все выбранные одного типа

            if (firstType == UnitType.Ems)
            {
                AddCmd(panel, "TREAT PED", () =>
                {
                    var patient = TargetingHelper.GetAimedOrNearestPed();
                    if (patient != null) EmsDispatcher.Treat(selected[0], patient);
                });
                AddCmd(panel, "TREAT OFFICER", () =>
                {
                    var officer = FindNearestOfficerPed(selected[0].Ped.Position);
                    if (officer != null) EmsDispatcher.Treat(selected[0], officer);
                });
                AddCmd(panel, "TREAT SUSPECT", () =>
                {
                    var suspect = FindNearestCuffedPed(selected[0].Ped.Position) ?? TargetingHelper.GetAimedOrNearestPed();
                    if (suspect != null) EmsDispatcher.Treat(selected[0], suspect);
                });
                AddCmd(panel, "LOAD PATIENT", () =>
                {
                    var patient = TargetingHelper.GetAimedOrNearestPed();
                    var ambulance = selected[0].Vehicle;
                    if (patient != null && ambulance != null) EmsDispatcher.LoadPatient(selected[0], patient, ambulance);
                });
                AddCmd(panel, "UNLOAD PATIENT", () =>
                {
                    var patient = TargetingHelper.GetAimedOrNearestPed();
                    if (patient != null) EmsDispatcher.UnloadPatient(patient);
                });
                AddCmd(panel, "TRANSPORT PATIENT", () =>
                {
                    var driverUnit = selected.FirstOrDefault(u => u.Status == UnitStatus.InVehicle) ?? selected[0];
                    TabletUI.EnterTargetingMode(point => EmsDispatcher.TransportPatient(driverUnit, point));
                });
            }
            else
            {
                AddCmd(panel, "SEARCH PED", () =>
                {
                    var target = TargetingHelper.GetAimedOrNearestPed();
                    if (target != null) SearchDispatcher.SearchPed(selected, target);
                });
                AddCmd(panel, "TAKE COVER", () => MovementDispatcher.TakeCover(selected));
            }
        }

        private void AddCmd(WrapPanel panel, string label, Action action)
        {
            var btn = new Button { Content = label, Style = (Style)FindResource("CommandButtonStyle") };
            btn.Click += (s, e) => action();
            panel.Children.Add(btn);
        }

        /// <summary>Treat Officer - цель обязательно один из наших управляемых юнитов
        /// (Partner/Code2/Code3), а не случайный ped рядом.</summary>
        private Ped FindNearestOfficerPed(Vector3 from, float radius = 30f)
        {
            return UnitManager.AllUnits
                .Where(u => u.IsAlive && u.Ped.Position.DistanceTo(from) <= radius)
                .OrderBy(u => u.Ped.Position.DistanceTo(from))
                .Select(u => u.Ped)
                .FirstOrDefault();
        }

        /// <summary>Treat Suspect - ищет ближайшего закованного (арестованного) ped'а в радиусе,
        /// а не просто любого ближайшего - иначе EMS будет "лечить" случайных прохожих под видом
        /// suspect'а.</summary>
        private Ped FindNearestCuffedPed(Vector3 from, float radius = 30f)
        {
            Ped nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var ped in World.GetAllPeds())
            {
                if (ped == null || !ped.Exists()) continue;
                float dist = ped.Position.DistanceTo(from);
                if (dist > radius) continue;

                bool isCuffed = NativeFunction.Natives.IS_PED_CUFFED<bool>(ped);
                if (!isCuffed) continue;

                if (dist < nearestDist) { nearestDist = dist; nearest = ped; }
            }

            return nearest;
        }

        // ---------------------------------------------------------------
        // Selection sync (Partners list -> Unit.IsSelected)
        // ---------------------------------------------------------------

        private void PartnersUnitsList_SelectionChanged(object sender, SelectionChangedEventArgs e) => SyncSelection(PartnersUnitsList, UnitManager.Partners);
        private void Code2UnitsList_SelectionChanged(object sender, SelectionChangedEventArgs e) { SyncSelection(Code2UnitsList, UnitManager.Code2Units); RefreshContextualCommands(Code2CommandsPanel, UnitSource.Code2); }
        private void Code3UnitsList_SelectionChanged(object sender, SelectionChangedEventArgs e) { SyncSelection(Code3UnitsList, UnitManager.Code3Units); RefreshContextualCommands(Code3CommandsPanel, UnitSource.Code3); }

        private void SyncSelection(ListBox listBox, IEnumerable<Unit> allUnitsOfSource)
        {
            var selectedUnits = listBox.SelectedItems.Cast<UnitListItem>().Select(i => i.Unit).ToHashSet();
            foreach (var u in allUnitsOfSource)
                u.IsSelected = selectedUnits.Contains(u);
        }

        private void BtnSelectAllPartners_Click(object sender, RoutedEventArgs e)
        {
            UnitManager.SelectAll(UnitSource.Partner);
            RefreshUnitList(PartnersUnitsList, UnitManager.Partners);
        }

        private List<Unit> SelectedPartners() => UnitManager.Selected(UnitSource.Partner).ToList();

        // ---------------------------------------------------------------
        // Partners: Movement
        // ---------------------------------------------------------------

        private void StartGoTo(List<Unit> units)
        {
            if (units.Count == 0) return;
            TabletUI.EnterTargetingMode(point => MovementDispatcher.Go(units, point));
        }

        private void BtnGoTo_Click(object sender, RoutedEventArgs e) => StartGoTo(SelectedPartners());
        private void BtnFollowMe_Click(object sender, RoutedEventArgs e) => MovementDispatcher.FollowMe(SelectedPartners());
        private void BtnPatrol_Click(object sender, RoutedEventArgs e) =>
            MovementDispatcher.Patrol(SelectedPartners(), (float)PatrolRadiusSlider.Value);

        private void PatrolRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PatrolRadiusLabel != null)
                PatrolRadiusLabel.Text = $"{(int)e.NewValue}m";
        }
        private void BtnConvoy_Click(object sender, RoutedEventArgs e) => MovementDispatcher.Convoy(SelectedPartners());
        private void BtnRegroup_Click(object sender, RoutedEventArgs e) => MovementDispatcher.Regroup(SelectedPartners());
        private void BtnHoldPosition_Click(object sender, RoutedEventArgs e) => MovementDispatcher.HoldPosition(SelectedPartners());
        private void BtnTakeCover_Click(object sender, RoutedEventArgs e) => MovementDispatcher.TakeCover(SelectedPartners());
        private void BtnCoverMe_Click(object sender, RoutedEventArgs e) => GuardDispatcher.CoverMe(SelectedPartners());

        private void BtnGetInVehicle_Click(object sender, RoutedEventArgs e)
        {
            foreach (var u in SelectedPartners())
                MovementDispatcher.GetInVehicle(u);
        }

        private void BtnExitVehicle_Click(object sender, RoutedEventArgs e)
        {
            foreach (var u in SelectedPartners().Where(u => u.Status == UnitStatus.InVehicle))
                MovementDispatcher.ExitVehicle(u);
        }

        // ---------------------------------------------------------------
        // Partners: Suspect commands (используют LSPDFR Functions API в реальном проекте -
        // здесь показаны нативные заглушки/направление к первому выбранному юниту)
        // ---------------------------------------------------------------

        private void BtnStopPed_Click(object sender, RoutedEventArgs e)
        {
            var unit = SelectedPartners().FirstOrDefault();
            var target = TargetingHelper.GetAimedOrNearestPed();
            if (unit == null || !TargetingHelper.IsValidSuspectTarget(target)) return;
            unit.Ped.Tasks.FollowNavigationMeshToPosition(target.Position, target.Heading, 2f);
        }

        private void BtnRequestId_Click(object sender, RoutedEventArgs e) => RunSuspectRequest(t => LspdfrBridge.RequestPedId(t), "REQUESTED_ID");
        private void BtnRequestLicense_Click(object sender, RoutedEventArgs e) => RunSuspectRequest(t => LspdfrBridge.RequestDriverLicense(t), "REQUESTED_LICENSE");
        private void BtnRequestRegistration_Click(object sender, RoutedEventArgs e) => RunSuspectRequest(t => LspdfrBridge.RequestVehicleRegistration(t), "REQUESTED_REGISTRATION");
        private void BtnRequestExitVehicle_Click(object sender, RoutedEventArgs e)
        {
            var target = TargetingHelper.GetAimedOrNearestPed();
            if (!TargetingHelper.IsValidSuspectTarget(target)) return;
            target.Tasks.LeaveVehicle();
        }

        /// <summary>
        /// Пытается выполнить настоящий LSPDFR-запрос (Functions.RequestDriverLicense и т.п.).
        /// Если LSPDFR не загружен (плагин запущен без него, например для теста) - фоллбек
        /// на voice line, чтобы хоть какая-то обратная связь была, а не тишина.
        /// </summary>
        private void RunSuspectRequest(Func<Ped, bool> lspdfrCall, string fallbackVoiceContext)
        {
            var unit = SelectedPartners().FirstOrDefault();
            var target = TargetingHelper.GetAimedOrNearestPed();
            if (unit == null || !TargetingHelper.IsValidSuspectTarget(target)) return;

            bool handled = lspdfrCall(target);
            if (!handled)
                NativeFunction.Natives.PLAY_PED_AMBIENT_SPEECH_NATIVE(unit.Ped, fallbackVoiceContext, "SPEECH_PARAMS_STANDARD");
        }

        // ---------------------------------------------------------------
        // Partners: Arrest / Search
        // ---------------------------------------------------------------

        private void BtnArrest_Click(object sender, RoutedEventArgs e)
        {
            var unit = SelectedPartners().FirstOrDefault();
            var target = TargetingHelper.GetAimedOrNearestPed();
            if (unit != null && target != null) ArrestDispatcher.Arrest(unit, target);
        }

        private void BtnForceArrest_Click(object sender, RoutedEventArgs e)
        {
            var units = SelectedPartners();
            var target = TargetingHelper.GetAimedOrNearestPed();
            if (units.Count > 0 && target != null) ArrestDispatcher.ForceArrest(units, target);
        }

        private void BtnGuardSuspect_Click(object sender, RoutedEventArgs e)
        {
            var unit = SelectedPartners().FirstOrDefault();
            var target = TargetingHelper.GetAimedOrNearestPed();
            if (unit != null && target != null) GuardDispatcher.GuardSuspect(unit, target);
        }

        private void BtnSearchPed_Click(object sender, RoutedEventArgs e)
        {
            var units = SelectedPartners();
            var target = TargetingHelper.GetAimedOrNearestPed();
            if (units.Count > 0 && target != null) SearchDispatcher.SearchPed(units, target);
        }

        private void BtnSearchVehicle_Click(object sender, RoutedEventArgs e)
        {
            var units = SelectedPartners();
            var target = TargetingHelper.GetAimedOrNearestVehicle();
            if (units.Count > 0 && target != null) SearchDispatcher.SearchVehicle(units, target);
        }
    }
}
