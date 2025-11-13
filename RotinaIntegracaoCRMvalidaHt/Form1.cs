using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using System.Data.SqlClient;
using MySql.Data.MySqlClient;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Reflection;
using System.Linq;
using RotinaIntegracaoCRMvalidaHt.Properties;
using RotinaIntegracaoCRMvalidaHt.Connects;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static RotinaIntegracaoCRMvalidaHt.Model.Models;


namespace RotinaIntegracaoCRMvalidaHt
{
    public partial class Form1 : Form
    {
        public bool teste;
        public string dataTeste;
        public string data;
        public string versao;
        
        List<RelatorioLead> listLeadsValidados = new List<RelatorioLead>();
        DateTime periodoInicio;
        DateTime periodoFim;

        
        public Form1()
        {
            InitializeComponent();
            Security.remote();
        }
        private async void Form1_Load(object sender, EventArgs e)
        {
            teste = true;

            Security.remote();
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            this.Text += " V." + v.Major.ToString() + "." + v.Minor.ToString() + "." + v.Build.ToString();
            versao = @" <br><font size=""-2"">Controlo de versão: " + " V." + v.Major.ToString() + "." + v.Minor.ToString() + "." + v.Build.ToString() + " Assembly built date: " + System.IO.File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location) + " by rc";

            string[] passedInArgs = Environment.GetCommandLineArgs();

            if (passedInArgs.Contains("-v") || passedInArgs.Contains("-V"))
            {
                Cursor.Current = Cursors.WaitCursor;
                await ValidarLeadsCRMAsync();
                Cursor.Current = Cursors.Default;
                System.Windows.Forms.Application.Exit();
            }
        }
       
