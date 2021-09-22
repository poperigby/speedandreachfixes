using Mutagen.Bethesda;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using System;
using System.Threading.Tasks;
using Mutagen.Bethesda.Plugins.Exceptions;

namespace SpeedandReachFixes
{
    public static class Program
    {
        private static Lazy<Settings> _settings = null!;
        private static Settings Settings => _settings.Value;

        public static Task<int> Main(string[] args)
        {
            return SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetAutogeneratedSettings("Settings", "settings.json", out _settings)
                .SetTypicalOpen(GameRelease.SkyrimSE, "SpeedAndReachFixes.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            Console.WriteLine("\n\nInitialization successful, beginning patcher process...\n");

            // initialize the modified record counter, and add Game Setting changes to the patch.
            var count = Settings.GameSettings.AddGameSettingsToPatch(state);

            if (count > 0) // if game settings were successfully added, write to log
                Console.WriteLine($"Modified {count} Game Setting {(count > 1 ? "s" : "")}.");

            // Apply attack angle modifier for all races, if the modifier isn't set to 0
            if (!Settings.AttackStrikeAngleModifier.Equals(0F))
            {
                try
                {
                    foreach (IRaceGetter race in state.LoadOrder.PriorityOrder.Race().WinningOverrides())
                    { // iterate through all races that have the ActorTypeNPC keyword.
                        if (!race.HasKeyword(Skyrim.Keyword.ActorTypeNPC) || race.EditorID == null)
                            continue; // skip this race if it does not have the ActorTypeNPC keyword

                        var raceCopy = race.DeepCopy();

                        var subrecordChanges = count;
                        foreach (var attack in raceCopy.Attacks)
                        {
                            if (attack.AttackData == null)
                                continue;
                            attack.AttackData.StrikeAngle = Settings.GetModifiedStrikeAngle(attack.AttackData.StrikeAngle);
                            ++count; // iterate counter by one for each modified attack
                        }
                        subrecordChanges = count - subrecordChanges;
                        if (subrecordChanges > 0)
                        {
                            state.PatchMod.Races.Set(raceCopy);
                            Console.WriteLine($"Modified {subrecordChanges} attacks for race: {race.EditorID}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw RecordException.Enrich(ex, weap);
                }
            }

            // Apply speed and reach fixes to all weapons.
            foreach (var weap in state.LoadOrder.PriorityOrder.WinningOverrides<IWeaponGetter>())
            {
                try
                {
                    if (weap.Data == null || weap.EditorID == null)
                        continue;

                    var weapon = weap.DeepCopy(); // copy weap record to temp

                    var (changedReach, changedSpeed) = Settings.ApplyChangesToWeapon(weapon);

                    if (changedReach || changedSpeed)
                    {
                        // if temp record was modified
                        state.PatchMod.Weapons.Set(weapon); // set weap record to temp
                        Console.WriteLine($"Successfully modified weapon: {weap.EditorID}");
                        ++count;
                        if (Settings.PrintWeaponStatsToLog)
                        {
                            Console.Write(
                                $"{(changedReach ? $"\tReach: {weap.Data.Reach}\n" : "")}{(changedSpeed ? $"\tSpeed: {weap.Data.Speed}\n" : "")}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw RecordException.Enrich(ex, weap);
                }
            }

            // Log the total number of records modified by the patcher.
            Console.WriteLine($"\nFinished patching {count} records.\n");
        }
    }
}
