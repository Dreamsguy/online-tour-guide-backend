using System.Text.Json;
using System.Text.Json.Serialization;
using NetTopologySuite.Geometries;

namespace OnlineTourGuide
{
    public class PointJsonConverter : JsonConverter<Point>
    {
        public override Point Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Предполагаем, что Point сериализуется как объект { "X": number, "Y": number }
            double x = 0, y = 0;
            reader.Read(); // Пропускаем открывающую скобку объекта
            while (reader.TokenType != JsonTokenType.EndObject)
            {
                string propertyName = reader.GetString();
                reader.Read();
                if (propertyName == "X")
                    x = reader.GetDouble();
                else if (propertyName == "Y")
                    y = reader.GetDouble();
                reader.Read();
            }
            return new Point(x, y) { SRID = 4326 };
        }

        public override void Write(Utf8JsonWriter writer, Point value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            // Проверяем координаты на NaN или Infinity
            double x = double.IsNaN(value.X) || double.IsInfinity(value.X) ? 0 : value.X;
            double y = double.IsNaN(value.Y) || double.IsInfinity(value.Y) ? 0 : value.Y;

            writer.WriteStartObject();
            writer.WriteNumber("X", x);
            writer.WriteNumber("Y", y);
            writer.WriteEndObject();
        }
    }
}