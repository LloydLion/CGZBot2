using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2
{
	class DisRole
	{
		public DisRole(DiscordRole origin, DiscordGuild guild)
		{
			Origin = origin;
			Guild = guild;
		}


		public DiscordRole Origin { get; }

		public DiscordGuild Guild { get; }


		public static implicit operator DiscordRole(DisRole role)
		{
			return role.Origin;
		}
	}
}
