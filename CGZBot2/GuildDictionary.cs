using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using System;
using System.Collections;
using System.Collections.Generic;

namespace CGZBot2
{
	class GuildDictionary<T> : Dictionary<DiscordGuild, T>, IGuildDictionary
	{
		public Func<DiscordGuild, T> DefaultValueFactory { get; init; } = (guild) => default;


		public T this[CommandContext ctx]
		{
			get => this[ctx.Guild];
			set => this[ctx.Guild] = value;
		}

		public new T this[DiscordGuild guild]
		{
			get { if (!ContainsKey(guild)) Add(guild, DefaultValueFactory(guild)); return base[guild]; }
			set { if (!ContainsKey(guild)) Add(guild, value); else base[guild] = value; }
		}
	}

	interface IGuildDictionary : IDictionary { }
}