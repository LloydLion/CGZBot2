using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Entities
{
	class ReputationRole
	{
		public ReputationRole(DisRole role, int targetLevel)
		{
			Role = role;
			TargetLevel = targetLevel;
		}


		public DisRole Role { get; }

		public int TargetLevel { get; }
	}
}
