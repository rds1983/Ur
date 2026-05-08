using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ur
{
	public static class UrConvertersFactory
	{
		private static readonly Dictionary<Type, JsonConverter> _converters = new Dictionary<Type, JsonConverter>();

		private class CustomJsonConverter<ItemType> : JsonConverter<ItemType> where ItemType : IHasId<string>, new()
		{
			public CustomJsonConverter()
			{
			}

			public override ItemType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			{
				var id = reader.GetString();

				return new ItemType
				{
					Id = id
				};
			}

			public override void Write(Utf8JsonWriter writer, ItemType value, JsonSerializerOptions options)
			{
				writer.WriteStringValue(value.Id);
			}
		}

		public static JsonConverter<T> GetConverter<T>() where T : IHasId<string>, new()
		{
			JsonConverter result;
			if (_converters.TryGetValue(typeof(T), out result))
			{
				return (JsonConverter<T>)result;
			}

			result = new CustomJsonConverter<T>();
			_converters[typeof(T)] = result;

			return (JsonConverter<T>)result;
		}
	}
}
