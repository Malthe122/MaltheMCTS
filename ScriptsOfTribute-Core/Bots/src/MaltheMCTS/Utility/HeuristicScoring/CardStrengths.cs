using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleBots.src.MaltheMCTS.Utility.HeuristicScoring
{
    public struct CardStrengths
    {
        public double PrestigeStrength = 0;
        public double PowerStrength = 0;
        public double GoldStrength = 0;
        public double AquireTavernStrenth = 0;
        public double SummersetSackingStrength = 0;
        public double DestroyCardStrength = 0;
        public double DrawStrength = 0;
        public double HealStrength = 0;
        public double OpponentDiscardStrength = 0;
        public double IncreasedPatronCallStrength = 0;
        public double ReplaceTavernStrength = 0;
        public double ReturnTopStrength = 0;
        public double TossStrength = 0;
        public double KnockoutStrength = 0;
        public double KnockoutAllStrength = 0;
        public double ReturnAgentTopStrenth = 0;
        public double DonateStrength = 0;

        public CardStrengths()
        {
        }

        public static CardStrengths operator +(CardStrengths a, CardStrengths b)
        {
            return new CardStrengths()
            {
                AquireTavernStrenth = a.AquireTavernStrenth + b.AquireTavernStrenth,
                DestroyCardStrength = a.DestroyCardStrength + b.DestroyCardStrength,
                DonateStrength = a.DonateStrength + b.DonateStrength,
                DrawStrength = a.DrawStrength + b.DrawStrength,
                GoldStrength = a.GoldStrength + b.GoldStrength,
                HealStrength = a.HealStrength + b.HealStrength,
                IncreasedPatronCallStrength = a.IncreasedPatronCallStrength + b.IncreasedPatronCallStrength,
                KnockoutAllStrength = a.KnockoutAllStrength + b.KnockoutAllStrength,
                KnockoutStrength = a.KnockoutStrength + b.KnockoutStrength,
                OpponentDiscardStrength = a.OpponentDiscardStrength + b.OpponentDiscardStrength,
                PowerStrength = a.PowerStrength + b.PowerStrength,
                PrestigeStrength = a.PrestigeStrength + b.PrestigeStrength,
                ReplaceTavernStrength = a.ReplaceTavernStrength + b.ReplaceTavernStrength,
                ReturnAgentTopStrenth = a.ReturnAgentTopStrenth + b.ReturnAgentTopStrenth,
                ReturnTopStrength = a.ReturnTopStrength + b.ReturnTopStrength,
                SummersetSackingStrength = a.SummersetSackingStrength + b.SummersetSackingStrength,
                TossStrength = a.TossStrength + b.TossStrength
            };
        }

        public static CardStrengths operator *(CardStrengths a, double multiplier)
        {
            return new CardStrengths()
            {
                 AquireTavernStrenth = a.AquireTavernStrenth * multiplier,
                 DestroyCardStrength = a.DestroyCardStrength * multiplier,
                 DonateStrength = a.DonateStrength * multiplier,
                 DrawStrength = a.DrawStrength * multiplier,
                 GoldStrength = a.GoldStrength * multiplier,
                 HealStrength = a.HealStrength * multiplier,
                 IncreasedPatronCallStrength = a.IncreasedPatronCallStrength * multiplier,
                 KnockoutAllStrength = a.KnockoutAllStrength * multiplier,
                 KnockoutStrength = a.KnockoutStrength * multiplier,
                 OpponentDiscardStrength = a.OpponentDiscardStrength * multiplier,
                 PowerStrength = a.PowerStrength * multiplier,
                 PrestigeStrength = a.PrestigeStrength * multiplier,
                 ReplaceTavernStrength = a.ReplaceTavernStrength * multiplier,
                 ReturnAgentTopStrenth = a.ReturnAgentTopStrenth * multiplier,
                 ReturnTopStrength = a.ReturnTopStrength * multiplier,
                 SummersetSackingStrength = a.SummersetSackingStrength * multiplier,
                 TossStrength = a.TossStrength * multiplier
            };
        }

        public static CardStrengths operator /(CardStrengths a, int divisor)
        {
            return new CardStrengths()
            {
                AquireTavernStrenth = a.AquireTavernStrenth / divisor,
                DestroyCardStrength = a.DestroyCardStrength / divisor,
                DonateStrength = a.DonateStrength / divisor,
                DrawStrength = a.DrawStrength / divisor,
                GoldStrength = a.GoldStrength / divisor,
                HealStrength = a.HealStrength / divisor,
                IncreasedPatronCallStrength = a.IncreasedPatronCallStrength / divisor,
                KnockoutAllStrength = a.KnockoutAllStrength / divisor,
                KnockoutStrength = a.KnockoutStrength / divisor,
                OpponentDiscardStrength = a.OpponentDiscardStrength / divisor,
                PowerStrength = a.PowerStrength / divisor,
                PrestigeStrength = a.PrestigeStrength / divisor,
                ReplaceTavernStrength = a.ReplaceTavernStrength / divisor,
                ReturnAgentTopStrenth = a.ReturnAgentTopStrenth / divisor,
                ReturnTopStrength = a.ReturnTopStrength / divisor,
                SummersetSackingStrength = a.SummersetSackingStrength / divisor,
                TossStrength = a.TossStrength / divisor
            };
        }
    }
}
