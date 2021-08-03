using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using System.Collections.Generic;

namespace CGZBot2
{
	class GuildSettings<T> : Dictionary<DiscordGuild, T>
	{
		public T this[CommandContext ctx]
		{
			get => this[ctx.Guild];
			set => this[ctx.Guild] = value;
		}
	}
}