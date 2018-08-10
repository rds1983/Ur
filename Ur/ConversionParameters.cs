using System;

namespace Ur
{
	public class ConversionParameters
	{
		public string InputPath { get; set; }
		public string[] Defines { get; set; }
		public bool IsPartial { get; set; }

		public bool AddGeneratedByUr { get;set; }
		public Func<string, string, string, bool> UseRefInsteadOfPointer { get; set; }
		public Action<CursorProcessResult> CustomGlobalVariableProcessor { get; set; }
		public Action<string, string[]> FunctionHeaderProcessed { get; set; }
		public Action BeforeLastClosingBracket { get; set; }
		public Func<string, BaseConfig> StructSource { get; set; }
		public Func<string, BaseConfig> GlobalVariableSource { get; set; }
		public Func<string, BaseConfig> EnumSource { get; set; }
		public Func<FunctionInfo, BaseConfig> FunctionSource { get; set; }

		public ConversionParameters()
		{
			IsPartial = true;
			Defines = new string[0];
		}
	}
}
