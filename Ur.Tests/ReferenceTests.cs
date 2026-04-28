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
			var storageEmployees = new MultipleFilesStorage<Employee>(Utility.EmployeesFolderName);
			var storageJobs = new SingleFileStorageString<Job>(Utility.JobsFileName);

			UrContext.Register(storageEmployees);
			UrContext.Register(storageJobs);
			Utility.LoadData();

			var job1 = new Job
			{
				Id = "junior",
				Name = "Junior Developer"
			};
			storageJobs.Create(job1);

			var job2 = new Job
			{
				Id = "senior",
				Name = "Senior Developer"
			};
			storageJobs.Create(job2);

			var employee1 = new Employee
			{
				Id = "alice",
				Name = "Alice",
				Job = job1
			};
			storageEmployees.Create(employee1);

			var employee2 = new Employee
			{
				Id = "bobSmith",
				Name = "Bob Smith",
				Job = job1
			};
			storageEmployees.Create(employee2);

			var employee3 = new Employee
			{
				Id = "charlie",
				Name = "Charlie Waters",
				Job = job2
			};
			storageEmployees.Create(employee3);

			storageJobs.SaveAll();
			storageEmployees.SaveAll();
		}
	}
}
