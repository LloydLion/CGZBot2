using DSharpPlus.Entities;
using LloydLion.Serialization.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Processors.Discord
{
	class DiscordMemberProcessor : ITypeSerializationProcessor
	{
		public int Priority => 0;


		public bool CanProcess(Type type)
		{
			return typeof(DiscordMember) == type;
		}

		public void Prepare(object origin, ObjectsDataSetBuilder builder, ISerializator invoker, string rootId = "")
		{
			builder.SetNamespace(rootId);
			var member = (DiscordMember)origin;

			builder.Root.AddNumber("id", member.Id);
			builder.Root.AddNumber("guild", member.Guild.Id);

			builder.ReturnNamespace();
		}

		public object Restore(object baseFeature, ObjectsDataSet obj, IDeserializator invoker, string rootId = "")
		{
			var root = obj.GetEntry(rootId).Data;
			var guild = Program.Client.GetGuildAsync((ulong)root.ReadPrimitive("guild")).Result;
			return guild.GetMemberAsync((ulong)root.ReadPrimitive("id")).Result;
		}
	}
}
