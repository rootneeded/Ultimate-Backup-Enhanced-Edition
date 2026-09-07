using System;
using System.Linq;
using System.Reflection;
using Rage;

namespace UltimateBackupReplacement.Compatibility
{
    /// <summary>
    /// Обёртка над LSPD First Response SDK (LSPD_First_Response.Mod.API.Functions).
    /// Используем reflection вместо жёсткой сборочной зависимости, чтобы:
    ///  1) не заставлять проект падать на компиляции, если LSPDFR.dll ещё не подложена в lib/;
    ///  2) не привязываться к конкретной версии сигнатуры намертво - если у автора LSPDFR
    ///     сменится подпись метода, мы просто тихо не найдём метод и упадём в safe-фоллбек,
    ///     а не сломаем сборку всего плагина.
    ///
    /// Если предпочитаешь жёсткую зависимость (что нормально для RPH-плагинов, большинство
    /// так и делает) - добавь референс на "LSPD First Response.dll" в .csproj и замени вызовы
    /// ниже на прямые: LSPD_First_Response.Mod.API.Functions.RequestDriverLicense(ped);
    /// Так будет и быстрее, и подскажет ошибку сразу на компиляции, а не в рантайме.
    /// </summary>
    public static class LspdfrBridge
    {
        private static bool _checked;
        private static Type _functionsType;

        private static void EnsureLoaded()
        {
            if (_checked) return;
            _checked = true;

            var lspdfrAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name.IndexOf("LSPD_First_Response", StringComparison.OrdinalIgnoreCase) >= 0
                                   || a.GetName().Name.IndexOf("LSPD First Response", StringComparison.OrdinalIgnoreCase) >= 0);

            _functionsType = lspdfrAssembly?.GetType("LSPD_First_Response.Mod.API.Functions");
        }

        public static bool IsLoaded()
        {
            EnsureLoaded();
            return _functionsType != null;
        }

        public static bool RequestDriverLicense(Ped ped)
        {
            EnsureLoaded();
            return TryInvoke("RequestDriverLicense", ped);
        }

        public static bool RequestVehicleRegistration(Ped ped)
        {
            EnsureLoaded();
            return TryInvoke("RequestVehicleRegistration", ped);
        }

        public static bool RequestPedId(Ped ped)
        {
            EnsureLoaded();
            return TryInvoke("RequestPedID", ped);
        }

        public static bool Arrest(Ped officer, Ped suspect)
        {
            EnsureLoaded();
            return TryInvoke("Arrest", officer, suspect);
        }

        /// <summary>
        /// Помечает ped'а как "занятого" для LSPDFR - его собственная система backup/патрулей
        /// больше не будет пытаться выдавать ему задачи или засчитывать его как доступного для
        /// авто-вызова. Обязательно вызывать при регистрации ЛЮБОГО юнита (Partner/Code2/Code3) -
        /// иначе LSPDFR и этот мод будут дёргать одного и того же ped'а параллельно.
        /// </summary>
        public static bool SetCopAsBusy(Ped ped, bool busy)
        {
            EnsureLoaded();
            return TryInvoke("SetCopAsBusy", ped, busy);
        }

        /// <summary>Используется, чтобы Arrest/Force Arrest/Search Ped не могли случайно
        /// схватить aimed/nearest ped'а, который на самом деле является другим офицером
        /// (например если игрок целится в толпу возле места ареста).</summary>
        public static bool IsPedAPoliceOfficer(Ped ped)
        {
            EnsureLoaded();
            if (_functionsType == null || ped == null || !ped.Exists()) return false;

            try
            {
                var method = _functionsType.GetMethod("IsPedAPoliceOfficer", BindingFlags.Public | BindingFlags.Static);
                if (method == null) return false;
                var result = method.Invoke(null, new object[] { ped });
                return result is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryInvoke(string methodName, params object[] args)
        {
            if (_functionsType == null) return false;

            try
            {
                var method = _functionsType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (method == null) return false;

                method.Invoke(null, args);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
