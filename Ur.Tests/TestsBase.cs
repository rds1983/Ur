using System;
using System.IO;

namespace Ur.Tests
{
	public class TestsBase : IDisposable
	{
		protected TestsBase()
		{
			// Called before every test method
		}

		public void Dispose()
		{
			// Called after every test method

			// Remove storages
			UrContext.UnregisterAll();

			// Delete data
			if (Directory.Exists(Utility.DataFolder))
			{
				Directory.Delete(Utility.DataFolder, true);
			}
		}
	}
}
