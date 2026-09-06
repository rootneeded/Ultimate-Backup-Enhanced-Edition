using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace UltimateBackupReplacement.UI
{
    /// <summary>
    /// RPH запускает плагин не на STA-потоке, а WPF требует STA - поэтому окно живёт
    /// на отдельном выделенном потоке со своим Dispatcher. Show()/Hide() дергают из
    /// игрового потока (GameFiber), поэтому реальную работу с окном всегда маршалим
    /// через Dispatcher.Invoke/BeginInvoke на поток окна.
    /// </summary>
    public static class TabletHost
    {
        private static Thread _thread;
        private static Application _app;
        private static TabletWindow _window;
        private static readonly object Lock = new object();

        private static void EnsureStarted()
        {
            lock (Lock)
            {
                if (_thread != null) return;

                var ready = new ManualResetEventSlim(false);

                _thread = new Thread(() =>
                {
                    _app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    _window = new TabletWindow();
                    _window.Hide(); // создаём, но не показываем сразу

                    ready.Set();
                    _app.Run();
                });

                _thread.SetApartmentState(ApartmentState.STA);
                _thread.IsBackground = true;
                _thread.Start();

                ready.Wait();

                // На случай hot-reload плагина в RPH (или выхода из игры) - гасим STA-поток
                // и WPF Application явно, иначе поток может повиснуть фоном.
                AppDomain.CurrentDomain.ProcessExit += (s, e) => Shutdown();
            }
        }

        public static void Shutdown()
        {
            if (_app == null) return;
            try
            {
                _app.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _window?.Close();
                    _app.Shutdown();
                }));
            }
            catch { /* поток уже мог завершиться - не критично */ }
        }

        public static void Show()
        {
            EnsureStarted();
            _window.Dispatcher.BeginInvoke(new Action(() =>
            {
                _window.RefreshAllTabs();
                _window.Show();
                _window.Activate();
            }));
        }

        public static void Hide()
        {
            if (_window == null) return;
            _window.Dispatcher.BeginInvoke(new Action(() => _window.Hide()));
        }

        /// <summary>Вызвать из главного игрового тика, если нужно периодически обновлять список юнитов
        /// пока окно открыто (например если юниты гибнут/меняют статус в фоне).</summary>
        public static void RefreshIfOpen()
        {
            if (_window == null || !_window.IsVisible) return;
            _window.Dispatcher.BeginInvoke(new Action(() => _window.RefreshAllTabs()));
        }
    }
}
