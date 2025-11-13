using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Reflection;
using System.Linq;
using RotinaIntegracaoCRMvalidaHt.Properties;
using System.IO;
using RotinaIntegracaoCRMvalidaHt.Connects;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static RotinaIntegracaoCRMvalidaHt.Model.Models;
using Newtonsoft.Json.Linq;
using MySqlX.XDevAPI;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Vml.Office;
using static Org.BouncyCastle.Math.EC.ECCurve;


namespace RotinaIntegracaoCRMvalidaHt
{
    public partial class Form1 : Form
    {
        public bool teste;
        public string dataTeste;
        public string data;
        public string versao;
        
        List<Relatorio> listFormadoresNotificados = new List<Relatorio>();

        
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

            if (passedInArgs.Contains("-a") || passedInArgs.Contains("-A"))
            {
                Cursor.Current = Cursors.WaitCursor;
                await EnviarEmailLembreteAulaFormadoresAsync();
                Cursor.Current = Cursors.Default;
                System.Windows.Forms.Application.Exit();
            }
        }
       
        private async void button1_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            await EnviarEmailLembreteAulaFormadoresAsync();
            Cursor.Current = Cursors.Default;
        }
        
        private async Task EnviarEmailLembreteAulaFormadoresAsync()
        {
            try
            {
                Connect.HTlocalConnect.ConnInit();

                DateTime dataRotina = DateTime.Now.AddDays(-1);

                string queryFormandos = $@"
                       SELECT 
                            f.Codigo_Formando, 
                            f.Nome_Abreviado, 
                            f.Data_Nascimento, 
                            c.Email1, 
                            c.Telefone1, 
                            i.Data_Inscricao, 
                            i.versao_data as Data_Inscricaorowid
                        FROM TBForFormandos f 
                        LEFT JOIN TBForInscricoes i ON f.Codigo_Formando = i.Codigo_Formando 
                        LEFT JOIN TBForAccoes a ON i.Rowid_Accao = a.versao_rowid 
                        INNER JOIN TBGerContactos c ON f.versao_rowid = c.Codigo_Entidade AND c.Tipo_Entidade=3
                        WHERE i.Data_Inscricao = '{dataRotina.ToString("yyyy-MM-dd")}';
                        ";

                SqlDataAdapter adapterFormandos = new SqlDataAdapter(queryFormandos, Connect.HTlocalConnect.Conn);
                DataTable dataTableFormandos = new DataTable();
                adapterFormandos.Fill(dataTableFormandos);
                var inscricoes = dataTableFormandos.AsEnumerable().Select(row => new Formador
                {
                    CodigoFormando = row["Codigo_Formando"].ToString(),
                    NomeAbreviado = row["Nome_Abreviado"].ToString(),
                    DataNascimento = DateTime.TryParse(row["Data_Nascimento"]?.ToString(), out var dataNascimento) ? dataNascimento : DateTime.MinValue,
                    Email = row["Email1"].ToString(),
                    Telefone = row["Telefone1"].ToString(),
                    DataInscricao = DateTime.TryParse(row["Data_Inscricao"]?.ToString(), out var dataInscricao) ? dataInscricao : DateTime.MinValue,
                    DataInscricaoRowId = DateTime.TryParse(row["Data_Inscricaorowid"]?.ToString(), out var dataInscricaoRowId) ? dataInscricaoRowId : DateTime.MinValue
                }).ToList();

                Connect.HTlocalConnect.ConnEnd();

                foreach (var inscricao in inscricoes)
                {
                    Relatorio relatorioFinal = new Relatorio()
                    {
                        NomeFormador = inscricao.NomeAbreviado,
                        EmailFormador = inscricao.Email,
                        Telemovel = inscricao.Telefone
                    };

                    var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(60); // timeout

                    // Construir a requisição
                    var request = new HttpRequestMessage(HttpMethod.Post, "http://192.168.1.213:8080/api/egoi/adicionar-contato"); // producao
                    ////var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5141/api/egoi/adicionar-contato"); // Ambiente Teste

                    // Formatação do telefone para o formato necessário (se ainda não estiver no formato 351-xxxxxxxxx)
                    string formattedPhone = inscricao.Telefone;
                    if (!string.IsNullOrEmpty(formattedPhone))
                    {
                        // Remover caracteres não numéricos
                        formattedPhone = Regex.Replace(formattedPhone, @"[^\d]", "");
                        
                        // Adicionar prefixo se não começar com 351
                        if (!formattedPhone.StartsWith("351"))
                            formattedPhone = "351" + formattedPhone;
                        
                        // Formato final 351-xxxxxxxxx
                        formattedPhone = formattedPhone.Insert(3, "-");
                    }

                    // Preparar data de nascimento no formato correto
                    string birthDate = inscricao.DataNascimento.ToString("yyyy-MM-dd");

                    var content = new StringContent(
                        JsonConvert.SerializeObject(new
                        {
                            @base = new
                            {
                                status = "active",
                                first_name = inscricao.NomeAbreviado,
                                birth_date = birthDate,
                                email = inscricao.Email,
                                cellphone = formattedPhone,
                                phone = formattedPhone
                            },
                            listid = 1, // 1 - Lista do Instituto CRIAP / 2 - Lista do Business
                            tagid = 7,   // Tag HT
                            referrer = new
                            {
                                referrer = "HT"
                            }
                        }),
                        Encoding.UTF8,
                        "application/json"
                    );

                    request.Content = content;

                    // Enviar a requisição e obter a resposta
                    var response = await client.SendAsync(request);

                    // Ler o conteúdo da resposta
                    var responseContent = await response.Content.ReadAsStringAsync();

                    try
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            // Deserializar o retorno da API
                            var egoiResponse = JsonConvert.DeserializeObject<EgoiApiResponse>(responseContent);

                            // Adicionar detalhes da resposta ao relatório com ContactId
                            relatorioFinal.ApiEgoiResponse = $"Sucesso (ContactId: {egoiResponse?.ContactId ?? "N/A"})";

                            if (!string.IsNullOrEmpty(egoiResponse?.ContactId))
                            {
                                RegistraLog(inscricao.CodigoFormando.ToString(),
                                    $"Importando formando para o Egoi. ContactId: {egoiResponse.ContactId}",
                                    "Importacao Egoi", "egoi");
                            }
                        }
                        else
                        {
                            // Adicionar detalhes da resposta ao relatório com mensagem de erro
                            relatorioFinal.ApiEgoiResponse = $"Erro: {responseContent}";

                            RegistraLog(inscricao.CodigoFormando.ToString(),
                                $"Erro ao importar para o Egoi: {responseContent}",
                                "Importacao Egoi", "egoi");
                        }

                        listFormadoresNotificados.Add(relatorioFinal);
                    }
                    catch (JsonException ex)
                    {
                        // Em caso de erro na deserialização, registrar o erro
                        string errorMessage = $"Erro ao processar resposta da API: {ex.Message}. Resposta: {responseContent}";
                        RegistraLog(inscricao.CodigoFormando.ToString(), errorMessage, "Importacao Egoi Erro", "egoi");

                        relatorioFinal.ApiEgoiResponse = "Erro ao processar resposta";
                        listFormadoresNotificados.Add(relatorioFinal);

                        if (!response.IsSuccessStatusCode)
                        {
                            sendEmail($"Erro ao chamar a API: {response.StatusCode}. Detalhes: {responseContent}",
                                Settings.Default.emailenvio, true, "informatica", "");
                        }
                    }
                }

                sendEmailRelatorio();
            }
            catch (Exception ex)
            {
                sendEmail(ex.ToString(), Settings.Default.emailenvio, true, "informatica", "");
            }
        }

        
        private void sendEmailRelatorio()
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
            if (listFormadoresNotificados != null && listFormadoresNotificados.Count > 0)
            {
                StringBuilder relatorio = new StringBuilder();
                relatorio.AppendLine("Relatório de Importação de contatos HT para o Egoi (Alunos - Inscrições):<br><br>");

                foreach (var relFormador in listFormadoresNotificados)
                {
                    relatorio.AppendLine($"<b>Incrição (Aluno):</b> {relFormador.NomeFormador}  |  Email: {relFormador.EmailFormador} |  ApiEgoi: {relFormador.ApiEgoiResponse}<br>");
                }
                body = relatorio.ToString();
            }
           
            mm.Subject = "Instituto CRIAP || Relatório - Importação de contatos HT para o Egoi (Alunos - Inscrições)" + data;
            mm.BodyEncoding = UTF8Encoding.UTF8;
            mm.IsBodyHtml = true;
            mm.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;
            mm.Body = body + "<br> " + versao;
            client.Send(mm);
        }

        private void sendEmail(string body, string tecnica = "", bool error = false, string emailPessoa = "", string temp = "", Formador acao = null, List<Attachment> attachments = null, string coordenadorEmail="")
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

            if (!error)
            {
                if (!teste)
                {
                    mm.CC.Add("informatica@criap.com");   
                }
                else
                {
                    mm.To.Add("raphaelcastro@criap.com");
                }
            }
            else if (!teste)
            {
                mm.To.Add("informatica@criap.com");
            }
            else
            {
                mm.To.Add("raphaelcastro@criap.com");
            }

            mm.Subject = (!teste) ? "Importação HT para o Egoi  / " : data + " TESTE - Importação HT para o Egoi - Aluno // ";

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