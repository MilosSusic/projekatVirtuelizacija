using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [DataContract] // Koristi se za prenos informacija o greškama u WCF servisu
    public class CustomException
    {
        string message;

        // Inicijalizuje izuzetak sa porukom
        public CustomException(string message)
        {
            this.Message = message;
        }

        [DataMember] // Serijalizuje poruku izuzetka za WCF prenos
        public string Message { get => message; set => message = value; }
    }
}
