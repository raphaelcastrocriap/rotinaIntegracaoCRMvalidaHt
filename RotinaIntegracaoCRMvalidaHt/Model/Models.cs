using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RotinaIntegracaoCRMvalidaHt.Model
{
    public class Models
    {
        public class objLogSendersFormando
        {
            public string idFormando { get; set; }
            public string mensagem { get; set; }
            public string menu { get; set; }
            public string refAccao { get; set; }
        }

        public class Lead
        {
            public string Id { get; set; }
            public DateTime DateEntered { get; set; }
            public DateTime DateModified { get; set; }
            public string ModifiedUserId { get; set; }
            public string Description { get; set; }
            public string Deleted { get; set; }
            public string EmailAddress { get; set; }
            public string NomeCompleto { get; set; }
            public string Phone { get; set; }
            public string LeadSource { get; set; }
            public string LeadSourceDescription { get; set; }
            public string Status { get; set; }
            public string StatusDescription { get; set; }
        }

        public class ValidacaoHtApiResponse
        {
            public string Resultado { get; set; }
        }

        public class RelatorioLead
        {
            public string NomeLead { get; set; }
            public string EmailLead { get; set; }
            public string TelefoneLead { get; set; }
            public string ValidacaoResponse { get; set; }
        }
    }
}
