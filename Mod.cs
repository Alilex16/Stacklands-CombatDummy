using HarmonyLib;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CombatDummyLexNS
{
    public class CombatDummyLex : Mod
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Combatable), nameof(Combatable.PerformAttack))]
        public static void Combatable__AttackMissedTracking(Combatable __instance)
        {
            foreach (CombatDummy combatDummy in __instance.AttackTargets)
            {
                if (!__instance.AttackIsHit)
                {
                    combatDummy.TrackingHitsMissed++;
                }
            }
        }
        
        public void Awake()
        {
            Harmony.PatchAll(typeof(CombatDummyLex));

            SokLoc.instance.LoadTermsFromFile(System.IO.Path.Combine(this.Path, "localization.tsv"));
        }

        public override void Ready()
        {
            // Seeking Wisdom(3), Reap&Sow(3), Logic and Reason(1)
            WorldManager.instance.GameDataLoader.AddCardToSetCardBag(SetCardBagType.BasicBuildingIdea, "lex_blueprint_combat_dummy", 1);

            Logger.Log("Combat Dummy Ready!");
        }
    }

    
    public class CombatDummy : Mob
    {
        // public bool fixIdeaDescription = false;
        public bool hasReset = false;

        public float TrackingTimeInTraining;
        public int TrackingHitsDealt;
        public int TrackingHitsMissed;
        public int TrackingHitsBlocked;
        public int TrackingHighestHit;
        public float TrackingAverageHit;
        public float TrackingDPS;
        public int TrackingTotalDamageDone;

        public override bool CanBeDragged => true;

        public override bool CanMove => false;

        protected override bool CanHaveCard(CardData otherCard) => otherCard is Villager || otherCard is Equipable;
        
        public override bool CanBePushedBy(CardData otherCard) { return false; }


        protected override void Awake()
        {
            // fixIdeaDescription = true;
            base.Awake();

            CanAttack = false;
            CanHaveInventory = true;

            BaseCombatStats = new CombatStats() {
                MaxHealth = 99999,
                AttackSpeed = 0,
                HitChance = 0, 
                AttackDamage = 0,
                Defence = 0,
                AttackSpeedIncrement = 0,
                HitChanceIncrement = 0,
                AttackDamageIncrement = 0,
                DefenceIncrement = 0,
                SpecialHits = new List<SpecialHit>(),
            };

            HealthPoints = BaseCombatStats.MaxHealth;

            BaseAttackType = AttackType.None;
        }

        public override void UpdateCard()
        {
            if (MyConflict == null && !hasReset)
            {
                ResetDummy();
            }
            else if (MyConflict != null)
            {
                TrackingTimeInTraining += Time.deltaTime * WorldManager.instance.TimeScale;

                TrackingDPS = (float)TrackingTotalDamageDone / (float)TrackingTimeInTraining;

                if (hasReset)
                {
                    hasReset = false;
                }
            }
            
            base.UpdateCard();
        }

        public override void UpdateCardText()
        {
            // if (!fixIdeaDescription) // so the idea keeps the original description
            // {
            //     return;
            // }

		    GameCard myGameCard = MyGameCard;
            if ((object)myGameCard == null)
            {
                return;
            }


            descriptionOverride = SokLoc.Translate("lex_combat_dummy_description");

            if (MyConflict != null)
            {
                descriptionOverride = GetTrackingDescription();
            }
            else
            {
                if (WorldManager.instance.HoveredCard == MyGameCard)
                {
                    descriptionOverride = SokLoc.Translate("lex_combat_dummy_description");
                }
                else
                {
                    descriptionOverride = SokLoc.Translate("lex_combat_dummy_description_fake");
                }
            }
        }

        private string GetTrackingDescription()
        {
            string newDescription = "";

            CombatStats processedCombatStats = ProcessedCombatStats;
		    string defenceTranslation = processedCombatStats.GetDefenceTranslation();
            
            if (processedCombatStats.Defence == 0)
            {
                defenceTranslation = "None";
            }

            string TrackingDefence = $"{defenceTranslation} ({processedCombatStats.Defence})";

            LocParam defense = LocParam.Create("defense", TrackingDefence);
            
            LocParam time = LocParam.Create("time", TrackingTimeInTraining.ToString("0.00"));
            LocParam hits = LocParam.Create("hits", TrackingHitsDealt.ToString());
            LocParam missed = LocParam.Create("missed", TrackingHitsMissed.ToString());
            LocParam blocked = LocParam.Create("blocked", TrackingHitsBlocked.ToString());
            LocParam highest = LocParam.Create("highest", TrackingHighestHit.ToString());
            LocParam average = LocParam.Create("average", TrackingAverageHit.ToString("0.00"));
            LocParam dps = LocParam.Create("dps", TrackingDPS.ToString("0.00"));
            LocParam total = LocParam.Create("total", TrackingTotalDamageDone.ToString());

            newDescription = SokLoc.Translate("lex_combat_dummy_description_long", defense, time, hits, missed, blocked, highest, average, dps, total);

            if (string.IsNullOrEmpty(newDescription))
            {
                return "";
            }

            return newDescription;
        }

        public override void Damage(int damage)
        {
            TrackingTotalDamageDone += damage;
            TrackingHitsDealt++;

            if (damage > TrackingHighestHit)
            {
                TrackingHighestHit = damage;
            }
            if (damage == 0)
            {
                TrackingHitsBlocked++;
            }

            TrackingAverageHit = (float)TrackingTotalDamageDone / (float)TrackingHitsDealt;

            base.Damage(0);
        }

        public override void Clicked()
        {
		    if (InputController.instance.GetKey(Key.LeftShift) || InputController.instance.GetKey(Key.RightShift))
            {
                ResetDummy();
            }
            else if (InputController.instance.GetKey(Key.LeftAlt) || InputController.instance.GetKey(Key.RightAlt))
            {
                ResetDummyTracking();
            }
            else
            {
                ResetDummyEquipment();
            }

            base.Clicked();
        }

        private void ResetDummy()
        {
		    ResetDummyEquipment();
            ResetDummyTracking();

            hasReset = true;
        }

        private void ResetDummyEquipment()
        {
            List<Equipable> allEquipables = base.GetAllEquipables();
            foreach (Equipable item in allEquipables)
            {
                if (item != null && !string.IsNullOrEmpty(item.Id))
                {
                    MyGameCard.Unequip(item);
                    item.MyGameCard.SendIt();
                }
            }
        }

        private void ResetDummyTracking()
        {
            TrackingTimeInTraining = 0f;
            TrackingHitsDealt = 0;
            TrackingHitsMissed = 0;
            TrackingHitsBlocked = 0;
            TrackingHighestHit = 0;
            TrackingAverageHit = 0.00f;
            TrackingDPS = 0.00f;
            TrackingTotalDamageDone = 0;
        }
    }
}
