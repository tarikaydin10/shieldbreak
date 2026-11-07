using Unity.Mathematics;

namespace Shieldbreak.Sim.AI
{
    /// <summary>One side of a melee forecast: a formation, or everything a commander has.</summary>
    public struct LanchesterSide
    {
        public float Soldiers;
        /// <summary>Soldiers weighed by how much each is worth (1 = a line soldier at full morale).</summary>
        public float Strength;
        /// <summary>Width of the front (m).</summary>
        public float Front;
        /// <summary>Soldiers standing in that front.</summary>
        public float FrontSoldiers;

        public void AddSoldier(float strength)
        {
            Soldiers += 1f;
            Strength += strength;
        }

        public void AddFront(float width, float frontSoldiers)
        {
            Front += width;
            FrontSoldiers += frontSoldiers;
        }
    }

    public struct LanchesterForecast
    {
        /// <summary>Own fighting power per enemy fighting power: above 1 the own side should win.</summary>
        public float Balance;
        /// <summary>Share of the own soldiers lost until one side is gone (1 = all, the own side loses).</summary>
        public float OwnLossShare;
        /// <summary>How long that takes (s), counted from contact.</summary>
        public float Seconds;
    }

    /// <summary>
    /// Melee forecast by Lanchester's linear law (CLAUDE.md 3.7): only the soldiers in contact fight, the ranks behind
    /// replace them, so each side loses soldiers at a constant rate. A side of S soldiers worth α each, with c in contact,
    /// has fighting power S·α·c; the larger power wins and the winner loses power(loser) / power(winner) of its soldiers.
    /// Contact is the shared width times the density of the front; what overlaps the enemy's front counts in part, as
    /// flanking soldiers. Rough on purpose: morale, reaches and the march are not in it. A commander's estimate.
    /// </summary>
    public static class Lanchester
    {
        const float MaxBalance = 10f;

        /// <summary>Soldiers of <paramref name="a"/> in contact with <paramref name="b"/>.</summary>
        public static float Contact(in LanchesterSide a, in LanchesterSide b, float flankShare)
        {
            // Without a formation everybody fights where he stands
            if (a.Front <= 0f) return a.Soldiers;
            if (b.Front <= 0f) return a.FrontSoldiers;
            float density = a.FrontSoldiers / a.Front;
            float shared = math.min(a.Front, b.Front);
            return density * (shared + (a.Front - shared) * flankShare);
        }

        /// <param name="flankShare">How much of a front overlapping the enemy's counts (0..1).</param>
        /// <param name="killRate">Soldiers of worth 1 one soldier in contact kills per second.</param>
        public static LanchesterForecast Melee(in LanchesterSide own, in LanchesterSide enemy, float flankShare, float killRate)
        {
            if (own.Soldiers <= 0f || enemy.Soldiers <= 0f)
                return new LanchesterForecast { Balance = own.Soldiers > 0f ? MaxBalance : 0f, OwnLossShare = own.Soldiers > 0f ? 0f : 1f };
            float ownContact = Contact(own, enemy, flankShare), enemyContact = Contact(enemy, own, flankShare);
            float ownPower = own.Strength * ownContact, enemyPower = enemy.Strength * enemyContact;
            float balance = enemyPower > 0f ? math.min(ownPower / enemyPower, MaxBalance) : MaxBalance;
            bool ownWins = balance >= 1f;
            // The winner's rate of killing, in soldiers per second, against the loser's numbers
            float winnerRate = killRate * (ownWins ? ownContact * own.Strength / own.Soldiers : enemyContact * enemy.Strength / enemy.Soldiers);
            return new LanchesterForecast
            {
                Balance = balance,
                OwnLossShare = ownWins && balance > 0f ? 1f / balance : 1f,
                Seconds = winnerRate > 0f ? (ownWins ? enemy.Soldiers : own.Soldiers) / winnerRate : 0f,
            };
        }
    }
}
