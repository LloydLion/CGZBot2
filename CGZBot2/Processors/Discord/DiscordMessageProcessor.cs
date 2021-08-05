using DSharpPlus.Entities;
using LloydLion.Serialization.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Processors.Discord
{
	class DiscordMessageProcessor : ITypeSerializationProcessor
	{
		public int Priority => 0;


		public bool CanProcess(Type type)
		{
			return typeof(DiscordMessage) == type;
		}

		public void Prepare(object origin, ObjectsDataSetBuilder builder, ISerializator invoker, string rootId = "")
		{
			builder.SetNamespace(rootId);
			var message = (DiscordMessage)origin;

			builder.Root.AddNumber("id", message.Id);
			builder.Root.AddNumber("channel", message.ChannelId);
			builder.Root.AddNumber("guild", message.Channel.Guild.Id);

			builder.ReturnNamespace();
		}

		public object Restore(object baseFeature, ObjectsDataSet obj, IDeserializator invoker, string rootId = "")
		{
			var root = obj.GetEntry(rootId).Data;
			var guild = Program.Client.GetGuildAsync((ulong)root.ReadPrimitive("guild")).Result;
			var channel = guild.GetChannel((ulong)root.ReadPrimitive("channel"));
			return channel.GetMessageAsync((ulong)root.ReadPrimitive("id")).Result;
		}
	}
}
