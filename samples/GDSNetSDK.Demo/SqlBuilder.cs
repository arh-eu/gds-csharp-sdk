using System;

namespace GDSNetSdk.InsertQueryDemo
{
    /// <summary>
    /// Builds SQL statements used by the demo:
    /// - a complex INSERT into the main multi_event table and the attachment table
    /// - a simple SELECT query
    /// </summary>
    public static class SqlBuilder
    {
        public static string BuildInsertWithAttachments(
            string eventId,
            string sourceId,
            string plate,
            string escapedExtraData,
            string frontImageId,
            string frontImageIdHex,
            string rearImageId,
            string rearImageIdHex)
        {
            if (eventId == null) throw new ArgumentNullException(nameof(eventId));
            if (sourceId == null) throw new ArgumentNullException(nameof(sourceId));
            if (plate == null) throw new ArgumentNullException(nameof(plate));
            if (escapedExtraData == null) throw new ArgumentNullException(nameof(escapedExtraData));
            if (frontImageId == null) throw new ArgumentNullException(nameof(frontImageId));
            if (frontImageIdHex == null) throw new ArgumentNullException(nameof(frontImageIdHex));
            if (rearImageId == null) throw new ArgumentNullException(nameof(rearImageId));
            if (rearImageIdHex == null) throw new ArgumentNullException(nameof(rearImageIdHex));

            return
                "\nINSERT INTO multi_event(id, source, plate, extra_data, front_plate_image, rear_plate_image) " +
                $"\n\tVALUES('{eventId}', '{sourceId}', '{plate}', '{escapedExtraData}', " +
                $"\n\tarray('{frontImageId}'), array('{rearImageId}'));" +
                "\nINSERT INTO \"multi_event-@attachment\" (id, meta, data) " +
                $"\n\tVALUES('{frontImageId}', '{{\"contentType\":\"image/png\"}}', 0x{frontImageIdHex});" +
                "\nINSERT INTO \"multi_event-@attachment\" (id, meta, data) " +
                $"\n\tVALUES('{rearImageId}', '{{\"contentType\":\"image/png\"}}', 0x{rearImageIdHex});";
        }

        public static string BuildSampleQuery()
            => "SELECT id FROM multi_event LIMIT 1";
    }
}
