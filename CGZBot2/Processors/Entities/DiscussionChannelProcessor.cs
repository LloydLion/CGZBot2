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
	class DiscussionChannelProcessor : ITypeSerializationProcessor
	{
		public int Priority => 0;


		public bool CanProcess(Type type)
		{
			return typeof(DiscussionChannel) == type;
		}

		public void Prepare(object origin, ObjectsDataSetBuilder builder, ISerializator invoker, string rootId = "")
		{
			builder.SetNamespace(rootId);
			var discuss = (DiscussionChannel)origin;

			if (builder.AddObject(discuss.Channel, "channel/", out var col, out var refer))
				invoker.Prepare(discuss.Channel, builder, "channel/");
			builder.Root.AddReference("channel", refer);

			if (builder.AddObject(discuss.ConfirmMessage, "conf msg/", out col, out refer))
				invoker.Prepare(discuss.ConfirmMessage, builder, "conf msg/");
			builder.Root.AddReference("conf msg", refer);

			builder.Root.AddString("state", discuss.State.ToString());

			builder.ReturnNamespace();
		}

		public object Restore(object baseFeature, ObjectsDataSet obj, IDeserializator invoker, string rootId = "")
		{
			var root = obj.GetEntry(rootId).Data;

			var state = (DiscussionChannel.ConfirmState)Enum.Parse
				(typeof(DiscussionChannel.ConfirmState), (string)root.ReadPrimitive("state"));

			var channel = invoker.Restore<DiscordChannel>(null, typeof(DiscordChannel), obj, obj.GetEntryByRefInProp(root, "channel").Value.Id);

			var ret = new DiscussionChannel(channel) { State = state };

			var confMsgEntry = obj.GetEntryByRefInProp(root, "conf msg");

			if (confMsgEntry.HasValue) ret.ConfirmMessage = invoker.Restore<DiscordMessage>(null, typeof(DiscordMessage), obj, confMsgEntry.Value.Id);
			else ret.ConfirmMessage = null;

			return ret;
		}
	}
}
