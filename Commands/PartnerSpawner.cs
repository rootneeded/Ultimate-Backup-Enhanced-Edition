
using Rage;
using Rage.Native;
using UltimateBackupReplacement.Compatibility;
using UltimateBackupReplacement.Core;
using UltimateBackupReplacement.Units;

namespace UltimateBackupReplacement.Commands
{
    public static class PartnerSpawner
    {
        public static Unit Spawn(
            PartnerLoadout loadout,
            Vector3 position,
            float heading)
        {
            var player = Game.LocalPlayer.Character;

            var pedModel = new Model("s_m_y_cop_01");
            pedModel.LoadAndWait();

            var ped = new Ped(
                pedModel,
                position,
                heading);

            ped.BlockPermanentEvents = true;
            ped.IsPersistent = true;

            var unit = new Unit(
                ped,
                UnitSource.Partner,
                UnitType.Police);

            LspdfrBridge.SetCopAsBusy(ped, true);

            if (loadout.CopyPlayerOutfit)
                CopyOutfitFromPlayer(player, ped);

            ApplyWeapon(ped, loadout.Handgun);
            ApplyWeapon(ped, loadout.Rifle);

            string carModelName = null;

            if (loadout.Car.Option == VehicleOption.CopyFromPlayer &&
                player.IsInVehicle(false))
            {
                carModelName = player.CurrentVehicle.Model.Name;
            }
            else
            {
                carModelName = loadout.Car.SpecificModel;
            }

            if (!string.IsNullOrEmpty(carModelName))
            {
                var carModel = new Model(carModelName);

                if (carModel.IsValid)
                {
                    carModel.LoadAndWait();

                    var spawnPos =
                        position +
                        ped.ForwardVector.ToNormalized() * 3f;

                    var vehicle = new Vehicle(
                        carModel,
                        spawnPos,
                        heading);

                    vehicle.IsPersistent = true;
                    unit.AssignedVehicle = vehicle;
                }
            }

            UnitManager.Register(unit);
            return unit;
        }

        private static void ApplyWeapon(
            Ped ped,
            WeaponChoice choice)
        {
            WeaponHash hash =
                choice.Option == VehicleOption.CopyFromPlayer
                    ? GetPlayerWeaponInSameCategory(choice)
                    : choice.SpecificWeapon;

            if (hash != 0)
                ped.Inventory.GiveNewWeapon(
                    hash,
                    -1,
                    true);
        }

        private static WeaponHash GetPlayerWeaponInSameCategory(
            WeaponChoice choice)
        {
            var player = Game.LocalPlayer.Character;

            var weapon = player.Inventory.EquippedWeapon;

            if (weapon != null)
                return weapon.Hash;

            return choice.SpecificWeapon;
        }

        private static void CopyOutfitFromPlayer(
            Ped player,
            Ped target)
        {
            for (int component = 0; component < 12; component++)
            {
                int drawable =
                    NativeFunction.Natives.GET_PED_DRAWABLE_VARIATION<int>(
                        player,
                        component);

                int texture =
                    NativeFunction.Natives.GET_PED_TEXTURE_VARIATION<int>(
                        player,
                        component);

                NativeFunction.Natives.SET_PED_COMPONENT_VARIATION(
                    target,
                    component,
                    drawable,
                    texture,
                    0);
            }
        }
    }
}

