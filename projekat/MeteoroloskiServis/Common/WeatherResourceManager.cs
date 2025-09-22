using System;
using System.IO;

namespace Common
{
    /// <summary>
    /// Implementacija Dispose pattern-a za upravljanje resursima meteorološke stanice
    /// </summary>
    public class WeatherResourceManager : IDisposable
    {
        private FileStream _measurementsStream;
        private StreamWriter _measurementsWriter;
        private FileStream _rejectsStream;
        private StreamWriter _rejectsWriter;
        private FileStream _analyticsStream;
        private StreamWriter _analyticsWriter;
        private bool _disposed = false;

        public FileStream MeasurementsStream => _measurementsStream;
        public StreamWriter MeasurementsWriter => _measurementsWriter;
        public FileStream RejectsStream => _rejectsStream;
        public StreamWriter RejectsWriter => _rejectsWriter;
        public FileStream AnalyticsStream => _analyticsStream;
        public StreamWriter AnalyticsWriter => _analyticsWriter;

        public void InitializeStreams(string sessionDirectory)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WeatherResourceManager));

            try
            {
                // Create measurements stream
                string measurementsPath = Path.Combine(sessionDirectory, "measurements_session.csv");
                _measurementsStream = new FileStream(measurementsPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                _measurementsWriter = new StreamWriter(_measurementsStream) { AutoFlush = true };

                // Create rejects stream
                string rejectsPath = Path.Combine(sessionDirectory, "rejects.csv");
                _rejectsStream = new FileStream(rejectsPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                _rejectsWriter = new StreamWriter(_rejectsStream) { AutoFlush = true };

                // Create analytics stream
                string analyticsPath = Path.Combine(sessionDirectory, "analytics_alerts.csv");
                _analyticsStream = new FileStream(analyticsPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                _analyticsWriter = new StreamWriter(_analyticsStream) { AutoFlush = true };

                // Write headers to match our parsed format
                _measurementsWriter.WriteLine("Date,T,Pressure,Tpot,Tdew,Rh,Sh");
                _rejectsWriter.WriteLine("Reason,Line");
                _analyticsWriter.WriteLine("Timestamp,AlertType,Message,Value,Threshold");
            }
            catch (Exception ex)
            {
                // Cleanup on initialization failure
                Dispose();
                throw new InvalidOperationException($"Failed to initialize streams: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    try { _measurementsWriter?.Flush(); _measurementsWriter?.Dispose(); } catch { }
                    try { _measurementsStream?.Dispose(); } catch { }
                    try { _rejectsWriter?.Flush(); _rejectsWriter?.Dispose(); } catch { }
                    try { _rejectsStream?.Dispose(); } catch { }
                    try { _analyticsWriter?.Flush(); _analyticsWriter?.Dispose(); } catch { }
                    try { _analyticsStream?.Dispose(); } catch { }

                    _measurementsWriter = null;
                    _measurementsStream = null;
                    _rejectsWriter = null;
                    _rejectsStream = null;
                    _analyticsWriter = null;
                    _analyticsStream = null;
                }

                _disposed = true;
            }
        }

        ~WeatherResourceManager()
        {
            Dispose(false);
        }

        /// <summary>
        /// Test metoda za simulaciju izuzetka tokom prenosa
        /// </summary>
        public void SimulateTransferException()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WeatherResourceManager));

            throw new InvalidOperationException("Simulated transfer interruption - testing resource cleanup");
        }
    }
}
