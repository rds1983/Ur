using System.Collections.Generic;

namespace Ur
{
	public class FunctionInfo
	{
		private readonly Dictionary<string, int> _refArguments = new Dictionary<string, int>();

		public string Name { get; set; }
		public string Signature { get; set; }

		public Dictionary<string, int> RefArguments
		{
			get { return _refArguments; }
		}

		public FunctionConfig Config { get; set; }
	}
}
