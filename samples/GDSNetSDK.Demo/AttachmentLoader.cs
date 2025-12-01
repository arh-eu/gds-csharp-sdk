using System;
using System.Collections.Generic;
using System.IO;

namespace GDSNetSdk.InsertQueryDemo
{
    /// <summary>
    /// Loads binary attachments used by the demo.
    /// </summary>
    public static class AttachmentLoader
    {
        private const string DefaultImageFileName = "InOutParkingCar.png";

        /// <summary>
        /// Loads the default demo PNG file and returns it in a mapping that
        /// can be used as the binaryContentsMapping parameter of SendEvent2.
        /// The same image bytes are used for both attachment IDs.
        /// </summary>
        public static Dictionary<string, byte[]> LoadDefaultDemoImages(
            string frontHexKey,
            string rearHexKey)
        {
            if (frontHexKey == null) throw new ArgumentNullException(nameof(frontHexKey));
            if (rearHexKey == null) throw new ArgumentNullException(nameof(rearHexKey));

            if (!File.Exists(DefaultImageFileName))
            {
                throw new FileNotFoundException(
                    $"Demo image file '{DefaultImageFileName}' not found next to the executable.");
            }

            byte[] imageBytes = File.ReadAllBytes(DefaultImageFileName);

            return new Dictionary<string, byte[]>
            {
                { frontHexKey, imageBytes },
                { rearHexKey, imageBytes }
            };
        }
    }
}