        private async void button1_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            await ValidarLeadsCRMAsync();
            Cursor.Current = Cursors.Default;
        }


        private async Task ValidarLeadsCRMAsync()
        {
            try
            {
                // Data de início da rotina
                DateTime dataInicio = DateTime.Now;

                Connect.CRMConnect.ConnInit();

                // Query para buscar a última data de execução
                string queryUltimaExecucao = @"
                    SELECT MAX(ultima_data_execucao) AS ultima_data_execucao 
                    FROM log_controle_rotina 
                    WHERE nome_rotina = 'rotina_validacao_ht_crm'";

                MySqlCommand cmdUltimaExecucao = new MySqlCommand(queryUltimaExecucao, Connect.CRMConnect.Conn);
                object result = cmdUltimaExecucao.ExecuteScalar();
                
                DateTime ultimaExecucao = result != null && result != DBNull.Value 
                    ? Convert.ToDateTime(result) 
                    : DateTime.Now.AddDays(-30); // Se não houver registro, buscar últimos 30 dias

                // Armazenar o período para o relatório
                periodoInicio = ultimaExecucao;
                periodoFim = dataInicio;

                // Query para buscar leads novos/modificados
                string queryLeads = $@"
                    SELECT l.id, l.date_entered, l.date_modified, l.modified_user_id, l.description, l.deleted, 
                           ea.email_address, CONCAT_WS(' ', l.first_name, l.last_name) AS nome_completo, 
                           COALESCE(l.phone_mobile, l.phone_home, l.phone_work) AS phone, l.lead_source, 
                           l.lead_source_description, l.status, l.status_description
                    FROM leads l
                    LEFT JOIN email_addr_bean_rel ebr ON ebr.bean_id = l.id AND ebr.bean_module = 'Leads'
                    LEFT JOIN email_addresses ea ON ea.id = ebr.email_address_id
                    WHERE l.deleted = 0 
                      AND l.date_entered >= '{ultimaExecucao:yyyy-MM-dd HH:mm:ss}' 
                      AND l.date_entered <= '{dataInicio:yyyy-MM-dd HH:mm:ss}'";

                MySqlDataAdapter adapterLeads = new MySqlDataAdapter(queryLeads, Connect.CRMConnect.Conn);
                DataTable dataTableLeads = new DataTable();
                adapterLeads.Fill(dataTableLeads);

                var leads = dataTableLeads.AsEnumerable().Select(row => new Lead
                {
                    Id = row["id"].ToString(),
                    DateEntered = DateTime.TryParse(row["date_entered"]?.ToString(), out var dateEntered) ? dateEntered : DateTime.MinValue,
                    DateModified = DateTime.TryParse(row["date_modified"]?.ToString(), out var dateModified) ? dateModified : DateTime.MinValue,
                    ModifiedUserId = row["modified_user_id"].ToString(),
                    Description = row["description"].ToString(),
                    Deleted = row["deleted"].ToString(),
                    EmailAddress = row["email_address"].ToString(),
                    NomeCompleto = row["nome_completo"].ToString(),
                    Phone = row["phone"].ToString(),
                    LeadSource = row["lead_source"].ToString(),
                    LeadSourceDescription = row["lead_source_description"].ToString(),
                    Status = row["status"].ToString(),
                    StatusDescription = row["status_description"].ToString()
                }).ToList();

                foreach (var lead in leads)
                {
                    if (string.IsNullOrEmpty(lead.NomeCompleto) || string.IsNullOrEmpty(lead.EmailAddress))
                        continue;

                    RelatorioLead relatorioLead = new RelatorioLead()
                    {
                        NomeLead = lead.NomeCompleto,
                        EmailLead = lead.EmailAddress,
                        TelefoneLead = lead.Phone
                    };

                    try
                    {
                        // Chamar API de validação
                        var client = new HttpClient();
                        client.Timeout = TimeSpan.FromSeconds(60);

                        // Configurar autenticação Basic Auth
                        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("informatica.admin:xSyNN85kann#02X"));
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

                        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5141/api/formando/verificar-cadastro-ht"); // teste

                        var requestContent = new
                        {
                            nome_completo = lead.NomeCompleto,
                            email_address = lead.EmailAddress,
                            phone = lead.Phone
                        };

                        var content = new StringContent(
                            JsonConvert.SerializeObject(requestContent),
                            Encoding.UTF8,
                            "application/json"
                        );

                        request.Content = content;

                        // Enviar requisição
                        var response = await client.SendAsync(request);
                        var responseContent = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            try
                            {
                                var apiResponse = JsonConvert.DeserializeObject<ValidacaoHtApiResponse>(responseContent);
                                
                                if (apiResponse != null && !string.IsNullOrEmpty(apiResponse.Resultado))
                                {
                                    // Atualizar lead no CRM
                                    string updateQuery = @"
                                        UPDATE leads 
                                        SET status_description = @status_description,
                                            modified_user_id = '60fbbaec-ad35-8fef-c1d1-6479ab72ef54',
                                            date_modified = NOW()
                                        WHERE id = @lead_id";

                                    MySqlCommand updateCmd = new MySqlCommand(updateQuery, Connect.CRMConnect.Conn);
                                    updateCmd.Parameters.AddWithValue("@status_description", apiResponse.Resultado);
                                    updateCmd.Parameters.AddWithValue("@lead_id", lead.Id);
                                    updateCmd.ExecuteNonQuery();

                                    relatorioLead.ValidacaoResponse = $"Sucesso: {apiResponse.Resultado}";
                                }
                                else
                                {
                                    relatorioLead.ValidacaoResponse = "Resposta da API inválida";
                                }
                            }
                            catch (JsonException ex)
                            {
                                relatorioLead.ValidacaoResponse = $"Erro ao processar resposta: {ex.Message}";
                            }
                        }
                        else
                        {
                            relatorioLead.ValidacaoResponse = $"Erro API: {response.StatusCode} - {responseContent}";
                        }

                        listLeadsValidados.Add(relatorioLead);
                    }
                    catch (Exception ex)
                    {
                        relatorioLead.ValidacaoResponse = $"Erro: {ex.Message}";
                        listLeadsValidados.Add(relatorioLead);
                    }
                }

                // Salvar log de controle da rotina
                string insertLogQuery = @"
                    INSERT INTO log_controle_rotina (nome_rotina, ultima_data_execucao) 
                    VALUES ('rotina_validacao_ht_crm', @data_inicio)
                    ON DUPLICATE KEY UPDATE ultima_data_execucao = @data_inicio";

                MySqlCommand insertLogCmd = new MySqlCommand(insertLogQuery, Connect.CRMConnect.Conn);
                insertLogCmd.Parameters.AddWithValue("@data_inicio", dataInicio);
                insertLogCmd.ExecuteNonQuery();

                Connect.CRMConnect.ConnEnd();

                // Enviar relatório por email
                sendEmailRelatorioLeads();
            }
            catch (Exception ex)
            {
                sendEmail($"Erro na rotina de validação de leads: {ex.ToString()}", Settings.Default.emailenvio, true, "informatica", "");
            }
        }



        private void sendEmailRelatorioLeads()
        {
            NetworkCredential basicCredential = new NetworkCredential(Settings.Default.emailenvio, Settings.Default.passwordemail);
            SmtpClient client = new SmtpClient();
            client.Port = 25;
            client.Host = "mail.criap.com";
            client.Timeout = 10000;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.Credentials = basicCredential;

            MailMessage mm = new MailMessage();
            mm.From = new MailAddress("Instituto CRIAP <" + Settings.Default.emailenvio + "> ");
            if (!teste)
            {
                mm.To.Add("informatica@criap.com");
                mm.To.Add("brunaarouca@criap.com");
            }
            else
            {
                mm.To.Add("raphaelcastro@criap.com");
            }
            
            string body = "";

            // Mostra relatório
            StringBuilder relatorio = new StringBuilder();
            relatorio.AppendLine("Relatório de Validação de Leads CRM com HT:<br>");
            relatorio.AppendLine($"<b>Período processado:</b> {periodoInicio:dd/MM/yyyy HH:mm:ss} até {periodoFim:dd/MM/yyyy HH:mm:ss}<br><br>");

            if (listLeadsValidados != null && listLeadsValidados.Count > 0)
            {
                relatorio.AppendLine($"<b>Total de leads processados:</b> {listLeadsValidados.Count}<br><br>");

                foreach (var relLead in listLeadsValidados)
                {
                    relatorio.AppendLine($"<b>Lead:</b> {relLead.NomeLead}  |  Email: {relLead.EmailLead} |  Telefone: {relLead.TelefoneLead} |  Validação: {relLead.ValidacaoResponse}<br>");
                }
            }
            else
            {
                relatorio.AppendLine("Nenhum lead foi encontrado no período especificado.<br>");
            }
            
            body = relatorio.ToString();
           
            mm.Subject = "Instituto CRIAP || Relatório - Validação de Leads CRM com HT" + data;
            mm.BodyEncoding = UTF8Encoding.UTF8;
            mm.IsBodyHtml = true;
            mm.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;
            mm.Body = body + "<br> " + versao;
            client.Send(mm);
        }

        private void sendEmail(string body, string tecnica = "", bool error = false, string emailPessoa = "", string temp = "")
        {
            NetworkCredential basicCredential = new NetworkCredential(Settings.Default.emailenvio, Settings.Default.passwordemail);
            SmtpClient client = new SmtpClient();
            client.Port = 25;
            client.Host = "mail.criap.com";
            client.Timeout = 10000;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.Credentials = basicCredential;

            MailMessage mm = new MailMessage();
            mm.From = new MailAddress("Instituto CRIAP <" + Settings.Default.emailenvio + "> ");

            if (!teste)
            {
                mm.To.Add("informatica@criap.com");
            }
            else
            {
                mm.To.Add("raphaelcastro@criap.com");
            }

            mm.Subject = (!teste) ? "Instituto CRIAP || Erro - Validação de Leads CRM" : data + " TESTE - Erro - Validação de Leads CRM";

            mm.BodyEncoding = UTF8Encoding.UTF8;
            mm.IsBodyHtml = true;
            mm.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;
            mm.Body = body + "<br> " + temp + (teste ? versao : "");

            client.Send(mm);
        }


        public void RegistraLog(string id, string mensagem, string menu, string refAcao)
        {
            List<objLogSendersFormando> logSenders = new List<objLogSendersFormando>();
            logSenders.Add(new objLogSendersFormando
            {
                idFormando = id,
                mensagem = mensagem,
                menu = menu,
                refAccao = refAcao
            });
            DataBaseLogSave(logSenders);
        }

        public static void DataBaseLogSave(List<objLogSendersFormando> logSenders)
        {
            if (logSenders.Count > 0)
            {
                string subQuery = "INSERT INTO sv_logs (idFormando, refAcao, dataregisto, registo, menu, username) VALUES ";
                for (int i = 0; i < logSenders.Count; i++)
                {
                    if (i < logSenders.Count - 1)
                        subQuery += "('" + logSenders[i].idFormando + "', '" + logSenders[i].refAccao + "', GETDATE(), '" + logSenders[i].mensagem + "', '" + logSenders[i].menu + "', 'system_rotina'), ";
                    else subQuery += "('" + logSenders[i].idFormando + "', '" + logSenders[i].refAccao + "', GETDATE(), '" + logSenders[i].mensagem + "', '" + logSenders[i].menu + "', 'system_rotina') ";
                }
                Connect.SVlocalConnect.ConnInit();
                SqlCommand cmd = new SqlCommand(subQuery, Connect.SVlocalConnect.Conn);
                cmd.ExecuteNonQuery();
                Connect.SVlocalConnect.ConnEnd();
                Connect.closeAll();
            }
        }

    }

}