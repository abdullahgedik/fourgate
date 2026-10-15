namespace Fourgate.Core.Audio;

/// <summary>Sound effect ids raised by gameplay.</summary>
public static class SfxIds
{
    public const string PlayerDash = "player_dash";
    public const string PlayerHurt = "player_hurt";
    public const string PlayerDeath = "player_death";
    public const string PlayerRevive = "player_revive";
    public const string LevelUp = "level_up";
    public const string UpgradePicked = "upgrade_picked";
    public const string GameOver = "game_over";
    public const string WaveStart = "wave_start";
    public const string WaveEnd = "wave_end";
    public const string DoorWarning = "door_warning";
    public const string CrateDrop = "crate_drop";
    public const string PickupAmmo = "pickup_ammo";
    public const string PickupHealth = "pickup_health";
    public const string PickupUpgrade = "pickup_upgrade";
    public const string PickupWeapon = "pickup_weapon";
    public const string WeaponSwitch = "weapon_switch";
    public const string EquipmentSwitch = "equipment_switch";
    public const string WeaponLevelUp = "weapon_level_up";
    public const string EnemyHit = "enemy_hit";
    public const string EnemyDeath = "enemy_death";
    public const string EnemyTelegraph = "enemy_telegraph";
    public const string EnemyFireball = "enemy_fireball";
    public const string BossTelegraph = "boss_telegraph";
    public const string BossAttack = "boss_attack";
    public const string ArmorHit = "armor_hit";
    public const string WeaponFire = "weapon_fire";
    public const string WeaponThrow = "weapon_throw";
    public const string Explosion = "explosion";
    public const string FireStart = "fire_start";
    public const string MinePlaced = "mine_placed";
    public const string WeaponEmpty = "weapon_empty";
    public const string WeaponReloadStart = "weapon_reload_start";
    public const string WeaponReloadEnd = "weapon_reload_end";

    // Weapons: firing and throwing also have one id per weapon (see WeaponFireFor / WeaponThrowFor).
    public const string BulletRicochet = "bullet_ricochet";
    public const string WeaponUnlocked = "weapon_unlocked";
    public const string BarrelStruck = "barrel_struck";

    // Bosses.
    public const string BossAppear = "boss_appear";
    public const string BossPhase = "boss_phase";
    public const string BossDeath = "boss_death";

    // Run.
    public const string Pause = "pause";
    public const string Resume = "resume";
    public const string WavesSurvived = "waves_survived";
    public const string NewHighScore = "new_high_score";

    // Menus.
    public const string UiMove = "ui_move";
    public const string UiConfirm = "ui_confirm";
    public const string UiBack = "ui_back";

    /// <summary>The fire sound of one weapon, e.g. <c>weapon_fire_shotgun</c>; build it once and keep it.</summary>
    public static string WeaponFireFor(string weaponId)
    {
        return WeaponFire + "_" + weaponId;
    }

    /// <summary>The throw / place sound of one weapon, e.g. <c>weapon_throw_grenade</c>; build it once and keep it.</summary>
    public static string WeaponThrowFor(string weaponId)
    {
        return WeaponThrow + "_" + weaponId;
    }
}
