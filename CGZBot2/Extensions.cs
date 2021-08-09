using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2
{
	static class Extensions
	{
		public static void TryDelete(this DiscordMessage msg)
		{
			try { msg.DeleteAsync().Wait(); } catch (Exception) { }
		}
	}
}
