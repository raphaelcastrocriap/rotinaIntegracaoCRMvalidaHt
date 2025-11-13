using Microsoft.Office.Interop.Word;
using MySql.Data.MySqlClient;
using System;
using System.Windows.Forms;

namespace RotinaIntegracaoCRMvalidaHt.Connects
{
    class Connect_CRM_Server
    {
        private string DBserver;
        private string DBuid;
        private string DBpwd;
        private string DBdatabase;
        private MySqlConnection Connection;
        public static string ConnectionString = null;

        public Connect_CRM_Server(string server, string uid, string pwd, string database)
        {
            DBserver = server;
            DBuid = uid;
            DBpwd = pwd;
            DBdatabase = database;
            ConnectionString = "server='" + server + "';uid='" + uid + "';pwd='" + pwd + "';database='" + database + "'; pooling = false; convert zero datetime=True; default command timeout=20;Allow User Variables=True;";
            Connection = new MySqlConnection(ConnectionString);
        }

        public Boolean TestConnect()
        {
            try
            {
                Connection.Close();
            }
            catch { }

            try
            {
                Connection.Open();
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                Connection.Close();
            }
        }

        public void ConnInit()
        {
            try
            {
                Connection.Close();
                Connection.Open();
            }
            catch (Exception e)
            {
                MessageBox.Show("Ocorreu um erro Suite CRM Server!\n\n" + e.ToString(), "Erro CRM", MessageBoxButtons.OK, MessageBoxIcon.Error);

            }
        }

        public void ConnEnd()
        {
            Connection.Close();
        }

        public MySqlConnection Conn { get { return Connection; } }
    }
}
