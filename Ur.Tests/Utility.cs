using System;
using System.IO;
using System.Reflection;

namespace Ur.Tests
{
	internal static class Utility
	{
		public const string EmployeesFolderName = "employees";
		public const string EmployeesFileName = "employees.json";
		public const string JobsFolderName = "jobs";
		public const string JobsFileName = "jobs.json";

		public static string EmployeesFolder => Path.Combine(DataFolder, EmployeesFolderName);
		public static string EmployeesFile => Path.Combine(DataFolder, EmployeesFileName);
		public static string JobsFolder => Path.Combine(DataFolder, JobsFolderName);
		public static string JobsFile => Path.Combine(DataFolder, JobsFileName);

		public static string DataFolder => Path.Combine(ExecutingAssemblyFolder, "data");

		public static string ExecutingAssemblyFolder
		{
			get
			{
				var codeBase = Assembly.GetExecutingAssembly().Location;
				var uri = new UriBuilder($"path:{codeBase}");
				var path = Uri.UnescapeDataString(uri.Path);
				return Path.GetDirectoryName(path);
			}
		}

		public static void LoadData()
		{
			UrContext.Load(DataFolder);
		}
	}
}
