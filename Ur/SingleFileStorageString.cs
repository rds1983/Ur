using System;

namespace Ur
{
	public class SingleFileStorageString<ItemType> : SingleFileStorage<string, ItemType> where ItemType : class
	{
		public SingleFileStorageString(Func<ItemType, string> keyGetter, string subFolderName, bool ignoreCase = true) :
			base(keyGetter, subFolderName, ignoreCase ? (key => key.ToLower()) : null)
		{
		}
	}
}
