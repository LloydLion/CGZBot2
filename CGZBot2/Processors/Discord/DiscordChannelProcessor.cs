using DSharpPlus.Entities;
using LloydLion.Serialization.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Processors.Discord
{
	class DiscordChannelProcessor : ITypeSerializationProcessor
	{
		public int Priority => 0;

		public bool CanProcess(Type type)
		{
			return type == typeof(DiscordChannel);
		}

		public void Prepare(object origin, ObjectsDataSetBuilder builder, ISerializator invoker, string rootId = "")
		{
			var obj = (DiscordChannel)origin;
			builder.SetNamespace(rootId);

			builder.Root.AddNumber("id", obj.Id);
			builder.Root.AddNumber("guild", obj.Guild.Id);

			builder.ReturnNamespace();
		}

		public object Restore(object baseFeature, ObjectsDataSet obj, IDeserializator invoker, string rootId = "")
		{
			var root = obj.GetEntry(rootId).Data;
			var guild = Program.Client.GetGuildAsync((ulong)root.ReadPrimitive("guild")).Result;
			return guild.GetChannel((ulong)root.ReadPrimitive("id"));
		}
	}
}
