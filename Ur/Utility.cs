using System.Collections;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Ur
{
	internal static class Utility
	{
		private static JsonSerializerOptions _defaultOptions;

		public static JsonSerializerOptions DefaultOptions
		{
			get
			{
				if (_defaultOptions == null)
				{
					_defaultOptions = CreateOptions();
				}

				return _defaultOptions;
			}
		}

		private static void IgnoreEmptyListOfStrings(JsonTypeInfo typeInfo)
		{
			var collectionProperties = typeInfo.Properties.Where(p => typeof(ICollection).IsAssignableFrom(p.PropertyType));

			foreach (JsonPropertyInfo propertyInfo in collectionProperties)
			{
				propertyInfo.ShouldSerialize = (_, val) =>
				{
					var col = val as ICollection;
					if (val == null)
					{
						return false;
					}

					return col.Count > 0;
				};
			}
		}

		public static JsonSerializerOptions CreateOptions()
		{
			var result = new JsonSerializerOptions
			{
				WriteIndented = true,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
				IncludeFields = true,
				IgnoreReadOnlyFields = true,
				IgnoreReadOnlyProperties = true,
				TypeInfoResolver = new DefaultJsonTypeInfoResolver
				{
					// This modifier will suppress empty lists
					Modifiers = { IgnoreEmptyListOfStrings }
				}
			};

			result.Converters.Add(new JsonStringEnumConverter());

			return result;
		}

		public static void SerializeToFile<T>(string path, JsonSerializerOptions options, T data)
		{
			var s = JsonSerializer.Serialize(data, options);
			File.WriteAllText(path, s);
		}

		public static T DeserializeFromFile<T>(string path, JsonSerializerOptions options)
		{
			var data = File.ReadAllText(path);
			return JsonSerializer.Deserialize<T>(data, options);
		}

		public static void EnsureFolder(string folderPath)
		{
			if (!Directory.Exists(folderPath))
			{
				UrContext.Log($"Creating folder '{folderPath}'");
				Directory.CreateDirectory(folderPath);
			}
		}
	}
}
