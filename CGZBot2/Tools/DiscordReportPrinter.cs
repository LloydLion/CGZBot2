using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Tools
{
	class DiscordReportPrinter<T> where T : IDiscordReportable
	{
		public DiscordReportPrinter(DiscordChannel channel)
		{
			Channel = channel;
		}


		public DiscordChannel Channel { get; }

		public DiscordGuild Guild => Channel.Guild;


		public DiscordMessage Report(T obj)
		{
			if (obj.NeedReportUpdate == false) return null;

			var builder = new DiscordMessageBuilder();

			if (obj.ReportMessage == null || !obj.ReportMessage.IsExist())
				if (obj.Print(builder) == false) return obj.ReportMessage = null;
				else return obj.ReportMessage = Channel.SendMessageAsync(builder).Result;
			else
			{
				if (obj.Print(builder) == false)
				{
					obj.ReportMessage.DeleteAsync().Wait();
					return null;
				}
				else
				{
					obj.ReportMessage.ModifyAsync(builder).Wait();
					return obj.ReportMessage;
				}
			}
		}
	}


	interface IDiscordReportable
	{
		bool NeedReportUpdate { get; }

		bool ManualUpdateRequested { get; }

		DiscordMessage ReportMessage { get; set; }


		void RequestManualUpdate();

		void ResetUpdateFlag();

		bool Print(DiscordMessageBuilder builder);
	}
}
