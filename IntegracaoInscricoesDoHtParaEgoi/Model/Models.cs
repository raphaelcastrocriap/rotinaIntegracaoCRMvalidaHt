using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegracaoInscricoesDoHtParaEgoi.Model
{
    public class Models
    {
        public class Formador
        {
            public string CodigoFormando { get; set; }
            public string NomeAbreviado { get; set; }
            public DateTime DataNascimento { get; set; }
            public string Email { get; set; }
            public string Telefone { get; set; }
            public DateTime DataInscricao { get; set; }
            public DateTime DataInscricaoRowId { get; set; }
        }

        public class objLogSendersFormando
        {
            public string idFormando { get; set; }
            public string mensagem { get; set; }
            public string menu { get; set; }
            public string refAccao { get; set; }
        }

        public class Relatorio
        {
            public string NomeFormador { get; set; }
            public string EmailFormador { get; set; }
            public string Telemovel { get; set; }
            public string ApiEgoiResponse { get; set; }
        }

        public class EgoiApiResponse
        {
            public string Message { get; set; }
            public string ContactId { get; set; }
        }
    }
}
