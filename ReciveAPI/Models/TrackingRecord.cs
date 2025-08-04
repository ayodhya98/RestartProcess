using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ReciveAPI.Models
{
    public class TrackingRecord
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("TrackingNumber")]
        public string TrackingNumber { get; set; }

        [BsonElement("JsonObject")]
        public BsonDocument JsonObject { get; set; }

        [BsonElement("FileName")]
        public string FileName { get; set; }

        [BsonElement("ProcessedAt")]
        public DateTime ProcessedAt { get; set; }

    }
}
