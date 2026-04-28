using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ur
{
	public class MultipleFilesStorage<ItemType> : GenericBaseStorage<string, ItemType> where ItemType : class
	{
		private string SubfolderName { get; set; }

		public ItemType this[string index] => EnsureByKey(index);
		public override string Name => SubfolderName;

		public string Folder => Path.Combine(UrContext.Folder, SubfolderName);

		public MultipleFilesStorage(Func<ItemType, string> keyGetter, string subFolderName, bool ignoreCase = true) :
			base(keyGetter, ignoreCase ? (key => key.ToLower()) : null)
		{
			SubfolderName = subFolderName;
		}

		protected override void InternalLoad()
		{
			ClearCache();

			var filesList = ListFiles();
			foreach (var filePath in filesList)
			{
				LoadEntity(filePath);
			}
		}

		public virtual ItemType LoadEntity(string filePath)
		{
			var entity = JsonDeserializeFromFile<ItemType>(filePath);

			AddToCache(entity);

			return entity;
		}

		protected virtual void InternalSave(ItemType entity)
		{
			var path = BuildPath(entity);
			var folder = Path.GetDirectoryName(path);
			EnsureFolder(folder);

			var asSE = entity as ISerializationEvents;
			try
			{
				if (asSE != null)
				{
					asSE.OnSerializationStarted();
				}

				JsonSerializeToFile(path, entity);
			}
			finally
			{
				if (asSE != null)
				{
					asSE.OnSerializationEnded();
				}
			}
		}

		public void Save(ItemType entity)
		{
			var key = GetKey(entity);
			if (!Cache.ContainsKey(key))
			{
				// If the item isn't cached, check the possibility it was renamed
				KeyValuePair<string, ItemType>? existing = null;
				foreach (var pair in Cache)
				{
					if (pair.Value == entity)
					{
						existing = pair;
						break;
					}
				}

				if (existing != null)
				{
					// It was renamed
					// Delete the file
					var existingKey = existing.Value.Key;
					var path = BuildPath(existingKey);
					if (File.Exists(path))
					{
						File.Delete(path);
					}
					else
					{
						Log($"WARNING: Unable to find file for existing item {existingKey}");
					}

					RemoveFromCache(existingKey);
				}
			}

			// Save the data
			InternalSave(entity);

			// Add to the cache
			AddToCache(entity);
		}

		public void Create(ItemType entity)
		{
			var key = GetKey(entity);
			if (GetByKey(key) != null)
			{
				throw new Exception($"Item with key '{key}' already exist.");
			}

			Save(entity);
		}

		protected virtual string[] ListFiles()
		{
			if (!Directory.Exists(Folder))
			{
				Log($"WARNING: Folder '{Folder}' doesnt exist.");
				return new string[0];
			}

			return Directory.EnumerateFiles(Folder, "*.json", SearchOption.AllDirectories).ToArray();
		}

		private string BuildPath(string key) => Path.ChangeExtension(Path.Combine(Folder, key), "json");

		public virtual string BuildPath(ItemType entity) => BuildPath(GetKey(entity, false));

		public override void Remove(ItemType entity)
		{
			// Delete the file
			var path = BuildPath(entity);
			File.Delete(path);

			base.Remove(entity);
		}

		public override void SaveAll()
		{
			foreach (var item in this)
			{
				InternalSave(item);
			}
		}
	}
}