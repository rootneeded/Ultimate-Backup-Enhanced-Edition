using System;
using System.Linq;
using System.Reflection;
using Rage;

namespace UltimateBackupReplacement.Compatibility
{
    /// <summary>
    /// Stop The Ped - отдельный плагин, обрабатывающий traffic stops. Публичной API он не
    /// выставляет, поэтому ищем через reflection по списку вероятных имён метода/сборки -
    /// у разных версий/форков STP имена могут отличаться, поэтому перебираем несколько
    /// кандидатов вместо жёсткой привязки к одному.
    ///
    /// РЕАЛЬНО ИСПОЛЬЗУЕТСЯ (не просто заготовка): все команды, работающие с конкретным ped'ом
    /// (Stop Ped/Request ID-License-Registration/Arrest/Force Arrest/Search Ped), должны звать
    /// IsPedBeingHandled(target) первым делом и отменять действие, если STP уже ведёт его -
    /// иначе два мода дёргают Task одного и того же ped'а одновременно, что почти гарантированно
    /// ломает анимацию/логику traffic stop у обоих.
    /// </summary>
    public static class StopThePedCompatibility
    {
        private static bool _checked;
        private static Assembly _stpAssembly;
        private static Type _stpMainType;
        private static object _stpInstance; // если API инстансное (Main.Instance), а не статическое

        // Кандидаты имён сборки (без учёта регистра, ищем подстроку)
        private static readonly string[] AssemblyNameCandidates = { "StopThePed", "Stop The Ped", "STP" };

        // Кандидаты имён метода, возвращающего bool по Ped - расширяй по мере проверки у себя
        private static readonly string[] MethodNameCandidates =
        {
            "IsPedBeingStopped", "IsPedStopped", "IsBeingStopped", "IsPedHandled", "IsPedBusy"
        };

        private static void EnsureLoaded()
        {
            if (_checked) return;
            _checked = true;

            _stpAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => AssemblyNameCandidates.Any(c =>
                    a.GetName().Name.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0));

            if (_stpAssembly == null) return;

            _stpMainType = _stpAssembly.GetTypes()
                .FirstOrDefault(t => t.Name.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0);

            if (_stpMainType == null) return;

            // Некоторые плагины держат состояние на статическом инстансе (Main.Instance),
            // а не на статических методах напрямую - пробуем найти такое свойство/поле.
            try
            {
                var instanceProp = _stpMainType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                _stpInstance = instanceProp?.GetValue(null);
            }
            catch { /* нет такого свойства - не критично, попробуем статические методы */ }
        }

        public static bool IsLoaded()
        {
            EnsureLoaded();
            return _stpAssembly != null;
        }

        /// <summary>
        /// Возвращает true, если STP прямо сейчас ведёт traffic stop с этим ped'ом.
        /// Перебирает несколько вероятных имён метода (статических и инстансных) -
        /// при первом успешном вызове результат кэшируется по имени метода, чтобы не
        /// перебирать заново каждый раз. Если ничего не найдено - консервативно false
        /// (не блокируем собственные команды, чтобы не ломать мод при несовпадении версий).
        /// </summary>
        private static MethodInfo _resolvedMethod;
        private static bool _resolvedIsInstance;

        public static bool IsPedBeingHandled(Ped ped)
        {
            EnsureLoaded();
            if (_stpMainType == null || ped == null || !ped.Exists()) return false;

            if (_resolvedMethod == null)
            {
                foreach (var name in MethodNameCandidates)
                {
                    var staticMethod = _stpMainType.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
                    if (staticMethod != null && staticMethod.GetParameters().Length == 1)
                    {
                        _resolvedMethod = staticMethod;
                        _resolvedIsInstance = false;
                        break;
                    }

                    if (_stpInstance != null)
                    {
                        var instanceMethod = _stpMainType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
                        if (instanceMethod != null && instanceMethod.GetParameters().Length == 1)
                        {
                            _resolvedMethod = instanceMethod;
                            _resolvedIsInstance = true;
                            break;
                        }
                    }
                }

                if (_resolvedMethod == null) return false; // ни один кандидат не найден в этой версии STP
            }

            try
            {
                object result = _resolvedIsInstance
                    ? _resolvedMethod.Invoke(_stpInstance, new object[] { ped })
                    : _resolvedMethod.Invoke(null, new object[] { ped });

                return result is bool b && b;
            }
            catch
            {
                return false;
            }
        }
    }
}
