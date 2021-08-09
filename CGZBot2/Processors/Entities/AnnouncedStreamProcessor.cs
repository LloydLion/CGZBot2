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
	class AnnouncedStreamProcessor : ITypeSerializationProcessor
	{
		public int Priority => 0;

		public bool CanProcess(Type type)
		{
			return type == typeof(AnnouncedStream);
		}

		public void Prepare(object origin, ObjectsDataSetBuilder builder, ISerializator invoker, string rootId = "")
		{
			builder.SetNamespace(rootId);
			var obj = (AnnouncedStream)origin;

			builder.Root.AddString("name", obj.Name);
			builder.Root.AddString("place", obj.Place);
			builder.Root.AddString("place type", obj.PlaceType.ToString());
			builder.Root.AddString("state", obj.State.ToString());
			builder.Root.AddString("msg state", obj.ReportMessageType?.ToString() ?? "$null");

			builder.Root.AddNumber("create date", obj.CreationDate.Ticks);
			builder.Root.AddNumber("start date", obj.StartDate.Ticks);
			builder.Root.AddNumber("real start date", obj.RealStartDate?.Ticks ?? -1);
			builder.Root.AddNumber("finish date", obj.FinishDate?.Ticks ?? -1);

			if (builder.AddObject(obj.Creator, "creator/", out var col, out var refer))
				invoker.Prepare(obj.Creator, builder, "creator/");
			builder.Root.AddReference("creator", refer);

			if (builder.AddObject(obj.ReportMessage, "rp msg/", out col, out refer))
				invoker.Prepare(obj.ReportMessage, builder, "rp msg/");
			builder.Root.AddReference("rp msg", refer);

			builder.ReturnNamespace();
		}

		public object Restore(object baseFeature, ObjectsDataSet obj, IDeserializator invoker, string rootId = "")
		{
			var root = obj.GetEntry(rootId).Data;

			var name = (string)root.ReadPrimitive("name");
			var place = (string)root.ReadPrimitive("place");
			var placeType = (AnnouncedStream.StreamingPlaceType)Enum.Parse
				(typeof(AnnouncedStream.StreamingPlaceType), (string)root.ReadPrimitive("place type"));

			var state = (AnnouncedStream.StreamState)Enum.Parse
				(typeof(AnnouncedStream.StreamState), (string)root.ReadPrimitive("state"));

			var msgStateStr = (string)root.ReadPrimitive("msg state");

			AnnouncedStream.StreamState? msgState;

			if(msgStateStr == "$null")
				msgState = null;
			else
				msgState = (AnnouncedStream.StreamState)Enum.Parse(typeof(AnnouncedStream.StreamState), msgStateStr);
			

			var createDate = new DateTime((long)root.ReadPrimitive("create date"));
			var startDate = new DateTime((long)root.ReadPrimitive("start date"));

			var realStartDateTicks = (long)root.ReadPrimitive("real start date");
			var finishDateTicks = (long)root.ReadPrimitive("finish date");

			DateTime? realStartDate;
			DateTime? finishDate;

			if (realStartDateTicks == -1)
				realStartDate = null;
			else realStartDate = new DateTime(realStartDateTicks);

			if (finishDateTicks == -1)
				finishDate = null;
			else finishDate = new DateTime(finishDateTicks);


			var creator = invoker.Restore<DiscordMember>(null, typeof(DiscordMember),
				obj, obj.GetEntryByRefInProp(root, "creator").Value.Id);

			var rpMsg = invoker.Restore<DiscordMessage>(null, typeof(DiscordMessage),
				obj, obj.GetEntryByRefInProp(root, "rp msg").Value.Id);


			return new AnnouncedStream(name, creator, startDate, place, placeType)
				{ CreationDate = createDate, RealStartDate = realStartDate, FinishDate = finishDate };
		}
	}
}
