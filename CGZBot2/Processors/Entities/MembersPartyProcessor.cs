using CGZBot2.Entities;
using DSharpPlus.Entities;
using LloydLion.Serialization.Common;
using LloydLion.Serialization.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Processors.Entities
{
	class MembersPartyProcessor : ITypeSerializationProcessor
	{
		public int Priority => 0;


		public bool CanProcess(Type type)
		{
			return typeof(MembersParty) == type;
		}

		public void Prepare(object origin, ObjectsDataSetBuilder builder, ISerializator invoker, string rootId = "")
		{
			builder.SetNamespace(rootId);
			var mp = (MembersParty)origin;

			builder.Root.AddString("name", mp.Name);

			var list = mp.Members.ToList();
			if (builder.AddObject(list, "members/", out var col, out var refer))
				invoker.Prepare(list, builder, "members/");
			builder.Root.AddReference("members", refer);

			if (builder.AddObject(mp.Creator, "creator/", out col, out refer))
				invoker.Prepare(mp.Creator, builder, "creator/");
			builder.Root.AddReference("creator", refer);

			builder.ReturnNamespace();
		}

		public object Restore(object baseFeature, ObjectsDataSet obj, IDeserializator invoker, string rootId = "")
		{
			var root = obj.GetEntry(rootId).Data;

			var creator = invoker.Restore<DiscordMember>(null, typeof(DiscordMember), obj, ((ObjectsSetReference)root.ReadPrimitive("creator")).TargetId);
			var members = invoker.Restore<List<DiscordMember>>(null, typeof(List<DiscordMember>), obj, ((ObjectsSetReference)root.ReadPrimitive("members")).TargetId);
			var name = (string)root.ReadPrimitive("name");

			var party = new MembersParty(creator, name);
			party.Members.AddRange(members);
			return party;
		}
	}
}
