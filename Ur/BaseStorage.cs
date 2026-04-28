using System.Text.Json;

namespace Ur
{
	public abstract class BaseStorage
	{
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

		protected virtual JsonSerializerOptions CreateJsonOptions() => UrContext.BaseOptionsCreator();

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
	}
}