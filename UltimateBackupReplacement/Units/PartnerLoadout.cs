using Rage;
using UltimateBackupReplacement.Core;

namespace UltimateBackupReplacement.Units
{
    public class WeaponChoice
    {
        public WeaponSlotOption Option;
        public WeaponHash SpecificWeapon;

        public static WeaponChoice CopyFromPlayer() =>
            new WeaponChoice { Option = WeaponSlotOption.CopyFromPlayer };

        public static WeaponChoice Fixed(WeaponHash hash) =>
            new WeaponChoice { Option = WeaponSlotOption.Specific, SpecificWeapon = hash };
    }

    public class VehicleChoice
    {
        public VehicleOption Option;
        public Model SpecificModel;

        public static VehicleChoice CopyFromPlayer() =>
            new VehicleChoice { Option = VehicleOption.CopyFromPlayer };

        public static VehicleChoice Fixed(Model model) =>
            new VehicleChoice { Option = VehicleOption.Specific, SpecificModel = model };
    }

    /// <summary>
    /// Пресет экипировки партнёра, применяется один раз при спавне.
    /// </summary>
    public class PartnerLoadout
    {
        public WeaponChoice Handgun = WeaponChoice.Fixed(WeaponHash.Pistol);
        public WeaponChoice Rifle = WeaponChoice.Fixed(WeaponHash.CarbineRifle);
        public VehicleChoice Car = VehicleChoice.Fixed(new Model("police"));
        public bool CopyPlayerOutfit = false;

        /// <summary>
        /// Список известных моделей машин для UI-пикера (LSPD Patrol, LSSD Patrol, FIB1/2, LSPD Riot, LSPD Transporter).
        /// Замените строковые имена на актуальные для вашего набора аддон-машин/ванильных моделей.
        /// </summary>
        public static readonly (string Label, string ModelName)[] KnownCarPresets =
        {
            ("LSPD Patrol",     "police"),
            ("LSSD Patrol",     "sheriff"),
            ("FIB 1",           "fbi"),
            ("FIB 2",           "fbi2"),
            ("LSPD Riot",       "riot"),
            ("LSPD Transporter","pbus"),
        };
    }
}
