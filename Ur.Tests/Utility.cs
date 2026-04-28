using System;
using System.IO;
using System.Reflection;

namespace Ur
{
	internal static class Utility
	{
		public const string EmployeesFolderName = "employees";
		public const string EmployeesFileName = "employees.json";

		public static string EmployeesFolder => Path.Combine(ExecutingAssemblyDirectory, EmployeesFolderName);
		public static string EmployeesFile => Path.Combine(ExecutingAssemblyDirectory, EmployeesFileName);

		public static string ExecutingAssemblyDirectory
		{
			get
			{
				var codeBase = Assembly.GetExecutingAssembly().Location;
				var uri = new UriBuilder($"path:{codeBase}");
				var path = Uri.UnescapeDataString(uri.Path);
				return Path.GetDirectoryName(path);
			}
		}
	}
}
