using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Entities
{
	class MemberReputation
	{
		private int lastrp;
		private int level;
		private int levelrp;


		public static readonly List<int> LevelsCost = new()
		{
			60, 100, 120, 160, 200, 250 
		};


		public MemberReputation(DiscordMember member)
		{
			Member = member;
		}


		public DiscordMember Member { get; }

		public int TotalReputation => lastrp + levelrp;

		public int Level => level;

		public int ReputationOnLevel => levelrp;

		public int NextLevelCost => Level >= LevelsCost.Count ? LevelsCost.Last() : LevelsCost[Level];


		public event Action<MemberReputation> ReputationChanged;


		public void GiveReputation(int rp)
		{
			levelrp += rp;
			RecalcLevel();
			ReputationChanged?.Invoke(this);
		}

		public void RevokeReputation(int rp)
		{
			levelrp -= rp;
			if (levelrp < 0) levelrp = 0;
			ReputationChanged?.Invoke(this);
		}

		private void RecalcLevel()
		{
			while(levelrp >= NextLevelCost)
			{
				levelrp -= NextLevelCost;
				lastrp += NextLevelCost;
				level++;
			}
		}
	}
}
