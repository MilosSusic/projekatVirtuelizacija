using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class WeatherSample
    {
        [DataMember]
        public DateTime Date { get; set; }

        [DataMember]
        public double T { get; set; } // Temperature (degC)

        [DataMember]
        public double Pressure { get; set; } // Pressure (mbar)

        [DataMember]
        public double Tpot { get; set; } // Potential Temperature (K)

        [DataMember]
        public double Tdew { get; set; } // Dew Point Temperature (degC)

        [DataMember]
        public double Rh { get; set; } // Relative Humidity (%)

        [DataMember]
        public double Sh { get; set; } // Specific Humidity (g/kg)

        public static bool TryParseCsv(string csvLine, out WeatherSample sample, out string error)
        {
            sample = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(csvLine))
            {
                error = "Empty line";
                return false;
            }

            // Split CSV line and remove quotes
            string cleaned = csvLine.Replace("\"", "");
            string[] parts = cleaned.Split(new[] { ',', ';', '\t' }, StringSplitOptions.None);

            var ci = CultureInfo.InvariantCulture;
            DateTime date = DateTime.UtcNow;
            double t = 0, pressure = 0, tpot = 0, tdew = 0, rh = 0, sh = 0;

            bool parsed = false;

            // Expected format: date,p,T,Tpot,Tdew,rh,VPmax,VPact,VPdef,sh,...
            if (parts.Length >= 10)
            {
                try
                {
                    // Parse Date (first column - 'date')
                    if (DateTime.TryParse(parts[0], ci, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime tmpDate))
                        date = tmpDate;
                    else
                        date = DateTime.UtcNow;

                    // Parse weather parameters based on new format:
                    // 0=date, 1=p(pressure), 2=T, 3=Tpot, 4=Tdew, 5=rh, 6=VPmax, 7=VPact, 8=VPdef, 9=sh
                    if (double.TryParse(parts[1], NumberStyles.Float, ci, out pressure) &&  // p
                        double.TryParse(parts[2], NumberStyles.Float, ci, out t) &&         // T
                        double.TryParse(parts[3], NumberStyles.Float, ci, out tpot) &&      // Tpot
                        double.TryParse(parts[4], NumberStyles.Float, ci, out tdew) &&      // Tdew
                        double.TryParse(parts[5], NumberStyles.Float, ci, out rh) &&        // rh
                        double.TryParse(parts[9], NumberStyles.Float, ci, out sh))          // sh
                    {
                        parsed = true;
                    }
                }
                catch (Exception ex)
                {
                    error = $"Parse error: {ex.Message}";
                    return false;
                }
            }

            // Fallback: extract numeric tokens in order
            if (!parsed)
            {
                var matches = Regex.Matches(cleaned, @"-?\d+(?:\.\d+)?");
                if (matches.Count >= 6)
                {
                    try
                    {
                        t = double.Parse(matches[0].Value, ci);
                        pressure = double.Parse(matches[1].Value, ci);
                        tpot = double.Parse(matches[2].Value, ci);
                        tdew = double.Parse(matches[3].Value, ci);
                        rh = double.Parse(matches[4].Value, ci);
                        sh = double.Parse(matches[5].Value, ci);
                        date = DateTime.UtcNow;
                        parsed = true;
                    }
                    catch (Exception ex)
                    {
                        error = $"Fallback parse error: {ex.Message}";
                        return false;
                    }
                }
            }

            if (!parsed)
            {
                error = $"Unable to parse line - found {parts.Length} parts, expected at least 7";
                return false;
            }

            sample = new WeatherSample
            {
                Date = date,
                T = t,
                Pressure = pressure,
                Tpot = tpot,
                Tdew = tdew,
                Rh = rh,
                Sh = sh
            };

            return true;
        }

        public override string ToString()
        {
            return $"Date={Date:O}, T={T:F2}°C, P={Pressure:F2}mbar, Tpot={Tpot:F2}K, Tdew={Tdew:F2}°C, RH={Rh:F2}%, SH={Sh:F2}g/kg";
        }
    }
}
