using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ReciveAPI.Models
{
    public class TrackingRecord
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string GridFSFileId { get; set; }

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
