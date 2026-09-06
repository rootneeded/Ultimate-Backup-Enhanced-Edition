using Rage;
using UltimateBackupReplacement.Compatibility;
using UltimateBackupReplacement.Core;
using UltimateBackupReplacement.Units;

namespace UltimateBackupReplacement.Commands
{
    public static class PartnerSpawner
    {
        public static Unit Spawn(PartnerLoadout loadout, Vector3 position, float heading)
        {
            var player = Game.LocalPlayer.Character;

            var pedModel = new Model("s_m_y_cop_01");
            pedModel.LoadAndWait();
            var ped = new Ped(pedModel, position, heading);
            ped.BlockPermanentEvents = true;
            ped.IsPersistent = true;

            var unit = new Unit(ped, UnitSource.Partner, UnitType.Police);
            LspdfrBridge.SetCopAsBusy(ped, true); // LSPDFR больше не трогает этого ped'а как своего

            if (loadout.CopyPlayerOutfit)
                CopyOutfitFromPlayer(player, ped);

            ApplyWeapon(ped, loadout.Handgun);
            ApplyWeapon(ped, loadout.Rifle);

            // Машина спавнится рядом с партнёром и закрепляется как его личная (AssignedVehicle)
            var carModel = loadout.Car.Option == VehicleOption.CopyFromPlayer && player.IsInVehicle()
                ? player.CurrentVehicle.Model
                : loadout.Car.SpecificModel;

            if (carModel.IsValid)
            {
                carModel.LoadAndWait();
                var spawnPos = position + (ped.ForwardVector.ToNormalized() * 3f);
                var vehicle = new Vehicle(carModel, spawnPos);
                unit.AssignedVehicle = vehicle;
            }

            UnitManager.Register(unit);
            return unit;
        }

        private static void ApplyWeapon(Ped ped, WeaponChoice choice)
        {
            WeaponHash hash = choice.Option == WeaponSlotOption.CopyFromPlayer
                ? GetPlayerWeaponInSameCategory(choice)
                : choice.SpecificWeapon;

            if (hash != WeaponHash.Unarmed)
                ped.Inventory.GiveNewWeapon(hash, -1, true);
        }

        private static WeaponHash GetPlayerWeaponInSameCategory(WeaponChoice choice)
        {
            var player = Game.LocalPlayer.Character;
            // Упрощённо берём текущее оружие игрока в руках. Для точного соответствия
            // "именно из хендган-слота" / "именно из rifle-слота" нужно перебрать
            // player.Inventory.Weapons и отфильтровать по WeaponComponent/группе оружия.
            return player.Inventory.EquippedWeapon?.Hash ?? choice.SpecificWeapon;
        }

        private static void CopyOutfitFromPlayer(Ped player, Ped target)
        {
            for (int component = 0; component < 12; component++)
            {
                int drawable = NativeFunction.Natives.GET_PED_DRAWABLE_VARIATION<int>(player, component);
                int texture = NativeFunction.Natives.GET_PED_TEXTURE_VARIATION<int>(player, component);
                NativeFunction.Natives.SET_PED_COMPONENT_VARIATION(target, component, drawable, texture, 0);
            }
        }
    }
}
