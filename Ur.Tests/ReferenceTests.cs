using Ur.Tests.Data;
using Xunit;

namespace Ur.Tests
{
	public class ReferenceTests : TestsBase
	{
		[Fact]
		public void ReferenceTest()
		{
			// This test is just to make sure that the project compiles and references are correct.
			// It doesn't actually test anything.

			// Create and register the storage
			var storageEmployees = new MultipleFilesStorage<Employee>(e => e.Id, Utility.EmployeesFolderName);
			var storageJobs = new SingleFileStorageString<Job>(j => j.Id, Utility.EmployeesFile);
		}
	}
}
