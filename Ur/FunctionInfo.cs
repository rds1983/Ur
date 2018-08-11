using System.Collections.Generic;

namespace Ur
{
	public class FunctionInfo
	{
		private readonly Dictionary<string, string> _arguments = new Dictionary<string, string>();

		public Dictionary<string, string> Arguments
		{
			get
			{
				return _arguments;
			}
		}

		public string Name { get; set; }
		public string Signature { get; set; }

		public BaseConfig Config { get; set; }
	}
}
