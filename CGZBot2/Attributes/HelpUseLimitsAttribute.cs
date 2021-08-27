using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Attributes
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	class HelpUseLimitsAttribute : Attribute
	{
		public HelpUseLimitsAttribute(CommandUseLimit limit)
		{
			Limit = limit;
		}


		public CommandUseLimit Limit { get; }
	}

	enum CommandUseLimit
	{
		Public,
		Private,
		Admins
	}
}
