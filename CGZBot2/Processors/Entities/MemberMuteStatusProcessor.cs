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
	class MemberMuteStatusProcessor : ITypeSerializationProcessor
	{
		public int Priority => 0;


		public bool CanProcess(Type type)
		{
			return typeof(MemberMuteStatus) == type;
		}

		public void Prepare(object origin, ObjectsDataSetBuilder builder, ISerializator invoker, string rootId = "")
		{
			builder.SetNamespace(rootId);
			var mute = (MemberMuteStatus)origin;

			builder.Root.AddString("reason", mute.Reason);
			builder.Root.AddNumber("timeout", mute.Timeout?.Ticks ?? -1);
			builder.Root.AddNumber("start date", mute.StartTime.Ticks);
			builder.Root.AddBoolean("cleared", mute.IsCleared);

			if (builder.AddObject(mute.Member, "member/", out var col, out var refer))
				invoker.Prepare(mute.Member, builder, "member/");
			builder.Root.AddReference("member", refer);

			builder.ReturnNamespace();
		}

		public object Restore(object baseFeature, ObjectsDataSet obj, IDeserializator invoker, string rootId = "")
		{
			var root = obj.GetEntry(rootId).Data;

			var reason = (string)root.ReadPrimitive("reason");
			var cleared = (bool)root.ReadPrimitive("cleared");
			var timeoutTicks = (long)root.ReadPrimitive("timeout");
			var startDate =  new DateTime((long)root.ReadPrimitive("start date"));

			TimeSpan? timeout;

			if (timeoutTicks == -1) timeout = null;
			else timeout = new TimeSpan(timeoutTicks);

			var member = invoker.Restore<DiscordMember>(null, typeof(DiscordMember), obj, obj.GetEntryByRefInProp(root, "member").Value.Id);

			var mute = new MemberMuteStatus(member, timeout, reason)
			{
				StartTime = startDate
			};

			if (cleared) mute.Clear();

			return mute;
		}
	}
}
