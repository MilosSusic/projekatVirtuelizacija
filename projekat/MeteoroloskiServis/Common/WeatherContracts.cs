using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Common
{
    [DataContract]
    public class WeatherSessionMeta
    {
        [DataMember]
        public string SessionId { get; set; }

        [DataMember]
        public DateTime StartedAt { get; set; }

        [DataMember]
        public double T { get; set; }

        [DataMember]
        public double Pressure { get; set; }

        [DataMember]
        public double Tpot { get; set; }

        [DataMember]
        public double Tdew { get; set; }

        [DataMember]
        public double Rh { get; set; }

        [DataMember]
        public double Sh { get; set; }

        [DataMember]
        public DateTime Date { get; set; }

        [DataMember]
        public double TThreshold { get; set; }

        [DataMember]
        public double RHThreshold { get; set; }

        [DataMember]
        public double DEWThreshold { get; set; }

        [DataMember]
        public double DeviationPercent { get; set; }
    }

    [DataContract]
    public class WeatherAck
    {
        [DataMember]
        public bool Success { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string Status { get; set; }
    }

    [ServiceContract]
    public interface IWeatherService
    {
        [OperationContract]
        [FaultContract(typeof(CustomException))]
        WeatherAck StartSession(WeatherSessionMeta meta);

        [OperationContract]
        [FaultContract(typeof(CustomException))]
        WeatherAck PushSample(WeatherSample sample);

        [OperationContract]
        [FaultContract(typeof(CustomException))]
        WeatherAck EndSession();
    }
}
