using CGZBot2.Entities;
using LloydLion.Serialization.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Processors.Entities
{
	class ReputationRoleProcessor : ITypeSerializationProcessor
	{
		public int Priority => 0;


		public bool CanProcess(Type type)
		{
			return typeof(ReputationRole) == type;
		}

		public void Prepare(object origin, ObjectsDataSetBuilder builder, ISerializator invoker, string rootId = "")
		{
			builder.SetNamespace(rootId);
			var role = (ReputationRole)origin;

			builder.Root.AddNumber("role", role.Role.Origin.Id);
			builder.Root.AddNumber("guild", role.Role.Guild.Id);
			builder.Root.AddNumber("level", role.TargetLevel);

			builder.ReturnNamespace();
		}

		public object Restore(object baseFeature, ObjectsDataSet obj, IDeserializator invoker, string rootId = "")
		{
			var root = obj.GetEntry(rootId).Data;
			var guild = Program.Client.GetGuildAsync((ulong)root.ReadPrimitive("guild")).Result;
			return new ReputationRole(guild.GetRole((ulong)root.ReadPrimitive("role")).ToDisRole(guild), (int)root.ReadPrimitive("level"));
		}
	}
}
