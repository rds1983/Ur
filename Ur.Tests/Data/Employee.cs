namespace Ur.Tests.Data
{
	internal class Employee : IHasId<string>
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public Job Job { get; set; }
	}
}
