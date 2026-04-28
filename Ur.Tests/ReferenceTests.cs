using Ur.Tests.Data;
using Xunit;

namespace Ur.Tests
{
	public class ReferenceTests : TestsBase
	{
		private class Employees : MultipleFilesStorage<Employee>
		{
			public Employees() : base(Utility.EmployeesFolderName)
			{
			}

			protected override void SetReferences()
			{
				base.SetReferences();

				var storageJobs = (MultipleFilesStorage<Job>)UrContext.GetStorageByType<Job>();
				foreach (var emp in this)
				{
					emp.Job = storageJobs.EnsureByKey(emp.Job.Id);
				}
			}
		}

		[Fact]
		public void ReferenceTest()
		{
			// This test is just to make sure that the project compiles and references are correct.
			// It doesn't actually test anything.

			// Create and register the storage
			var storageEmployees = new Employees();
			var storageJobs = new MultipleFilesStorage<Job>(Utility.JobsFolderName);

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

			// Clear the data
			UrContext.Clear();

			Assert.Equal(0, storageEmployees.Count);
			Assert.Equal(0, storageJobs.Count);

			// Load it again
			Utility.LoadData();

			Assert.Equal(3, storageEmployees.Count);
			Assert.Equal(2, storageJobs.Count);

			var storedJob1 = storageJobs.EnsureByKey("junior");
			Assert.Equal("junior", storedJob1.Id);
			Assert.Equal("Junior Developer", storedJob1.Name);

			var storedJob2 = storageJobs.EnsureByKey("senior");
			Assert.Equal("senior", storedJob2.Id);
			Assert.Equal("Senior Developer", storedJob2.Name);

			var storedEmployee1 = storageEmployees.EnsureByKey("alice");
			Assert.Equal("alice", storedEmployee1.Id);
			Assert.Equal("Alice", storedEmployee1.Name);
			Assert.Same(storedJob1, storedEmployee1.Job);

			var storedEmployee2 = storageEmployees.EnsureByKey("bobSmith");
			Assert.Equal("bobSmith", storedEmployee2.Id);
			Assert.Equal("Bob Smith", storedEmployee2.Name);
			Assert.Same(storedJob1, storedEmployee2.Job);

			var storedEmployee3 = storageEmployees.EnsureByKey("charlie");
			Assert.Equal("charlie", storedEmployee3.Id);
			Assert.Equal("Charlie Waters", storedEmployee3.Name);
			Assert.Same(storedJob2, storedEmployee3.Job);
		}
	}
}
