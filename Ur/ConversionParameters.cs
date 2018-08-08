﻿using System.IO;

namespace Ur
{
	public class ConversionParameters
	{
		public string InputPath { get; set; }
		public TextWriter Output { get; set; }
		public string[] Defines { get; set; }
		public string Namespace { get; set; }
		public string Class { get; set; }
		public bool IsPartial { get; set; }
		public string[] SkipStructs { get; set; }
		public string[] SkipGlobalVariables { get; set; }
		public string[] SkipFunctions { get; set; }
		public string[] Classes { get; set; }
		public string[] GlobalArrays { get; set; }

		public ConversionParameters()
		{
			IsPartial = true;
			Defines = new string[0];
			SkipStructs = new string[0];
			SkipGlobalVariables = new string[0];
			SkipFunctions = new string[0];
			Classes = new string[0];
			GlobalArrays = new string[0];
		}
	}
}
