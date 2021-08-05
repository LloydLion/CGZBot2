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
	class CreatedVoiceChannelProcessor : ITypeSerializationProcessor
	{
		public int Priority => 0;


		public bool CanProcess(Type type)
		{
			return typeof(CreatedVoiceChannel) == type;
		}

		public void Prepare(object origin, ObjectsDataSetBuilder builder, ISerializator invoker, string rootId = "")
		{
			builder.SetNamespace(rootId);

			var channel = (CreatedVoiceChannel)origin;

			if (builder.AddObject(channel.Channel, "channel/", out var col, out var refer))
				invoker.Prepare(channel.Channel, builder, "channel/");
			builder.Root.AddReference("channel", refer);

			if (builder.AddObject(channel.Creator, "creator/", out col, out refer))
				invoker.Prepare(channel.Creator, builder, "creator/");
			builder.Root.AddReference("creator", refer);

			if (builder.AddObject(channel.ReportMessage, "message/", out col, out refer))
				invoker.Prepare(channel.ReportMessage, builder, "message/");
			builder.Root.AddReference("message", refer);

			builder.ReturnNamespace();
		}

		public object Restore(object baseFeature, ObjectsDataSet obj, IDeserializator invoker, string rootId = "")
		{
			var root = obj.GetEntry(rootId).Data;

			var chprocessor = invoker.Processors.OrderBy(s => s.Priority).First(s => s.CanProcess(typeof(DiscordChannel)));
			var msgprocessor = invoker.Processors.OrderBy(s => s.Priority).First(s => s.CanProcess(typeof(DiscordMessage)));
			var memprocessor = invoker.Processors.OrderBy(s => s.Priority).First(s => s.CanProcess(typeof(DiscordMember)));

			var tmp = obj.GetEntryByRefInProp(root, "channel").Value;
			var channel = (DiscordChannel)chprocessor.Restore(null, obj, invoker, tmp.Id);

			tmp = obj.GetEntryByRefInProp(root, "message").Value;
			var message = (DiscordMessage)msgprocessor.Restore(null, obj, invoker, tmp.Id);

			tmp = obj.GetEntryByRefInProp(root, "creator").Value;
			var creator = (DiscordMember)memprocessor.Restore(null, obj, invoker, tmp.Id);

			var ret = new CreatedVoiceChannel(channel, creator) { ReportMessage = message };
			return ret;
		}
	}
}
