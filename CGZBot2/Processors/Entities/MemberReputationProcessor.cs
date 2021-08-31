using CGZBot2.Entities;
using DSharpPlus.Entities;
using LloydLion.Serialization.Common;
using LloydLion.Serialization.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Processors.Entities
{
	class MemberReputationProcessor : ITypeSerializationProcessor
	{
		public int Priority => 0;


		public bool CanProcess(Type type)
		{
			return typeof(MemberReputation) == type;
		}

		public void Prepare(object origin, ObjectsDataSetBuilder builder, ISerializator invoker, string rootId = "")
		{
			builder.SetNamespace(rootId);
			var mr = (MemberReputation)origin;

			builder.Root.AddNumber("rp", mr.TotalReputation);

			if (builder.AddObject(mr.Member, "member/", out var col, out var refer))
				invoker.Prepare(mr.Member, builder, "member/");
			builder.Root.AddReference("member", refer);

			builder.ReturnNamespace();
		}

		public object Restore(object baseFeature, ObjectsDataSet obj, IDeserializator invoker, string rootId = "")
		{
			var root = obj.GetEntry(rootId).Data;

			var member = invoker.Restore<DiscordMember>(null, typeof(DiscordMember), obj, ((ObjectsSetReference)root.ReadPrimitive("member")).TargetId);

			var mr = new MemberReputation(member);

			mr.GiveReputation((int)root.ReadPrimitive("rp"));

			return mr;
		}
	}
}
