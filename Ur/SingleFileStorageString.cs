namespace Ur
{
	public class SingleFileStorageString<ItemType> : SingleFileStorage<string, ItemType> where ItemType : class, IHasId<string>, new()
	{
		public SingleFileStorageString(string subFolderName, bool ignoreCase = true) : base(subFolderName, ignoreCase ? (key => key.ToLower()) : null)
		{
		}
	}
}
