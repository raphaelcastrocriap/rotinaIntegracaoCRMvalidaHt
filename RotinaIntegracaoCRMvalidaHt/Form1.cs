using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Reflection;
using System.Linq;
using System.Windows.Forms;
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
        
        string relatorioHtml = "";

        
        public Form1()
        {
            InitializeComponent();
            Security.remote();
        }
        private async void Form1_Load(object sender, EventArgs e)
        {
            teste = false;

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
                // Chamar API de validação de leads
                var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(5); // Timeout maior para processamento

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5141/api/crm/validar-leads-ht"); // teste
                //var request = new HttpRequestMessage(HttpMethod.Post, "http://192.168.1.213:8080/api/crm/validar-leads-ht"); // teste

                var requestContent = new
                {
                    data_referencia = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
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
                        var apiResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);
                        
                        if (apiResponse?.relatorio_html != null)
                        {
                            relatorioHtml = apiResponse.relatorio_html.ToString();
                            
                            // Enviar relatório por email
                            sendEmailRelatorioLeads();
                        }
                        else
                        {
                            sendEmail("Erro: Resposta da API não contém o relatório HTML esperado.", "", true, "", "");
                        }
                    }
                    catch (JsonException ex)
                    {
                        sendEmail($"Erro ao processar resposta da API: {ex.Message}. Resposta: {responseContent}", "", true, "", "");
                    }
                }
                else
                {
                    sendEmail($"Erro na API de validação: {response.StatusCode} - {responseContent}", "", true, "", "");
                }
            }
            catch (Exception ex)
            {
                sendEmail($"Erro na rotina de validação de leads: {ex.ToString()}", "", true, "", "");
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
            }
            else
            {
                mm.To.Add("raphaelcastro@criap.com");
            }
           
            mm.Subject = "Instituto CRIAP || Relatório - Validação de Leads CRM com validação do HT";
            mm.BodyEncoding = UTF8Encoding.UTF8;
            mm.IsBodyHtml = true;
            mm.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;
            mm.Body = relatorioHtml + "<br><br>" + versao;
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