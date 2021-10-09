using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Tools
{
	static class DialogUtils
	{
		public static Action<DialogContext, DialogMessage.ShowContext> ShowText(string text, string key)
		{
			return (dc, sc) =>
			{
				var msg = dc.Channel.SendMessageAsync(text).Result;
				sc.DynamicParameters.Add(key, msg);
			};
		}

		public static Action<DialogContext, DialogMessage.ShowContext> DeleteMessage(string key)
		{
			return (dc, sc) =>
			{
				((DiscordMessage)sc.DynamicParameters[key]).TryDelete();
			};
		}
	}
}
