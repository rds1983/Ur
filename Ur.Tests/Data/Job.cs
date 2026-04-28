namespace Ur.Tests.Data
{
	internal class Job : IHasId<string>
	{
		public string Id { get; set; }
		public string Name { get; set; }
	}
}
