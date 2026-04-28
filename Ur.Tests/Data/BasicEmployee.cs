namespace Ur.Tests.Data
{
	internal class BasicEmployee: IHasId<string>
	{
		public string Id { get; set; }
		public string Name { get; set; }
	}
}
