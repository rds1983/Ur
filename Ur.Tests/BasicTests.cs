using System.IO;
using Ur.Data;
using Xunit;

namespace Ur.Tests
{
	public class BasicTests
	{
		[Fact]
		public void BasicEmployeeMultipleFilesStorageTest()
		{
			// Create and register the storage
			var storage = new MultipleFilesStorage<BasicEmployee>(e => e.Id, Utility.EmployeesFolderName);

			UrContext.Register(storage);
			UrContext.Load(Utility.ExecutingAssemblyDirectory);

			// Create some employees and save them
			var employee1 = new BasicEmployee
			{
				Id = "alice",
				Name = "Alice"
			};
			storage.Save(employee1);

			var employee2 = new BasicEmployee
			{
				Id = "bobSmith",
				Name = "Bob Smith"
			};
			storage.Save(employee2);

			Assert.Equal(2, storage.Count);

			// Clear the data
			UrContext.Clear();

			Assert.Equal(0, storage.Count);

			// Load it again
			UrContext.Load(Utility.ExecutingAssemblyDirectory);
			
			// Validate the data
			Assert.Equal(2, storage.Count);
			var loadedEmployee1 = storage.EnsureByKey("alice");
			Assert.NotNull(loadedEmployee1);
			Assert.Equal(employee1.Id, loadedEmployee1.Id);
			Assert.Equal(employee1.Name, loadedEmployee1.Name);

			var loadedEmployee2 = storage.EnsureByKey("bobSmith");
			Assert.NotNull(loadedEmployee2);
			Assert.Equal(employee2.Id, loadedEmployee2.Id);
			Assert.Equal(employee2.Name, loadedEmployee2.Name);

			// Remove the created folder
			Directory.Delete(Utility.EmployeesFolder, true);
		}

		[Fact]
		public void BasicEmployeeSingleFileStorageTest()
		{
			// Create and register the storage
			var storage = new SingleFileStorageString<BasicEmployee>(e => e.Id, Utility.EmployeesFileName);

			UrContext.Register(storage);
			UrContext.Load(Utility.ExecutingAssemblyDirectory);

			// Create some employees and save them
			var employee1 = new BasicEmployee
			{
				Id = "alice",
				Name = "Alice"
			};
			storage.Create(employee1);

			var employee2 = new BasicEmployee
			{
				Id = "bobSmith",
				Name = "Bob Smith"
			};
			storage.Create(employee2);
			storage.SaveAll();

			Assert.Equal(2, storage.Count);

			// Clear the data
			UrContext.Clear();

			Assert.Equal(0, storage.Count);

			// Load it again
			UrContext.Load(Utility.ExecutingAssemblyDirectory);

			// Validate the data
			Assert.Equal(2, storage.Count);
			var loadedEmployee1 = storage.EnsureByKey("alice");
			Assert.NotNull(loadedEmployee1);
			Assert.Equal(employee1.Id, loadedEmployee1.Id);
			Assert.Equal(employee1.Name, loadedEmployee1.Name);

			var loadedEmployee2 = storage.EnsureByKey("bobSmith");
			Assert.NotNull(loadedEmployee2);
			Assert.Equal(employee2.Id, loadedEmployee2.Id);
			Assert.Equal(employee2.Name, loadedEmployee2.Name);

			// Remove the created file
			File.Delete(Utility.EmployeesFile);
		}
	}
}