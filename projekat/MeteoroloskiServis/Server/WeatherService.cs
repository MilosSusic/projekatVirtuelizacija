using Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.ServiceModel;

namespace Server
{
    /// <summary>
    /// WeatherService (WCF) - provides methods for clients to 
    /// send weather data, manage sessions and handle alerts.
    /// </summary>

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Single)]
    public class WeatherService : IWeatherService, IDisposable
    {
        private readonly string storageRoot = ConfigurationManager.AppSettings["weatherStoragePath"] ?? "WeatherStorage";
        private WeatherResourceManager _resourceManager;
        private string _currentSessionId;
        
        // Threshold values
        private double _tThreshold;
        private double _rhThreshold; 
        private double _dewThreshold;
        private double _deviationPct;
        
        // Analytics state
        private double? _lastTemperature;
        private double? _lastRH;
        private double? _lastTdew;
        private double _runningMeanT;
        private double _runningMeanRH;
        private double _runningMeanTdew;
        private long _count;    // total number of samples processed
        private int _written;   // number of samples successfully written
        private readonly object _lockObject = new object();

        // Events for weather monitoring
        public event EventHandler<string> OnTransferStarted;
        public event EventHandler<string> OnSampleReceived;
        public event EventHandler<string> OnTransferCompleted;
        public event EventHandler<string> OnWarningRaised;

        public WeatherService()
        {
            _resourceManager = new WeatherResourceManager();
            
            // Subscribe to events for logging
            OnTransferStarted += (s, m) => Console.WriteLine($"[START] {m}");
            OnSampleReceived += (s, m) => Console.Write('.');
            OnTransferCompleted += (s, m) => Console.WriteLine($"\n[END] {m}");
            OnWarningRaised += (s, m) => 
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n🚨 METEOROLOŠKI ALARM: {m}");
                Console.ResetColor();
            };
        }

        public WeatherAck StartSession(WeatherSessionMeta meta)
        {
            lock (_lockObject)
            {
                // Thread-safe: only one thread can initialize session at a time
                try
                {
                    if (meta == null)
                        return new WeatherAck { Success = false, Message = "Meta is null", Status = "NACK" };

                    // Validate thresholds
                    if (meta.TThreshold <= 0)
                        return new WeatherAck { Success = false, Message = "TThreshold must be positive", Status = "NACK" };
                    if (meta.RHThreshold <= 0)
                        return new WeatherAck { Success = false, Message = "RHThreshold must be positive", Status = "NACK" };
                    if (meta.DEWThreshold <= 0)
                        return new WeatherAck { Success = false, Message = "DEWThreshold must be positive", Status = "NACK" };
                    if (meta.DeviationPercent <= 0 || meta.DeviationPercent > 100)
                        return new WeatherAck { Success = false, Message = "DeviationPercent must be between 0 and 100", Status = "NACK" };

                    _currentSessionId = string.IsNullOrWhiteSpace(meta.SessionId) ? Guid.NewGuid().ToString("N") : meta.SessionId;
                    _tThreshold = meta.TThreshold;
                    _rhThreshold = meta.RHThreshold;
                    _dewThreshold = meta.DEWThreshold;
                    _deviationPct = meta.DeviationPercent;

                    string sessionDir = Path.Combine(storageRoot, _currentSessionId);
                    Directory.CreateDirectory(sessionDir);
                    
                    _resourceManager.InitializeStreams(sessionDir);

                    // Reset analytics state
                    _lastTemperature = null;
                    _lastRH = null;
                    _lastTdew = null;
                    _runningMeanT = 0;
                    _runningMeanRH = 0;
                    _runningMeanTdew = 0;
                    _count = 0;
                    _written = 0;

                    // Notify external subscribers and log start
                    OnTransferStarted?.Invoke(this, $"Meteorološka sesija {_currentSessionId} pokrenuta u {meta.StartedAt:O}\nFolder: {sessionDir}");
                    Console.WriteLine($"✅ Sesija uspešno pokrenuta - threshold vrednosti: T={_tThreshold}°C, RH={_rhThreshold}%, DEW={_dewThreshold}°C, Odstupanje={_deviationPct}%");
                    
                    return new WeatherAck { Success = true, Message = "Session started", Status = "IN_PROGRESS" };
                }
                catch (Exception ex)
                {
                    return new WeatherAck { Success = false, Message = ex.Message, Status = "NACK" };
                }
            }
        }

        public WeatherAck PushSample(WeatherSample sample)
        {
            lock (_lockObject)
            {
                // Thread-safe: ensures only one thread processes a sample at a time
                try
                {
                    if (_resourceManager?.MeasurementsWriter == null)
                    {
                        Console.WriteLine("\n❌ Sesija nije pokrenuta");
                        return new WeatherAck { Success = false, Message = "Session not started", Status = "NACK" };
                    }

                    Console.WriteLine($"\n📥 Primljen uzorak: T={sample.T:F2}°C, RH={sample.Rh:F2}%, Tdew={sample.Tdew:F2}°C, P={sample.Pressure:F2}mbar");

                    // Validate sample
                    var valid = ValidateSample(sample, out string valError);
                    if (!valid)
                    {
                        Console.WriteLine($"\n❌ Server odbacio uzorak: {valError}");
                        _resourceManager.RejectsWriter?.WriteLine($"\"{valError.Replace("\"", "\"\"")}\",\"{SerializeSample(sample).Replace("\"", "\"\"")}\"");
                        return new WeatherAck { Success = false, Message = valError, Status = "IN_PROGRESS" };
                    }

                    // Write to measurements file
                    try
                    {
                        _resourceManager.MeasurementsWriter.WriteLine(string.Join(",",
                            sample.Date.ToString("O"),
                            sample.T.ToString(CultureInfo.InvariantCulture),
                            sample.Pressure.ToString(CultureInfo.InvariantCulture),
                            sample.Tpot.ToString(CultureInfo.InvariantCulture),
                            sample.Tdew.ToString(CultureInfo.InvariantCulture),
                            sample.Rh.ToString(CultureInfo.InvariantCulture),
                            sample.Sh.ToString(CultureInfo.InvariantCulture)));
                        _written++;

                        // Log first few samples fully for debugging
                        if (_written <= 3)
                        {
                            Console.WriteLine($"\n✅ Server prihvatio uzorak {_written}: {sample}");
                        }
                        if (_written % 20 == 0) Console.Write(" ");
                    }
                    catch (Exception ioex)
                    {
                        _resourceManager.RejectsWriter?.WriteLine($"\"WriteError: {ioex.Message.Replace("\"", "\"\"")}\";\"{SerializeSample(sample).Replace("\"", "\"\"")}\"");
                        return new WeatherAck { Success = false, Message = ioex.Message, Status = "IN_PROGRESS" };
                    }

                    // ===== ANALYTICS 1: Detection of sudden temperature change (ΔT) =====
                    if (_lastTemperature.HasValue)
                    {
                        double deltaT = sample.T - _lastTemperature.Value;
                        if (Math.Abs(deltaT) > _tThreshold)
                        {
                            string direction = deltaT > 0 ? "IZNAD očekivanog" : "ISPOD očekivanog";
                            string message = $"🔴 TEMPERATURE SPIKE: ΔT={deltaT:F3}°C ({direction}) | Threshold: {_tThreshold:F3}°C";
                            string csvMessage = $"TEMPERATURE SPIKE ΔT={deltaT:F3}°C ({direction}) Threshold={_tThreshold:F3}°C";
                            OnWarningRaised?.Invoke(this, message);
                            _resourceManager.AnalyticsWriter?.WriteLine($"{sample.Date:O},TemperatureSpike,{csvMessage},{deltaT:F3},{_tThreshold:F3}");
                        }
                    }
                    _lastTemperature = sample.T;

                    // ===== ANALYTICS 2: Detection of sudden humidity change (ΔRH) =====
                    if (_lastRH.HasValue)
                    {
                        double deltaRH = sample.Rh - _lastRH.Value;
                        if (Math.Abs(deltaRH) > _rhThreshold)
                        {
                            string direction = deltaRH > 0 ? "IZNAD očekivanog" : "ISPOD očekivanog";
                            string message = $"🔴 HUMIDITY SPIKE: ΔRH={deltaRH:F3}% ({direction}) | Threshold: {_rhThreshold:F3}%";
                            string csvMessage = $"HUMIDITY SPIKE ΔRH={deltaRH:F3}% ({direction}) Threshold={_rhThreshold:F3}%";
                            OnWarningRaised?.Invoke(this, message);
                            _resourceManager.AnalyticsWriter?.WriteLine($"{sample.Date:O},HumiditySpike,{csvMessage},{deltaRH:F3},{_rhThreshold:F3}");
                        }
                    }
                    _lastRH = sample.Rh;

                    // ===== ANALYTICS 3: Detection of sudden dew point change (ΔDEW) =====
                    if (_lastTdew.HasValue)
                    {
                        double deltaDEW = sample.Tdew - _lastTdew.Value;
                        if (Math.Abs(deltaDEW) > _dewThreshold)
                        {
                            string direction = deltaDEW > 0 ? "IZNAD očekivanog" : "ISPOD očekivanog";
                            string message = $"🔴 DEW POINT SPIKE: ΔDEW={deltaDEW:F3}°C ({direction}) | Threshold: {_dewThreshold:F3}°C";
                            string csvMessage = $"DEW POINT SPIKE ΔDEW={deltaDEW:F3}°C ({direction}) Threshold={_dewThreshold:F3}°C";
                            OnWarningRaised?.Invoke(this, message);
                            _resourceManager.AnalyticsWriter?.WriteLine($"{sample.Date:O},DewPointSpike,{csvMessage},{deltaDEW:F3},{_dewThreshold:F3}");
                        }
                    }
                    _lastTdew = sample.Tdew;

                    // ===== Running mean and ±25% deviation check for temperature =====
                    _runningMeanT = ((_runningMeanT * _count) + sample.T) / (_count + 1);
                    double lowT = _runningMeanT * (1 - _deviationPct / 100.0);
                    double highT = _runningMeanT * (1 + _deviationPct / 100.0);
                    if (sample.T < lowT)
                    {
                        string message = $"🟡 OUT OF BAND: Temperature ISPOD očekivane vrednosti | T={sample.T:F3}°C < {lowT:F3}°C (Mean: {_runningMeanT:F3}°C)";
                        string csvMessage = $"OUT OF BAND Temperature ISPOD očekivane vrednosti T={sample.T:F3}°C < {lowT:F3}°C Mean={_runningMeanT:F3}°C";
                        OnWarningRaised?.Invoke(this, message);
                        _resourceManager.AnalyticsWriter?.WriteLine($"{sample.Date:O},OutOfBandWarning,{csvMessage},{sample.T:F3},{lowT:F3}");
                    }
                    else if (sample.T > highT)
                    {
                        string message = $"🟡 OUT OF BAND: Temperature IZNAD očekivane vrednosti | T={sample.T:F3}°C > {highT:F3}°C (Mean: {_runningMeanT:F3}°C)";
                        string csvMessage = $"OUT OF BAND Temperature IZNAD očekivane vrednosti T={sample.T:F3}°C > {highT:F3}°C Mean={_runningMeanT:F3}°C";
                        OnWarningRaised?.Invoke(this, message);
                        _resourceManager.AnalyticsWriter?.WriteLine($"{sample.Date:O},OutOfBandWarning,{csvMessage},{sample.T:F3},{highT:F3}");
                    }

                    _count++;

                    OnSampleReceived?.Invoke(this, "sample");
                    Console.WriteLine($"✅ Uzorak {_written} uspešno zapisan u fajl");
                    return new WeatherAck { Success = true, Message = "OK", Status = "IN_PROGRESS" };
                }
                catch (Exception ex)
                {
                    _resourceManager.RejectsWriter?.WriteLine($"\"Error: {ex.Message.Replace("\"", "\"\"")}\",\"{SerializeSample(sample).Replace("\"", "\"\"")}\"");
                    return new WeatherAck { Success = false, Message = ex.Message, Status = "IN_PROGRESS" };
                }
            }
        }

        public WeatherAck EndSession()
        {
            lock (_lockObject)
            {
                if (_resourceManager?.MeasurementsWriter == null)
                    return new WeatherAck { Success = false, Message = "No active session", Status = "NACK" };

                Dispose();
                OnTransferCompleted?.Invoke(this, $"Meteorološka sesija {_currentSessionId} završena (zapisano={_written})");
                return new WeatherAck { Success = true, Message = "Session completed", Status = "COMPLETED" };
            }
        }

        private static string SerializeSample(WeatherSample s)
        {
            if (s == null) return "<null>";
            return $"{s.Date:O},{s.T},{s.Pressure},{s.Tpot},{s.Tdew},{s.Rh},{s.Sh}";
        }

        private static bool ValidateSample(WeatherSample s, out string error)
        {
            error = string.Empty;
            if (s == null) { error = "Sample is null"; return false; }
            
            // Validate Temperature
            if (double.IsNaN(s.T) || double.IsInfinity(s.T))
            { error = $"Invalid Temperature: {s.T}"; return false; }
            
            // Validate Pressure 
            if (double.IsNaN(s.Pressure) || double.IsInfinity(s.Pressure) || s.Pressure <= 0)
            { error = $"Invalid Pressure: {s.Pressure}"; return false; }
            
            // Validate Relative Humidity
            if (double.IsNaN(s.Rh) || double.IsInfinity(s.Rh) || s.Rh < 0 || s.Rh > 100)
            { error = $"Invalid Relative Humidity: {s.Rh}"; return false; }
            
            // Validate Dew Point Temperature
            if (double.IsNaN(s.Tdew) || double.IsInfinity(s.Tdew))
            { error = $"Invalid Dew Point Temperature: {s.Tdew}"; return false; }
            
            // Validate Date
            if (s.Date == default(DateTime))
            { error = $"Invalid Date: {s.Date}"; return false; }
            
            Console.WriteLine($"✅ Validacija uzorka prošla: T={s.T:F2}°C, P={s.Pressure:F2}mbar, RH={s.Rh:F2}%, Tdew={s.Tdew:F2}°C");
            return true;
        }

        public void Dispose()
        {
            lock (_lockObject)
            {
                _resourceManager?.Dispose();
            }
        }
    }
}
