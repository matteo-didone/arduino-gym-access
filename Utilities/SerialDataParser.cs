namespace ArduinoGymAccess.Utilities
{
    public static class SerialDataParser
    {
        private const string RFID_PREFIX = "RFID:";

        public class SerialData
        {
            public string Type { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public bool IsValid { get; set; }
            public string Error { get; set; } = string.Empty;
        }

        public static SerialData Parse(string rawData)
        {
            if (string.IsNullOrWhiteSpace(rawData))
            {
                return new SerialData { IsValid = false, Error = "Dati ricevuti vuoti o nulli" };
            }

            try
            {
                if (rawData.StartsWith(RFID_PREFIX))
                {
                    string rfidCode = rawData.Substring(RFID_PREFIX.Length).Trim();
                    return new SerialData
                    {
                        Type = "RFID",
                        Value = rfidCode,
                        IsValid = IsValidRfidFormat(rfidCode)
                    };
                }

                return new SerialData { IsValid = false, Error = "Formato dati non riconosciuto" };
            }
            catch (Exception ex)
            {
                return new SerialData { IsValid = false, Error = $"Errore nel parsing dei dati: {ex.Message}" };
            }
        }

        private static bool IsValidRfidFormat(string rfidCode)
        {
            return !string.IsNullOrWhiteSpace(rfidCode) &&
                   rfidCode.Length >= 8 &&
                   rfidCode.All(char.IsLetterOrDigit);
        }
    }
}
