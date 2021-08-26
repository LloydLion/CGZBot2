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
	class TeamGameProcessor : ITypeSerializationProcessor
	{
		public int Priority => 0;


		public bool CanProcess(Type type)
		{
			return typeof(TeamGame) == type;
		}

		public void Prepare(object origin, ObjectsDataSetBuilder builder, ISerializator invoker, string rootId = "")
		{
			builder.SetNamespace(rootId);

			var game = (TeamGame)origin;
			builder.Root.AddObjectPrimitives(game, s => s.ToLower());
			builder.Root.Remove(nameof(game.NeedReportUpdate).ToLower());

			if(builder.AddObject(game.Creator, "creator/", out var col, out var refer))
				invoker.Prepare(game.Creator, builder, "creator/");
			builder.Root.AddReference("creator", refer);

			if (builder.AddObject(game.CreatedVoice, "voice/", out col, out refer))
				invoker.Prepare(game.CreatedVoice, builder, "voice/");
			builder.Root.AddReference("voice", refer);

			var list = game.TeamMembers.ToList();
			if (builder.AddObject(list, "members/", out col, out refer))
				invoker.Prepare(list, builder, "members/");
			builder.Root.AddReference("members", refer);

			list = game.Invited.ToList();
			if (builder.AddObject(list, "invited/", out col, out refer))
				invoker.Prepare(list, builder, "invited/");
			builder.Root.AddReference("invited", refer);

			builder.Root.AddString("state", game.State.ToString());
			builder.Root.AddNumber("create date", game.CreationDate.Ticks);
			builder.Root.AddNumber("start date", game.StartDate?.Ticks ?? -1);
			builder.Root.AddNumber("end date", game.FinishDate?.Ticks ?? -1);

			builder.ReturnNamespace();
		}

		public object Restore(object baseFeature, ObjectsDataSet obj, IDeserializator invoker, string rootId = "")
		{
			var root = obj.GetEntry(rootId).Data;

			var creator = invoker.Restore<DiscordMember>(null, typeof(DiscordMember), obj, ((ObjectsSetReference)root.ReadPrimitive("creator")).TargetId);
			var members = invoker.Restore<List<DiscordMember>>(null, typeof(List<DiscordMember>), obj, ((ObjectsSetReference)root.ReadPrimitive("members")).TargetId);
			var invited = invoker.Restore<List<DiscordMember>>(null, typeof(List<DiscordMember>), obj, ((ObjectsSetReference)root.ReadPrimitive("invited")).TargetId);

			DiscordChannel voice;
			var voiceRef = ((ObjectsSetReference)root.ReadPrimitive("voice")).TargetId;

			if (voiceRef == null) voice = null;
			else voice = invoker.Restore<DiscordChannel>(null, typeof(DiscordChannel), obj, voiceRef);

			var state = (TeamGame.GameState)Enum.Parse
				(typeof(TeamGame.GameState), (string)root.ReadPrimitive("state"));

			var createDate = new DateTime((long)root.ReadPrimitive("create date"));
			var startDateTicks = (long)root.ReadPrimitive("start date");
			var finishDateTicks = (long)root.ReadPrimitive("end date");

			var game = new TeamGame(creator, null, null, 0) { Invited = invited, TeamMembers = members, State = state,
				CreationDate = createDate, StartDate = startDateTicks == -1 ? null : new DateTime(startDateTicks),
				FinishDate = finishDateTicks == -1 ? null : new DateTime(finishDateTicks), CreatedVoice = voice
			};

			root.RestoreObjectPrimitives(game, s => s.ToLower());

			return game;
		}
	}
}
