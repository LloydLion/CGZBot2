using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Entities
{
	class MembersParty
	{
		public MembersParty(DiscordMember creator, string name)
		{
			Creator = creator;
			Name = name;
		}

		public DiscordMember Creator { get; }

		public string Name { get; set; }

		public ICollection<DiscordMember> Members { get; } = new List<DiscordMember>();
	}
}
