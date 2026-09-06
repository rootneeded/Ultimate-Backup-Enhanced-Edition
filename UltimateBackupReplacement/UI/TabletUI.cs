using System;
using Rage;

namespace UltimateBackupReplacement.UI
{
    public enum TabletTab { Partners, Code2, Code3 }

    /// <summary>
    /// Состояние экрана планшета. Открытие ставит игру на паузу тем же способом,
    /// что и системное ESC-меню. Сам рендер - в TabletHost/TabletWindow (WPF-окно
    /// поверх игры), этот класс координирует состояние и связывает Go To ->
    /// targeting-mode -> подтверждение точки клавишей T (см. KeyBindings).
    /// </summary>
    public static class TabletUI
    {
        public static bool IsOpen { get; private set; }
        public static bool IsInTargetingMode { get; private set; }
        public static TabletTab CurrentTab { get; private set; } = TabletTab.Partners;

        /// <summary>Действие, которое нужно выполнить с точкой, подтверждённой в targeting-mode.</summary>
        public static Action<Vector3> PendingTargetAction;

        public static void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            CurrentTab = TabletTab.Partners;

            Game.IsPaused = true;
            TabletHost.Show();
        }

        public static void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            IsInTargetingMode = false;
            PendingTargetAction = null;

            TabletHost.Hide();
            Game.IsPaused = false;
        }

        public static void SwitchTab(TabletTab tab) => CurrentTab = tab;

        /// <summary>Go To нажат в UI: прячем окно, мир остаётся на паузе, ждём подтверждения точки.</summary>
        public static void EnterTargetingMode(Action<Vector3> onConfirmed)
        {
            PendingTargetAction = onConfirmed;
            IsInTargetingMode = true;
            TabletHost.Hide();
        }

        /// <summary>Вызывается из KeyBindings по нажатию T во время targeting-mode.</summary>
        public static void ConfirmTarget(Vector3 point)
        {
            if (!IsInTargetingMode) return;
            var action = PendingTargetAction;
            PendingTargetAction = null;
            IsInTargetingMode = false;
            TabletHost.Show();
            action?.Invoke(point);
        }

        public static void CancelTargeting()
        {
            if (!IsInTargetingMode) return;
            PendingTargetAction = null;
            IsInTargetingMode = false;
            TabletHost.Show();
        }
    }
}

