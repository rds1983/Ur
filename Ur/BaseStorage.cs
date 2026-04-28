using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ur
{
	public abstract class BaseStorage
	{
		public abstract Type StoredType { get; }
		public bool Loaded { get; private set; }

		public virtual string Name => GetType().Name;

		internal void Load()
		{
			Loaded = false;
			InternalLoad();

			Loaded = true;
		}

		internal abstract void Clear();

		protected abstract void InternalLoad();

		protected internal virtual void SetReferences()
		{
		}

		protected void Log(string message) => UrContext.Log(message);

		protected void LogDoesntExist(string name)
		{
			Log($"WARNING: Folder '{name}' doesnt exist. Skipping loading of {Name}.");
		}

		protected virtual JsonSerializerOptions CreateJsonOptions()
		{
			var result = UrContext.BaseOptionsCreator();

			// Add other storages converters
			foreach(var storage in UrContext.Storages)
			{
				if (ReferenceEquals(storage, this))
				{
					continue;
				}

				var converter = storage.CreateJsonConverter();
				if (converter == null)
				{
					continue;
				}

				result.Converters.Add(converter);
			}

			return result;
		}

		protected virtual void JsonSerializeToFile<T>(string path, T data)
		{
			Log($"Saving to '{path}'");

			Utility.SerializeToFile(path, CreateJsonOptions(), data);
		}

		protected virtual T JsonDeserializeFromFile<T>(string path)
		{
			Log($"Loading '{path}'");

			return Utility.DeserializeFromFile<T>(path, CreateJsonOptions());
		}

		protected virtual JsonConverter CreateJsonConverter() => null;

	}
}