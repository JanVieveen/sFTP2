using System;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using System.IO;
using System.Net;

using System.Collections;

using System.Data.SqlClient;
using System.Xml;


using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using System.Globalization;
using System.Text.RegularExpressions;

using RestSharp;
using RestSharp.Authenticators;
using RestSharp.Deserializers;
using RestSharp.Extensions;
using RestSharp.Serializers;
using RestSharp.Validation;

using Newtonsoft.Json;

/* X12
https://www-03.ibm.com/procurement/proweb.nsf/objectdocswebview/fileedi+specifications+997+functional+acknowledgment/$file/997+functional+acknowledgment.pdf
*/

namespace sFTP2
{
    public partial class Form1 : Form
    {
        public static string TEMPXMLFN = "temp.xml";
        public static string TEMPXMLFN2 = "temp2.xml";
        public static string TEMPFN = "temp.txt";
        public static string TWDIR = "TW\\";

        public string connectionstring;
        public static SqlConnection SQLconn, SQLconn2, SQLconn3, SQLconnX, SQLconnX2, SQLconnX3;
        public static SqlDataReader SQL_x, SQL_x2, SQL_x3, SQL_xx, SQL_xx2, SQL_xx3;
        public static SqlCommand SQLcmd, SQLcmd2, SQLcmd3, SQLcmdx, SQLcmdx2, SQLcmdx3;
        public static SqlTransaction SQLtransaction, SQLtransaction2;
        public static int database_open = 0;
        public static bool transactionenabled = false;
        public static string UserID = "";

        public static NumberFormatInfo provider = new NumberFormatInfo();
        List <string> FTPnames = new List<string>();

        public XmlWriter XMLWriter = null;

        public int XMLcnt = 0;

        public int linecnt = 0;

        // blok is voor multiple values from the calloff
        int blokcnt = 0;                
        string[] blokvalues = null;
        int memblokline = 0; // herinneren waar terug te springen

        public Form1()
        {
            InitializeComponent();
        }
        //----------------------------------------------------------------------------------------------------------
        static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }
        //----------------------------------------------------------------------------------------------------------
        public static int writelog(string text,string basisstr)
        {
            try
            {
                FileStream fs = new FileStream(basisstr + DateTime.Now.ToString("yyMM") + ".txt", FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                StreamWriter streamwriter = new StreamWriter(fs);
                streamwriter.WriteLine(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "-" + UserID + ": " + text);
                streamwriter.Close();
            }
            catch (Exception ex)
            {
            }
            return (1);
        }
        //----------------------------------------------------------------------------------------------------------
        public static int atol(string ipstr)
        {
            string resstr = "", bstr = "01234567890-.,";
            int i;
            string cstr;
            for (i = 0; i < ipstr.Length; i++)
            {
                if (bstr.IndexOf(ipstr.Substring(i, 1)) >= 0)
                {
                    cstr = ipstr.Substring(i, 1);
                    if ((cstr == ".") || (cstr == ","))
                    {
                        i = 999;
                    }
                    else
                    {
                        resstr += cstr; //ipstr.Substring(i, 1);
                    }
                }
            }
            if (resstr.Length == 0) return (0);
            try
            {
                return (Convert.ToInt32(resstr));
            }
            catch (Exception ex)
            {
                return (0);
            }
        }
        //----------------------------------------------------------------------------------------------------------
        public static long atol2(string ipstr)
        {
            string resstr = "", bstr = "01234567890-.,";
            int i;
            string cstr;

            string[] split = ipstr.Split('-');
            if (split.Length >= 2)
            {
                ipstr = split[1];
            }
            for (i = 0; i < ipstr.Length; i++)
            {
                if (bstr.IndexOf(ipstr.Substring(i, 1)) >= 0)
                {
                    cstr = ipstr.Substring(i, 1);
                    if ((cstr == ".") || (cstr == ","))
                    {
                        i = 999;
                    }
                    else
                    {
                        resstr += cstr; //ipstr.Substring(i, 1);
                    }
                }
            }
            if (resstr.Length == 0) return (0);
            try
            {
                return (Convert.ToInt64(resstr));
            }
            catch (Exception ex)
            {
                return (0);
            }
        }
        //----------------------------------------------------------------------------------------------------------
        public static double atof(string ipstr)
        {
            string resstr = "", bstr = "01234567890-.,";
            int i;
            //ipstr = ipstr.Replace(',', '.');

            for (i = 0; i < ipstr.Length; i++)
            {
                if (bstr.IndexOf(ipstr.Substring(i, 1)) >= 0)
                {
                    resstr += ipstr.Substring(i, 1);
                }
            }
            if (resstr.Length == 0) return (0);
            try
            {
                double fl = Convert.ToDouble(resstr, provider);

                //double.TryParse(resstr, NumberStyles.Any, CultureInfo.InvariantCulture, out fl);
                return (fl);
            }
            catch
            {
                return (0);
            }
        }
        //--------------------------------------------------------------------------
        public string get_unique_fn(string directory, string fn)
        {
            int teller = 0;
            string fn1 = Path.GetFileNameWithoutExtension(fn);
            string ext1 = Path.GetExtension(fn);
            string localfn = directory + fn;

            while ((File.Exists(localfn)) && (teller < 1000))
            {
                localfn = directory + fn1 + "_" + (teller++).ToString() + ext1;
            }

            return (localfn);
        }
        //--------------------------------------------------------------------------
        public string get_system_login()
        {
            //------------- get user info
            string astr = System.Security.Principal.WindowsIdentity.GetCurrent().Name.ToString();
            string[] split = astr.Split(new char[] { '\\' });
            if (split.Length >= 1)
            {
                UserID = split[split.Length - 1];
            }
            else
            {
                UserID = "";
            }
            return (UserID);
        }

        //----------------------------------------------------------------------------------------------------------
        public static void wait(int msec)
        {
            long t_start = (System.DateTime.Now.Second * 1000) + System.DateTime.Now.Millisecond;
            long t_end = t_start;
            while ((t_end - t_start + 60000) % 60000 < msec)
            {
                t_end = (System.DateTime.Now.Second * 1000) + System.DateTime.Now.Millisecond;
                Application.DoEvents();
            }
            Application.DoEvents();
        }
        //----------------------------------------------------------------------------------------------------------
        public static string short_amount(string ipstr)
        {
            double f = atof(ipstr);
            if (Math.Abs(f) < 1000.0)
                return (f.ToString("###"));
            f /= 1000.0;
            if (Math.Abs(f) < 1000.0)
                return (f.ToString("###K"));
            f /= 1000.0;
            return (f.ToString("###M"));
        }
        //----------------------------------------------------------------------------------------------------------
        public int applicationlog(string application, string message, string filename)
        {
            try
            {
                check_databaseopen(1);
                SQLcmdx = new SqlCommand("insert into log (log_user, log_application, log_dt, log_message, log_filename) " +
                                        " values         (@log_user,@log_application,@log_dt,@log_message,@log_filename)", SQLconnX);
                SQLcmdx.Parameters.AddWithValue("log_user", UserID);
                SQLcmdx.Parameters.AddWithValue("log_application", application);
                SQLcmdx.Parameters.AddWithValue("log_dt", DateTime.Now);
                SQLcmdx.Parameters.AddWithValue("log_message", message);
                SQLcmdx.Parameters.AddWithValue("log_filename", filename);
                SQLcmdx.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                writelog("error in applicationlog: " + ex.Message);
            }
            return (1);
        }
        //----------------------------------------------------------------------------------------------------------
        public static int writelog(string text)
        {
            try
            {
                FileStream fs = new FileStream("3Gimport_" + DateTime.Now.ToString("yyMM") + ".txt", FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                StreamWriter streamwriter = new StreamWriter(fs);
                streamwriter.WriteLine(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "-" + UserID + ": " + text);
                streamwriter.Close();
            }
            catch (Exception ex)
            {
            }
            return (1);
        }
        //--------------------------------------------------------------------------
        public static string datestring(DateTime dt)
        {
            return dt.ToString("dd-MM-yyyy");
        }
        //--------------------------------------------------------------------------
        public static string tostring(object x)
        {
            if (x == null) return "";
            try
            {
                return x.ToString();
            }
            catch
            {
            }
            return "";
        }
        //--------------------------------------------------------------------------
        public static int toint(object x)
        {
            if (x == null) return 0;
            try
            {
                if (Convert.ToInt32(x) == 0) return 0;
            }
            catch { }
            try
            {
                if (Convert.ToBoolean(x) == false) return 0;
            }
            catch { }
            try
            {
                if (Convert.ToString(x).Trim().Length == 0) return 0;
            }
            catch { }
            return 1;
        }
        //--------------------------------------------------------------------------
        public static DateTime todt(string ipstr)
        {
            try
            {
                return Convert.ToDateTime(ipstr);
            }
            catch
            {
                return (Convert.ToDateTime("2000-01-01"));
            }
        }
        //--------------------------------------------------------------------------
        public static DateTime todt(object ipstr)
        {
            try
            {
                return Convert.ToDateTime(ipstr);
            }
            catch
            {
                return (Convert.ToDateTime("2000-01-01"));
            }
        }
        //------------------------------------------------------------------------------------
        public static void save_column_layout(string UserID, DataGridView dataGridView1, string filename)
        {
            int i;
            FileStream fs = new FileStream(filename + UserID + ".col", FileMode.Create);
            BinaryWriter w = new BinaryWriter(fs);      // Create the writer for data.

            for (i = 0; i < dataGridView1.Columns.Count; i++)
            {
                string name = dataGridView1.Columns[i].Name;
                int width = dataGridView1.Columns[i].Width;
                int index = dataGridView1.Columns[i].DisplayIndex;
                w.Write(i);
                w.Write(index);
                w.Write(width);
                w.Write(name);
            }
            w.Close();
        }
        //------------------------------------------------------------------------------------
        public static void read_column_layout(string UserID, DataGridView dataGridView1, string filename)
        {
            int i;
            try
            {
                FileStream fs = new FileStream(filename + UserID + ".col", FileMode.Open);
                BinaryReader w = new BinaryReader(fs);      // Create the writer for data.

                bool stoppen = false;
                int lastposition = -1, position = 0;
                int index = 0;
                int width = 0;
                string name = "";
           
                try
                {
                    position = w.ReadInt32();
                    index = w.ReadInt32();
                    width = w.ReadInt32();
                    name = w.ReadString();
                }
                catch (Exception e)
                {
                    w.Close();
                    return;
                }


                while (!stoppen)
                {
                    stoppen = (lastposition > position);
                    lastposition = position;
                    if (!stoppen)
                    {
                        try
                        {
                            if (index >= 0 && index < dataGridView1.Columns.Count)
                            {
                                dataGridView1.Columns[name].DisplayIndex = index;
                            }
                            if (width >= 0 && width <= 800)
                            {
                                dataGridView1.Columns[name].Width = width;
                            }
                            position = w.ReadInt32();
                            index = w.ReadInt32();
                            width = w.ReadInt32();
                            name = w.ReadString();

                        }
                        catch (Exception e)// te ver gelezen
                        {
                            w.Close();
                            return;
                        }
                    }
                }
                w.Close();
            }
            catch
            {
                return; // profiel niet gevonden
            }
        }
        //----------------------------------------------------------------------------------------------------------
        public void check_databaseopen(int action)
        {
            if ((action == 999) && (database_open > 0))
            {
                SQLconn.Close();
                SQLconn2.Close();
                SQLconn3.Close();
                SQLconnX.Close();
                SQLconnX2.Close();
                database_open = 0;
            }

            if (database_open == 0)
            {
                //    server=TMS-BCK;uid=sa;pwd=pltas;database=tmw_ploeger2000
                connectionstring = "server=" + formsetup._setup[0].str +
                                    ";uid=" + formsetup._setup[2].str +
                                    ";pwd=" + formsetup._setup[3].str +
                                    ";database=" + formsetup._setup[1].str +
                                    ";Connection Timeout=12";
                try
                {
                    SQLconn = new SqlConnection(connectionstring); SQLconn.Open();
                    SQLconn2 = new SqlConnection(connectionstring); SQLconn2.Open();
                    SQLconn3 = new SqlConnection(connectionstring); SQLconn3.Open();
                    database_open = 1;
                }
                catch (SqlException sq)
                {
                    MessageBox.Show("Kan geen connectie maken met de local database!\r\n\r\n" /*+ connectionstring*/+ " fout connecting CM database " + sq.Message);
                    // Application.Exit();
                }
                //-----------------------------------------------
                connectionstring = "server=" + formsetup._setup[50].str +
                                    ";uid=" + formsetup._setup[52].str +
                                    ";pwd=" + formsetup._setup[53].str +
                                    ";database=" + formsetup._setup[51].str +
                                    ";Connection Timeout=12";
                try
                {
                    SQLconnX = new SqlConnection(connectionstring); SQLconnX.Open();
                    SQLconnX2 = new SqlConnection(connectionstring); SQLconnX2.Open();
                    SQLconnX3 = new SqlConnection(connectionstring); SQLconnX3.Open();
                }
                catch (SqlException sq)
                {
                    MessageBox.Show("Kan geen connectie maken met de intermediale database!\r\n\r\n" /*+ connectionstring*/+ " fout connecting CM database " + sq.Message);
                    // Application.Exit();
                }
            }
        }



        //----------------------------------------------------------------------------------------------------------
        public XmlNodeList getXMLitem(string XML, string item)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(XML);
            MemoryStream stream = new MemoryStream(byteArray);
            XmlDocument doc = new XmlDocument();
            doc.Load(stream);

            XmlNodeList elemList = doc.GetElementsByTagName(item);
            return (elemList);
        }
        //      [StructLayout(LayoutKind.Sequential)]
        // ---------- get mouse position
        public struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }

        /// <summary>
        /// Retrieves the cursor's position, in screen coordinates.
        /// </summary>
        /// <see>See MSDN documentation for further information.</see>
        [System.Runtime.InteropServices.DllImport("user32.dll")]

        public static extern bool GetCursorPos(out POINT lpPoint);

        public static Point GetCursorPosition()
        {
            POINT lpPoint;
            GetCursorPos(out lpPoint);
            //bool success = User32.GetCursorPos(out lpPoint);
            // if (!success)

            return lpPoint;
        }
        //----------------------------------------------------------------------------------------------------------
        public void backupfile(string ipfn, string bufn)
        {
            string str;
            if (!File.Exists(ipfn)) return;
            StreamReader sread;
            try
            {
                sread = new StreamReader(ipfn, Encoding.GetEncoding("iso-8859-15"));
            }
            catch (Exception e)
            {
                writelog("Backupfile error: " + e.Message);
                return;
            }
            StreamWriter swrit = new StreamWriter(bufn + DateTime.Now.ToString("yyMM") + ".txt", true);
            swrit.WriteLine("-------------------------------------------------------");
            swrit.WriteLine(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + " (" + ipfn + ") by " + UserID);
            swrit.WriteLine("-------------------------------------------------------");

            while ((str = sread.ReadLine()) != null)
            {
                swrit.WriteLine(str);
            }
            sread.Close();
            swrit.Close();
        }

        //----------------------------------------------------------------------------------------------------------
        public int checkdubbel(string searchstring, string bufn)
        {
            int dubbelcnt = 0;
            string ipfn = bufn + DateTime.Now.ToString("yyMM") + ".txt";

            string str = "";
            if (!File.Exists(ipfn)) return (dubbelcnt);

            StreamReader sread = new StreamReader(ipfn, Encoding.GetEncoding("iso-8859-15"));
            try
            {

                while ((str = sread.ReadLine()) != null)
                {
                    if (str.IndexOf(searchstring) >= 0)
                    {
                        dubbelcnt++;
                        //                        sread.Close();
                        //                      return (true);
                    }
                }
                sread.Close();
            }
            catch (Exception ex)
            {
                writelog("error in checkdbubbel: " + ex.Message);
            }
            sread.Close();
            return (dubbelcnt);
        }

        public static int atol(double fl)
        {
            try
            {
                return (Convert.ToInt32(fl));
            }
            catch
            {
                return (0);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            sftp1.SSHUser = formsetup._setup[11].str;  //"9SWO000091@dt9SWO.kcmkt.com";
            if (true)
            {
                sftp1.SSHAuthMode = nsoftware.IPWorksSSH.SftpSSHAuthModes.amPassword;
                sftp1.SSHPassword = formsetup._setup[12].str;// "?07TODAY";
            }
            else
            {
                sftp1.SSHAuthMode = nsoftware.IPWorksSSH.SftpSSHAuthModes.amPublicKey;
                /*if (frmLogin.cbAuthType.SelectedIndex == 1)
                  sftp1.SSHCert = new Certificate(CertStoreTypes.cstPEMKeyFile, frmLogin.tbFilePath.Text, frmLogin.tbPassword.Text, "*");
                else
                  sftp1.SSHCert = new Certificate(CertStoreTypes.cstPFXFile, frmLogin.tbFilePath.Text, frmLogin.tbPassword.Text, "*");
        */
            }
            //      cmdConnect.Text = "Disconnect";
            try
            {
                sftp1.SSHLogon(formsetup._setup[10].str, 22); //                sftp1.SSHLogon("dt.kcmkt.com", 22);
                sftp1.RemotePath = formsetup._setup[20].str;
                FTPnames.Clear();
                sftp1.ListDirectory();

                applicationlog("FTP", "login", "");

                FTPnames.Sort();

                for (int i = 0; i < FTPnames.Count; i++)
                {
                    applicationlog("FTP", "download " + FTPnames[i], FTPnames[i]);

                    log.AppendText((i + 1).ToString("###") + " download " + FTPnames[i]);
                    sftp1.RemoteFile = FTPnames[i];
                    sftp1.LocalFile = formsetup._setup[22].str + FTPnames[i];
                    sftp1.Download();
                    log.AppendText("\r\n");
                    if (formsetup._setup[10].integer > 0)
                    {
                        sftp1.LocalFile = formsetup._setup[24].str + FTPnames[i];
                        sftp1.Download();
                    }
                    if (formsetup._setup[11].integer > 0)
                    {
                        sftp1.DeleteFile(FTPnames[i]);
                    }
                }
                log.AppendText("Ready, log off\r\n");
                sftp1.SSHLogoff();

            }
            catch (Exception ex)
            {
                applicationlog("FTP", "error in FTP-download: " + ex.Message, "");
                log.AppendText("Error in FTP-download: " + ex.Message + "\r\n");
            }
            if (formsetup._setup[12].integer > 0)
            {
                try
                {
                    string[] filenames = Directory.GetFiles(formsetup._setup[23].str);
                    if (filenames.Length > 0)
                    {
                        sftp1.SSHLogon(formsetup._setup[10].str, 22); //                sftp1.SSHLogon("dt.kcmkt.com", 22);
                        sftp1.RemotePath = formsetup._setup[21].str;
                        FTPnames.Clear();

                        for (int i = 0; i < filenames.Length; i++)
                        {
                            applicationlog("FTP", "upload " + filenames[i], filenames[i]);

                            if ((filenames[i].Length > 0) && (filenames[i].Substring(0, 1) != "."))
                            {
                                backupfile(filenames[i], "FTP_upload");
                                log.AppendText((i + 1).ToString("###") + " upload " + filenames[i]);
                                sftp1.RemoteFile = Path.GetFileName(filenames[i]);
                                sftp1.LocalFile = filenames[i];
                                sftp1.Upload();
                                log.AppendText("\r\n");
                                File.Delete(filenames[i]);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    applicationlog("FTP", "error in FTP-upload: " + ex.Message, "");
                    log.AppendText("Error in FTP-upload: " + ex.Message + "\r\n");
                }
            }
        }

        private void sftp1_OnSSHServerAuthentication(object sender, nsoftware.IPWorksSSH.SftpSSHServerAuthenticationEventArgs e)
        {
            e.Accept = true;
        }

        //-------------------------------------------------------------------------------------------------------------------------------------
        public void Upload(string uri, string filePath)
        {
            string formdataTemplate = "Content-Disposition: form-data; filename=\"{0}\";\r\nContent-Type: multipart/form-data\r\n\r\n";
            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundarybytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.ServicePoint.Expect100Continue = false;
            request.Method = "POST";
            request.ContentType = "multipart/form-data; boundary=" + boundary;
            request.Credentials = new System.Net.NetworkCredential("vanderWal", "9Y6SaBC5");

            using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(boundarybytes, 0, boundarybytes.Length);
                    string formitem = string.Format(formdataTemplate, Path.GetFileName(filePath));
                    byte[] formbytes = Encoding.UTF8.GetBytes(formitem);
                    requestStream.Write(formbytes, 0, formbytes.Length);
                    byte[] buffer = new byte[1024 * 4];
                    int bytesLeft = 0;

                    while ((bytesLeft = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        requestStream.Write(buffer, 0, bytesLeft);
                    }

                }
            }

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse()) { }

                Console.WriteLine("Success");
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        //-------------------------------------------------------------
        private static void DownloadCurrent()
        {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create("http://integration.demo.wktransportservices.com/vanDerWal/{04B16FE8-0AC2293A-0000016638AE1275-A37EFCBB41E1AD8D}");
            webRequest.Method = "GET";
            webRequest.Timeout = 3000;
            webRequest.BeginGetResponse(new AsyncCallback(PlayResponeAsync), webRequest);
        }

        private static void PlayResponeAsync(IAsyncResult asyncResult)
        {
            long total = 0;
            long received = 0;
            HttpWebRequest webRequest = (HttpWebRequest)asyncResult.AsyncState;
            webRequest.Credentials = new System.Net.NetworkCredential("vanderWal", "9Y6SaBC5");

            try
            {
                using (HttpWebResponse webResponse = (HttpWebResponse)webRequest.EndGetResponse(asyncResult))
                {
                    byte[] buffer = new byte[1024];

                    FileStream fileStream = File.OpenWrite("d:\\download.txt");
                    using (Stream input = webResponse.GetResponseStream())
                    {
                        total = input.Length;

                        int size = input.Read(buffer, 0, buffer.Length);
                        while (size > 0)
                        {
                            fileStream.Write(buffer, 0, size);
                            received += size;

                            size = input.Read(buffer, 0, buffer.Length);
                        }
                    }

                    fileStream.Flush();
                    fileStream.Close();
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
        //    DownloadCurrent();
            WebClient client = new WebClient();
            client.BaseAddress = "http://integration.demo.wktransportservices.com/vanDerWal";
            client.Credentials = new System.Net.NetworkCredential("vanderWal", "9Y6SaBC5");

            client.UploadFile("http://integration.demo.wktransportservices.com/vanDerWal/outgoing/", "d:\\transics.txt");

            client.DownloadFile("http://integration.demo.wktransportservices.com/vanDerWal/{04B16FE8-0AC2293A-0000016638AE1275-A37EFCBB41E1AD8D}", "d:\\download.txt");
           

            Upload("http://integration.demo.wktransportservices.com/vanDerWal", "d:\\transics.txt");

            var baseAddress = "http://integration.demo.wktransportservices.com/vanDerWal";//  formsetup._setup[14].str + "//" + tf_ref.ToString();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls;


            var http = (HttpWebRequest)WebRequest.Create(new Uri(baseAddress));
            //var http = HttpWebRequest.Create(new Uri(baseAddress));
            http.Credentials = new System.Net.NetworkCredential("vanderWal", "9Y6SaBC5");
        

            http.Accept = "application/json";
            http.ContentType = "application/json";
            http.Method = "POST";
            http.Headers.Add("Content-Type", "multipart/form-data");
            http.Headers.Add("Content-Disposition", "attachment; filename='test.txt'");
                        
            
            //----------------------------------------------------- list
            var http_ = (HttpWebRequest)WebRequest.Create(new Uri(baseAddress));
            http.Credentials = new System.Net.NetworkCredential("vanderWal", "9Y6SaBC5");


            http.Accept = "application/json";
            http.ContentType = "application/json";
            http.Method = "GET";
            http.Headers.Add("filters", "/vanDerWal/incoming");
            //string auth_token = "Bearer " + acces_token;
            //http.Headers.Add("Authorization", auth_token);

            //http.Headers.Add("filters", "type.eq.WAYBILL,status.ne.DRAFT");

            //string parsedContent = ipstr; // File.ReadAllText("test.txt", System.Text.Encoding.UTF8);
            //ASCIIEncoding encoding = new ASCIIEncoding();
            //Byte[] bytes = encoding.GetBytes(parsedContent);


            var response = http.GetResponse();


            var stream = response.GetResponseStream();
            var sr = new StreamReader(stream);
            string dstr = sr.ReadToEnd(); // dstr is de retour JSON

            writelog(dstr);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            provider.NumberDecimalSeparator = ".";
            provider.NumberGroupSeparator = ",";

            provider.NumberDecimalSeparator = ".";
            provider.NumberGroupSeparator = ",";

            System.Globalization.CultureInfo cinfo = new CultureInfo("de-DE");
            cinfo.NumberFormat.NumberDecimalSeparator = ".";
            cinfo.NumberFormat.NumberGroupSeparator = ",";
            System.Threading.Thread.CurrentThread.CurrentCulture = cinfo;

            get_system_login();
            formsetup.readsetup_();
            textBox1.Text = formsetup._setup[10].str;
            textBox2.Text = formsetup._setup[11].str;
            textBox7.Text = formsetup._setup[12].str;
            textBox3.Text = formsetup._setup[20].str;
            textBox4.Text = formsetup._setup[21].str;
            textBox6.Text = formsetup._setup[22].str;
            textBox5.Text = formsetup._setup[23].str;   // passwd
            textBox17.Text = formsetup._setup[24].str;  // backup dir FTP 

            textBox8.Text = formsetup._setup[0].str;   // database
            textBox9.Text = formsetup._setup[1].str;
            textBox10.Text = formsetup._setup[2].str;
            textBox11.Text = formsetup._setup[3].str;
//          
            textBox18.Text = formsetup._setup[50].str;
            textBox19.Text = formsetup._setup[51].str;
            textBox20.Text = formsetup._setup[52].str;
            textBox21.Text = formsetup._setup[53].str;
            
            formsetup.writesetup_();
            textBox12.Text = formsetup._setup[30].str; // Calloff template
            textBox13.Text = formsetup._setup[30].integer.ToString(); //sequence
            textBox22.Text = formsetup._setup[31].str; // TW status map


            checkBox2.Checked = (formsetup._setup[10].integer > 0);
            checkBox3.Checked = (formsetup._setup[11].integer > 0);
            checkBox4.Checked = (formsetup._setup[12].integer > 0);
            checkBox5.Checked = (formsetup._setup[13].integer > 0); // TW active
            checkBox6.Checked = (formsetup._setup[15].integer>0); // dockman webservice active

            textBox23.Text = DateTime.FromOADate(formsetup._setup[10].dbl).ToString("yyyy-MM-dd HH:mm:ss");

            textBox24.Text = formsetup._setup[32].str; // output smartway dockman files
            textBox25.Text = formsetup._setup[33].str;  //calloff filter. Format: 'xxxx','xxx'

            timer1.Enabled = true;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[10].str = textBox1.Text;
            formsetup.writesetup_();
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[11].str = textBox2.Text;
            formsetup.writesetup_();
        }

        private void textBox7_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[12].str = textBox7.Text;
            formsetup.writesetup_();
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[20].str = textBox3.Text;
            formsetup.writesetup_();
        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[21].str = textBox4.Text;
            formsetup.writesetup_();
        }

        private void textBox6_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[22].str = textBox6.Text;
            formsetup.writesetup_();
        }

        private void textBox5_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[23].str = textBox5.Text;
            formsetup.writesetup_();
        }

        private void sftp1_OnDirList(object sender, nsoftware.IPWorksSSH.SftpDirListEventArgs e)
        {
            if ((e.FileName.Length > 0) && (e.FileName.Substring(0, 1) != "."))
            {
                if (!e.IsDir)
                {
                    FTPnames.Add(e.FileName);
                }
            }
        }

        private void label12_Click(object sender, EventArgs e)
        {

        }

        private void textBox8_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[0].str = textBox8.Text;
            formsetup.writesetup_();
        }

        private void textBox9_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[1].str = textBox9.Text;
            formsetup.writesetup_();
        }

        private void textBox10_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[2].str = textBox10.Text;
            formsetup.writesetup_();
        }

        private void textBox11_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[3].str = textBox11.Text;
            formsetup.writesetup_();
        }

        private void tabPage4_Click(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            TW_create(textBox15.Text);
        }
        private void TW_create(string loadnr) {
            check_databaseopen(1);
            try
            {
                SQLcmd = new SqlCommand(
                    "select top 2 " +
                    "(select top 1 addrname  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addrname, " +
                    "(select top 1 addr1  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addr1, " +
                    "(select top 1 addr2  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addr2, " +
                    "(select top 1 cityname from Loc where loc.LocId=ordheader.LocIdOrig) as f_cityname, " +
                    "(select top 1 postalcode from Loc where loc.LocId=ordheader.LocIdOrig) as f_postalcode, " +
                    "(select top 1 CountryISO2 from Loc inner join Country on country.CountryId=loc.countryid where loc.LocId=ordheader.LocIdOrig) as f_country, " +
                    "(select top 1 addrname  from Loc where loc.LocId=ordheader.LocIddest ) as t_addrname, " +
                    "(select top 1 addr1  from Loc where loc.LocId=ordheader.LocIdDest) as t_addr1, " +
                    "(select top 1 addr2  from Loc where loc.LocId=ordheader.LocIdDest) as t_addr2, " +
                    "(select top 1 cityname from Loc where loc.LocId=ordheader.LocIdDest) as t_cityname, " +
                    "(select top 1 postalcode from Loc where loc.LocId=ordheader.LocIdDest) as t_postalcode, " +
                    "(select top 1 CountryISO2 from Loc inner join Country on country.CountryId=loc.countryid where loc.LocId=ordheader.LocIdDest) as t_country, " +
                    "(select top 1 commentvalue from OrdComment where ordcomment.ordheaderid=ordheader.OrdHeaderId and qualifierid=25) as locationid, " +
                    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=8) as ref99, " +
                    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=9) as ref45, " +
                    "(select top 1 interstateCcId from TradingPartnerCarrier where Tradingpartnercarrier.tradingpartnerid=l.TradingPartnerIdCarrier) as car_ref, " +
                    "(select top 1 tradingpartnername from TradingPartnerCarrier left join TradingPartner on TradingPartnerCarrier.TradingPartnerId=TradingPartner.TradingPartnerId where Tradingpartnercarrier.tradingpartnerid=l.TradingPartnerIdCarrier) as car_name,"+
                    "* from load as l " +
                    "left join ShipmentLoad  on shipmentload.loadid = l.LoadId " +
                        //                    "right join ShipmentOrdLeg on shipmentordleg.ShipmentId=shipmentload.ShipmentId " +
                    "outer apply (select top 1 * from ShipmentOrdLeg where shipmentordleg.ShipmentId=shipmentload.ShipmentId ) shipmentOrdLeg " +
                    "left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid " +
                    "left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId " +
                    "left join OrdLine on ordheader.ordheaderid = ordline.OrdHeaderId " +
                    "left join Commodity on commodity.CommodityId=ordline.CommodityId " +
                    "where l.LoadNum=@loadnum " +
                    "order by ordheader.OrdHeaderId desc ", SQLconn);
                SQLcmd.Parameters.AddWithValue("loadnum", loadnr);
                SQL_x = SQLcmd.ExecuteReader();
                while (SQL_x.Read())
                {
                    linecnt = 0;
                    if (TW_calloff(SQL_x["ref45"].ToString(),1) == 0)
                    {
                        SQL_x.Close();
                        return;
                    }
                    update_order_status(SQL_x["ref45"].ToString(), 1,loadnr);
                }
                SQL_x.Close();
                TW_send();
            }
            catch (Exception ex)
            {
                MessageBox.Show("error in TW: " + ex.Message);
            }

        }
        //----------------------------------------------------------------------------------------------------------
        public void TW_send()
        {
            string fn1="";
            try
            {
                string[] fn = Directory.GetFiles(TWDIR, "*.*");

                for (int i = 0; i < fn.Length; i++)
                {
                    fn1 = fn[i];
                    applicationlog("FTP", "TW-send: " + fn[i], fn[i]);
                    log.AppendText("TW send " + fn[i] + "\r\n");
                    WebClient client = new WebClient();
                    client.BaseAddress = "http://integration.demo.wktransportservices.com/vanDerWal";
                    client.Credentials = new System.Net.NetworkCredential("vanderWal", "9Y6SaBC5");

                    client.UploadFile("http://integration.demo.wktransportservices.com/vanDerWal/outgoing/", fn[i]);
                    File.Delete(fn[i]);
                }
            }
            catch (Exception ex)
            {
                writelog("error in TW_send: " + ex.Message);
                log.AppendText("error in TW_send: " + ex.Message);
                applicationlog("FTP","Error in TW: "+fn1+", "+ex.Message,fn1);
            }
        }

        //----------------------------------------------------------------------------------------------------------
        public string get_field(ref string ipstr, ref int pos, ref int len)
        {
            int i = ipstr.IndexOf("[");
            int j = ipstr.IndexOf("]");
            if ((i >= 0) && (j > i))
            {
                string resultstring = ipstr.Substring(i + 1, j - i - 1);
                ipstr = ipstr.Remove(i, j - i + 1);
                pos = i;
                len = j - i - 1;
                return (resultstring);
            }
            return ("");
        }
        //-----------------------------------------------------------------------------------------------------------------
        string process_field(string ipstr)
        {
            try
            {
                if (ipstr.Length == 0) return ("");
                //---------------------------------------
                if (ipstr.Substring(0, 1) == "F")
                {
                    string[] split = ipstr.Split(',');
                    if (split.Length <= 1)
                    {
                        return ("not enough parameters in query: " + ipstr);
                    }
                    string dstr2 = SQL_x[split[1].Trim()].ToString();
                    return (dstr2);
                }
                if (ipstr.IndexOf("$value") == 0)
                {
                    if ((blokcnt < blokvalues.Length) && (blokcnt >= 0))
                    {
                        return (blokvalues[blokcnt]);
                    }
                    else
                    {
                        return("error in blokvalue $value. Index:" + blokcnt.ToString() + "/" + blokvalues.Length.ToString());
                    }
                }
                if (ipstr.IndexOf("$IF")==0)
                {
                    string[] split = ipstr.Split(',');  //$if,dbveld,compare,true,false
                    if (split.Length >= 5)
                    {
                        string dstr2 = SQL_x[split[1].Trim()].ToString();
                        if (dstr2.IndexOf(split[2])>=0)
                        { //gevonden
                            return (process_field(split[3].Trim().Replace(".", ",")));
                        }
                        else
                        { // niet gevonden
                            return (process_field(split[4].Trim().Replace(".", ",")));
                        }
                    }
                }
                if (ipstr.Substring(0, 1) == "W")
                {
                    string[] split = ipstr.Split(',');
                    if (split.Length <= 1)
                    {
                        return ("not enough parameters in query: " + ipstr);
                    }
                    string dstr2 = SQL_x[split[1].Trim()].ToString();
                    return (Convert.ToInt32(atof(dstr2)).ToString());
                }
                if (ipstr.IndexOf("$split") >= 0)
                {
                    int kolom = 1;
                    string[] split = ipstr.Split(',');
                    if (split.Length <= 3)
                    {
                        return ("not enough parameters in $split: " + ipstr);
                    }
                    string dstr2 = SQL_x[split[1].Trim()].ToString();
                    if (split[2].Length == 0)
                    {
                        return ("splitter not defined in $split");
                    }
                    string[] split2 = dstr2.Split(Convert.ToChar(split[2].Trim().Substring(0, 1)));
                    if (split.Length > 3) 
                        kolom = atol(split[3]);
                    if (kolom - 1 < split2.Length) return (split2[kolom - 1]);
                    else return ("");
                }
                if (ipstr.ToLower().IndexOf("$lines") == 0)
                {
                    return ((linecnt-1).ToString());
                }
                if (ipstr.ToLower().IndexOf("$ref1") == 0)
                {
                    string[] split = ipstr.Split(',');
                    if (split.Length <= 1)
                    {
                        return ("not enough parameters in query: " + ipstr);
                    }
                    string dstr2 = SQL_x[split[1].Trim()].ToString();
                    string[] split3 = dstr2.Split('/');
                    return (split3[0]);
                }
                if (ipstr.ToLower().IndexOf("$ref2") == 0)
                {
                    string[] split = ipstr.Split(',');
                    if (split.Length <= 1)
                    {
                        return ("not enough parameters in query: " + ipstr);
                    }
                    string dstr2 = SQL_x[split[1].Trim()].ToString();
                    string[] split3 = dstr2.Split('/');
                    if (split3.Length > 1) 
                        return (split3[1]);
                    return (split3[0]);
                }
                if (ipstr.ToLower().IndexOf("$sequence") == 0)
                {
                    return (formsetup._setup[30].integer.ToString()); //sequence
                }
                if (ipstr.ToLower().IndexOf("$cnt") == 0)
                {
                    XMLcnt++;
                    return (XMLcnt.ToString()); //teller in XML
                }
                if (ipstr.ToLower().IndexOf("$datenow") == 0)
                {
                    return (DateTime.Now.ToString("yyyyMMdd"));
                }
                if (ipstr.ToLower().IndexOf("$timenow3") == 0)
                {
                    return (DateTime.Now.ToString("HHmmss"));
                }
                if (ipstr.ToLower().IndexOf("$timenow") == 0)
                {
                    return (DateTime.Now.ToString("HHmm"));
                }
                if (ipstr.ToLower().IndexOf("$date2") == 0)
                {
                    string[] split = ipstr.Split(',');
                    if (split.Length <= 1)
                    {
                        return ("not enough parameters in $date: " + ipstr);
                    }
                    string dstr2 = SQL_x[split[1]].ToString();
                    DateTime dt = todt(dstr2);
                    return (dt.ToString("yyMMdd"));
                }
                if (ipstr.ToLower().IndexOf("$date") == 0)
                {
                    string[] split = ipstr.Split(',');
                    if (split.Length <= 1)
                    {
                        return ("not enough parameters in $date: " + ipstr);
                    }
                    string dstr2 = SQL_x[split[1]].ToString();
                    DateTime dt = todt(dstr2);
                    return (dt.ToString("yyyyMMdd"));
                }
                if (ipstr.ToLower().IndexOf("$time3") == 0)
                {
                    string[] split = ipstr.Split(',');
                    if (split.Length <= 1)
                    {
                        return ("not enough parameters in $time: " + ipstr);
                    }
                    string dstr2 = SQL_x[split[1]].ToString();
                    DateTime dt = todt(dstr2);
                    return (dt.ToString("HHmmss"));
                }
                if (ipstr.ToLower().IndexOf("$time") == 0)
                {
                    string[] split = ipstr.Split(',');
                    if (split.Length <= 1)
                    {
                        return ("not enough parameters in $time: " + ipstr);
                    }
                    string dstr2 = SQL_x[split[1]].ToString();
                    DateTime dt = todt(dstr2);
                    return (dt.ToString("HHmm"));
                }
            }
            catch (Exception ex)
            {
                log.AppendText(">>>> error in process_field: " + ex.Message+"\r\n");
            }
            return (ipstr );
        }
        //-----------------------------------------------------------------------------------------------------------------
        public string process_line(string datafield)
        {
            int len = 0;
            int pos = 0;
            string resstr = "";
            //string datafield = split[3];
            while ((resstr = get_field(ref datafield, ref pos, ref len)).Length > 0)
            {
                string result = process_field(resstr);
                datafield = datafield.Insert(pos, result);
            }
            return (datafield);
        }

        List<string> list_formatfile = new List<string>();

        public int TW_calloff(string loadnr, int action) // action=-1: cancel. Anders add
        {
            linecnt = 0;
            string dstr = "";
            try
            {
                /* uitzetten, geen TW meer in nov 2019---------------------------
                Directory.CreateDirectory(TWDIR);
                StreamReader sr = new StreamReader(formsetup._setup[30].str);
                StreamWriter sw = new StreamWriter(TWDIR + loadnr, false, Encoding.GetEncoding("iso-8859-15"));
                bool printed = false;
                if (action == -1) sr = new StreamReader("TWCalloffcancel.txt");

                list_formatfile.Clear();

                while ((dstr = sr.ReadLine()) != null)
                {
                    dstr = dstr.Trim();
                    list_formatfile.Add(dstr);
                }
                sr.Close();

                int listcnt = 0;
                while (listcnt < list_formatfile.Count)
                {
                    dstr = list_formatfile[listcnt];
                    if ((dstr.Length > 0) && (dstr.Substring(0, 1) != "!"))
                    {
                        string[] split = dstr.Split(';');
                        for (int i = 0; i < split.Length; i++) split[i] = split[i].Trim();

                        if ((dstr.Length > 0) && ("{}".IndexOf(dstr.Substring(0, 1)) >= 0))
                        {       // wel <>
                            if (dstr.Substring(0, 1) == "{")
                            {
                                if (split.Length >= 3)
                                {
                                    string scheiding = split[2];
                                    if (scheiding.Length > 1) scheiding = scheiding.Substring(0, 1);
                                    if (scheiding.Length == 0) scheiding = " ";
                                    blokcnt = 0;
                                    string dstr2 = process_line(split[1]).Trim();
                                    while (dstr2.IndexOf("  ") >= 0)
                                        dstr2 = dstr2.Replace("  ", " ");

                                    blokvalues = dstr2.Split(Convert.ToChar(scheiding));
                                    memblokline = listcnt;
                                }
                            }
                            else
                            { // "}"
                                if (++blokcnt < blokvalues.Length)
                                {  // meer values beschikbaar
                                    listcnt = memblokline;
                                }
                                // else doe niets: hij loopt verder
                            }
                        }
                        else
                        {  // geen {}
                            if (split.Length >= 2)
                            {
                                dstr = process_line(split[1]);
                                if (dstr.IndexOf("~") >= 0)
                                {
                                    sw.WriteLine("~");
                                    printed = false;
                                    linecnt++;
                                }
                                else
                                {
                                    if (printed)
                                    {
                                        sw.Write("*");
                                    }
                                    sw.Write(RemoveDiacritics(filter(dstr)));
                                    printed = true;
                                }
                            }
                        }
                    }
                    listcnt++; // volgende regel
                }
                sw.Close();
                textBox16.Text = (++formsetup._setup[30].integer).ToString(); //sequence

                backupfile(TWDIR + loadnr, "TW_Calloff");
                
                //WebClient client = new WebClient();
                //client.BaseAddress = "https://integration.demo.wktransportservices.com/vanDerWal";
                //client.BaseAddress = "https://comm2.transwide.com/vanderWal/incoming";
                //client.Credentials = new System.Net.NetworkCredential("vanderWal", "vxeYiez3ke2t");

                //writelog("TW start");
                //string TWstr = File.ReadAllText(TWDIR+fn);
                //var result = client.UploadString("https://comm2.transwide.com/vanderWal/incoming","POST",TWstr);
                //writelog(result.ToString());

                

                //---------------------------------------------------------------------------------------------------------------------------------------
                // REFERENTIE: https://docs.microsoft.com/en-us/dotnet/framework/network-programming/how-to-send-data-using-the-webrequest-class

                var baseAddress = "https://comm2.transwide.com/vanderWal/incoming";//  formsetup._setup[14].str + "//" + tf_ref.ToString();
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls;

                var http = (HttpWebRequest)WebRequest.Create(new Uri(baseAddress));
                http.Credentials = new System.Net.NetworkCredential("vanderWal", "vxeYiez3ke2t");

                //http.Accept = "application/json";
                //http.ContentType = "application/json";
                http.Method = "POST";
                //http.Headers.Add("Content-Type", "multipart/form-data");
                //http.Headers.Add("Content-Disposition", "attachment; filename='test.txt'");

                string postData = File.ReadAllText(TWDIR + loadnr);
                byte[] byteArray = Encoding.UTF8.GetBytes(postData);
                // Set the ContentType property of the WebRequest.  
                http.ContentType = "application/x-www-form-urlencoded";
                // Set the ContentLength property of the WebRequest.  
                http.ContentLength = byteArray.Length;
                // Get the request stream.  
                Stream dataStream = http.GetRequestStream();
                // Write the data to the request stream.  
                dataStream.Write(byteArray, 0, byteArray.Length);
                // Close the Stream object.  
                dataStream.Close();
                // Get the response.  
                log.AppendText("now send TW-calloff " + loadnr); wait(10);
                HttpWebResponse response = (HttpWebResponse)http.GetResponse();
                log.AppendText(response.StatusCode + "/" + response.StatusDescription + "\r\n"); wait(10);
                response.Close();

                //writelog(response.status);
                */
            }
            catch (Exception ex)
            {
                log.AppendText("Error in TW_calloff: " + ex.Message + "\r\n");
                writelog("Error in TW_calloff: " + ex.Message);
                applicationlog("FTP", "error in TW_calloff: " + ex.Message, "");
                return (0);
            }

            if (formsetup._setup[15].integer > 0)    // Smartway calloff
            {
                try
                {
                    // put calloffs
                    webservice.DMWSSoapClient dmws = new webservice.DMWSSoapClient();
                    webservice.Response response = new webservice.Response();
                    webservice.CallOff calloff = new webservice.CallOff();
                    webservice.LoginData login = new webservice.LoginData();
                    webservice.carrierStruct carrier = new webservice.carrierStruct();
                    webservice.ArrayOfLocationStruct locations = new webservice.ArrayOfLocationStruct();
                    webservice.ArrayOfReferenceStruct references = new webservice.ArrayOfReferenceStruct();
                    login.LoginName = "smartway"; login.Password = "utrecht";

                    calloff.action = "new";
                    if (action < 0) calloff.action = "cancel";

                    calloff.ShipperRef = process_line("[F,ref99]");
                    calloff.OrderRef = process_line("[F,ref45_2]") + " / " + process_line("[F,LoadNum]");

                    carrier.carrierRef = process_line("[$split,car_ref,/,1]");
                    carrier.carrierCode = process_line("[F,TradingPartnerNum]");
                    carrier.carrierName = process_line("[F,car_name]");

                    webservice.locationStruct location = new webservice.locationStruct();
                    location.locationID = process_line("[$ref1,locationid]");
                    if (location.locationID.Trim().Length == 0) location.locationID = process_line("[F,f_locnum]").Trim();
                    location.locationDateTime1 = todt(process_line("[F,DateTime_earlypickup]"));
                    location.locationDateTime2 = todt(process_line("[F,DateTime_latepickup]"));
                    location.locationName = process_line("[F,f_addrname]");
                    location.locationAddress = process_line("[F,f_addr1]");
                    location.locationCity = process_line("[F,f_cityname]");
                    location.locationPostalcode = process_line("[F,f_postalcode]");
                    location.locationCountry = process_line("[F,f_country]");
                    locations.Add(location);

                    location = new webservice.locationStruct();
                    location.locationID = process_line("[$ref2,locationid]");
                    if (location.locationID.Trim().Length == 0) location.locationID = process_line("[F,t_locnum]").Trim();
                    location.locationDateTime1 = todt(process_line("[F,Datetime_earlydelivery]"));
                    location.locationDateTime2 = todt(process_line("[F,DateTime_latedelivery]"));
                    location.locationName = process_line("[F,t_addrname]");
                    location.locationAddress = process_line("[F,t_addr1]");
                    location.locationCity = process_line("[F,t_cityname]");
                    location.locationPostalcode = process_line("[F,t_postalcode]");
                    location.locationCountry = process_line("[F,t_country]");
                    locations.Add(location);

                    calloff.Pallets = atof(process_line("[F,Handlingunitcounttot]"));
                    calloff.Volume = atof(process_line("[F,PieceCountTot]"));
                    calloff.Gweight = atof(process_line("[W,Wtbase_grossTot]"));
                    calloff.Nweight = atof(process_line("[W,Volbase_Grosstot]"));
                    calloff.goods_description = process_line("[F,Name]");

                    calloff.reference = references;
                    calloff.login = login;
                    calloff.carrier = carrier;
                    calloff.locations = locations;
                    response = dmws.Reservation(calloff);
                    if (response.response.IndexOf("OK") == 0)
                    {
                        writelog("succesfull DM calloff for " + calloff.ShipperRef + " / " + response.response);
                        log.AppendText("Succesfull DM response:" + calloff.ShipperRef + " / " + response.response+"\r\n");
                        applicationlog("DM webservice", "DM respone OK" + calloff.ShipperRef + " / " + response.response, "");
                    }
                    else
                    {
                        writelog("Error DM calloff for " + calloff.ShipperRef + " / " + response.response);
                        log.AppendText("Error in DM response:" + calloff.ShipperRef + " / " + response.response + "\r\n");
                        applicationlog("DM webservice", "DM respone error:" + calloff.ShipperRef + " / " + response.response, "");
                    }
                    /*
                    //--- retrieval
                    webservice.ArrayOfBooking bookings = new webservice.ArrayOfBooking();

                    bookings = dmws.retrieve_realisations(DateTime.FromOADate(formsetup._setup[10].dbl), login);
                    StreamWriter sw = new StreamWriter(formsetup._setup[32].str + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    sw.WriteLine("==0.30==" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    for (int i = 0; i < bookings.Count; i++)
                    {
                        sw.WriteLine(bookings[i].reference + "~" + bookings[i].reference2 + "~" +
                            bookings[i].sit_name + "~" + bookings[i].car_name + "~" +
                            bookings[i].dt.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt1.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt2.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt3.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt4.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt5.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt6.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt7.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt8.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].errormessage);
                    }
                    sw.WriteLine();
                    if (bookings.Count > 0)
                    {
                        formsetup._setup[10].dbl = bookings[bookings.Count - 1].dt.AddSeconds(-1).ToOADate(); // laatste datum
                        formsetup.writesetup_();
                    } */
                }
                catch (Exception ex)
                {
                    log.AppendText("error in dockman webservice:" + ex.Message);
                    writelog("error in dockman webservice:" + ex.Message);
                    applicationlog("DM webservice", "error in dockman webservice:" + ex.Message, "");
                }
            }

            return (1);
        }

        private string filter(string ip)
        {
            return ip.Replace("*", "");
        }
        
        private void textBox12_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[30].str = textBox12.Text;
            formsetup.writesetup_();
        }

        private void textBox16_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[30].integer = atol(textBox16.Text);
            formsetup.writesetup_();
        }

        //---------------- extract file -------------
        private void getfile(string ipfn, string separator, string destfn)
        {
            StreamReader sr = new StreamReader(ipfn, Encoding.GetEncoding("iso-8859-15"));
            StreamWriter sw = new StreamWriter(destfn, false, Encoding.GetEncoding("iso-8859-15"));
            string dstr;
            bool writing = false;
            while ((dstr = sr.ReadLine()) != null)
            {
                if (dstr.Length > 0)
                {
                    if (dstr.Substring(0, 1) == "[")
                    {
                        writing = false;
                        if (dstr.IndexOf(separator) >= 0)
                            writing = true;
                    }
                    else
                    {
                        if (writing)
                        {
                            sw.WriteLine(dstr);
                        }
                    }
                }
            }
            sr.Close();
            sw.Close();
        }
        private string process_lineXML(string ipstr)
        {
            int a, b;

            while ((a = ipstr.IndexOf('[')) >= 0)
            {
                if ((b = ipstr.IndexOf(']')) >= 0)
                {
                    if (b - a < 2)
                    {
                        MessageBox.Show("wrong sequence []: " + ipstr);
                        Application.Exit();
                    }
                    string tempstr = ipstr.Substring(a + 1, b - a - 1);
                    ipstr = ipstr.Remove(a, b - a + 1);
                    ipstr = ipstr.Insert(a, process_field(tempstr));
                }
                else
                {
                    MessageBox.Show("Not found ']':" + ipstr);
                    Application.Exit();
                    return (ipstr);
                }

            }

            return (ipstr);
        }


        //---------------- process XML file -------------
        private void process_XMLfile(ref XmlWriter XMLWriter, string maskfn)
        {
            StreamReader sr = new StreamReader(maskfn, Encoding.GetEncoding("iso-8859-15"));
            string dstr, dstr2;

            while ((dstr = sr.ReadLine()) != null)
            {
                string[] split = dstr.Split(';');
                for (int i = 0; i < split.Length; i++)
                {
                    split[i] = split[i].Trim();
                }
                if (split.Length > 0)
                {
                    switch (split[0])
                    {
                        case "!": break; // remark
                        case "+": if (split.Length < 2)
                            {
                                MessageBox.Show("+ code te kort!");
                                Application.Exit();
                            }
                            int len = split.Length;
                            if (split[len - 1].Trim().Length == 0)
                                len--;
                            switch (len)
                            {
                                case 2: XMLWriter.WriteStartElement(split[1]); break;
                                case 3: XMLWriter.WriteStartElement(split[1], split[2]); break;
                                case 4:
                                    XMLWriter.WriteStartElement(split[1]);
                                    XMLWriter.WriteAttributeString(process_field(split[2]), process_field(split[3]));
                                    break;
                                default: XMLWriter.WriteStartElement(split[2], split[1], split[3]); break;

                            }

                            break;
                        case "-": XMLWriter.WriteEndElement();
                            break;
                        case "F":
                            dstr2 = process_line(split[2]);

                            if ((formsetup._setup[0].integer > 0) && (dstr2.Trim().Length == 0))
                            {
                                //break; // filter lege lijnen
                            }
                            if ((split.Length >= 4) && (split[3].ToLower().IndexOf("datetime_tc") >= 0))
                            {
                                if (dstr2.Trim().Length > 0)
                                    dstr2 = todt(dstr2).ToString("yyyyMMddHHmm");
                            }
                            else
                                if ((split.Length >= 4) && (split[3].ToLower().IndexOf("datetime") >= 0))
                                {
                                    if (dstr2.Trim().Length > 0)
                                        dstr2 = todt(dstr2).ToString("o");
                                }

                            if ((split.Length >= 4) && (split[3].ToLower().IndexOf("bool") >= 0))
                            {
                                dstr2 = atol(dstr2) > 0 ? "true" : "false";
                            }
                            if ((split.Length >= 4) && (split[3].ToLower().IndexOf("float") >= 0))
                            {
                                dstr2 = atof(dstr2).ToString("0.00");//.Replace(",", ".");
                            }
                            if ((split.Length >= 4) && (split[3].ToLower().IndexOf("int") >= 0))
                            {
                                dstr2 = atol(dstr2).ToString();
                            }
                            bool process = true;
                            if ((split.Length >= 4) && (split[3].ToLower().IndexOf("nonempty") >= 0))
                            {
                                if (dstr2.Trim().Length == 0)
                                    process = false;
                            }

                            if (process)
                                switch (split.Length)
                                {
                                    case 4:
                                        if (dstr2 != "null")
                                        {
                                            XMLWriter.WriteElementString(split[1], dstr2);
                                        }
                                        else
                                        {
                                            string dstr3 = File.ReadAllText("empty.txt");
                                            XMLWriter.WriteRaw("<" + split[1] + " " + dstr3 + " />");
                                            //XMLWriter.WriteElementString("xsi", split[1], dstr3, null); // nil specificatie
                                        }
                                        break;
                                    case 5:

                                        //XMLWriter.WriteAttributeString(split[1], "uom=KG", dstr2);
                                        //XMLWriter.WriteAttributeString("", split[4], "uom", dstr2);
                                        //XMLWriter.WriteAttributeString("uom", split[1],"kg",dstr2);
                                        //XMLWriter.WriteAttributeString("uom", dstr2, split[1],split[4]);

                                        XMLWriter.WriteElementString(split[1], split[4], dstr2);
                                        //XMLWriter.WriteAttributeString("", split[1], split[4], dstr2);
                                        //XMLWriter.WriteAttributeString("uom", split[1], split[4], dstr2);
                                        break;
                                    case 6: XMLWriter.WriteStartElement(split[1]);
                                        XMLWriter.WriteAttributeString(split[4], split[5]);
                                        XMLWriter.WriteString(dstr2);
                                        XMLWriter.WriteEndElement();

                                        break;
                                }
                            break;
                    }
                }
            }
            sr.Close();
        }

        private void generate_XML(int soort)
        {
            try
            {

                //XMLWriter = new XmlTextWriter(TEMPXMLFN, Encoding.GetEncoding("ISO-8859-1"));
                //XMLWriter = new XmlTextWriter(TEMPXMLFN, Encoding.ASCII);

                string fn = "KC_" + formsetup._setup[20].integer.ToString() + ".XML";
                formsetup._setup[20].integer++;
                formsetup.writesetup_();

                XMLWriter = XmlWriter.Create(fn);
                //XMLWriter.Formatting = Formatting.Indented;

                XMLWriter.WriteStartDocument();
                XMLWriter.WriteComment("version 1.1 - Van der Wal/JWV");
                //XMLWriter.WriteAttributeString("xmlns", "uom", null, "http://schemas.3Gtms.com/tmw/va/tns");

                //XMLWriter.WriteStartElement("ns2", "Orderdata", "http://xx.com");  DEZE WERKTE
                if (soort==0) getfile("KC_SHIPPL01.txt", "[BODY]", TEMPFN);
                if (soort==1) getfile("KC_ZEU01.txt", "[BODY]", TEMPFN);
                process_XMLfile(ref XMLWriter, TEMPFN);

                XMLWriter.Close();
                writelog("Done");


            }
            catch (Exception ex)
            {
                writelog("error in create_XML: " + ex.Message);
                log.AppendText("error in create_XML: " + ex.Message);
                return;
            }

            return;
        }

        private void button7_Click(object sender, EventArgs e)
        {
            Client TWclient = new Client();
            List<string> list = TWclient.GetOrderGuids();
            TWclient.DownloadOrders(list);
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            formsetup._setup[13].integer = checkBox5.Checked ? 1 : 0;
            formsetup.writesetup_();
        }

        private void textBox22_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[31].str = textBox22.Text; // Calloff template
            formsetup.writesetup_();
        }

        private void button4_Click(object sender, EventArgs e)
        {  // shippl create button, losse actie
            bool first = true;
            check_databaseopen(1);
            try
            {
                SQLcmd = new SqlCommand(
                    "select top 100 " +
                    "(select top 1 addrname  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addrname, " +
                    "(select top 1 addr1  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addr1, " +
                    "(select top 1 addr2  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addr2, " +
                    "(select top 1 cityname from Loc where loc.LocId=ordheader.LocIdOrig) as f_cityname, " +
                    "(select top 1 postalcode from Loc where loc.LocId=ordheader.LocIdOrig) as f_postalcode, " +
                    "(select top 1 CountryISO2 from Loc inner join Country on country.CountryId=loc.countryid where loc.LocId=ordheader.LocIdOrig) as f_country, " +
                    "(select top 1 addrname  from Loc where loc.LocId=ordheader.LocIddest ) as t_addrname, " +
                    "(select top 1 addr1  from Loc where loc.LocId=ordheader.LocIdDest) as t_addr1, " +
                    "(select top 1 addr2  from Loc where loc.LocId=ordheader.LocIdDest) as t_addr2, " +
                    "(select top 1 cityname from Loc where loc.LocId=ordheader.LocIdDest) as t_cityname, " +
                    "(select top 1 postalcode from Loc where loc.LocId=ordheader.LocIdDest) as t_postalcode, " +
                    "(select top 1 CountryISO2 from Loc inner join Country on country.CountryId=loc.countryid where loc.LocId=ordheader.LocIdDest) as t_country, " +
                    "(select top 1 commentvalue from OrdComment where ordcomment.ordheaderid=ordheader.OrdHeaderId and qualifierid=25) as locationid, " +
                    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=8) as ref99, " +
                    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=9) as ref45, " +
                    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=24) as deldoc,  "+
                    "(select top 1 interstateCcId from TradingPartnerCarrier where Tradingpartnercarrier.tradingpartnerid=l.TradingPartnerIdCarrier) as car_ref, " +
                    "(select top 1 tradingpartnername from TradingPartnerCarrier left join TradingPartner on TradingPartnerCarrier.TradingPartnerId = TradingPartner.TradingPartnerId where Tradingpartnercarrier.tradingpartnerid = l.TradingPartnerIdCarrier) as car_name, "+
                    "* from load as l " +
                    "left join ShipmentLoad  on shipmentload.loadid = l.LoadId " +
      //                    "right join ShipmentOrdLeg on shipmentordleg.ShipmentId=shipmentload.ShipmentId " +
                    "outer apply (select top 1 * from ShipmentOrdLeg where shipmentordleg.ShipmentId=shipmentload.ShipmentId ) shipmentOrdLeg " +
                  " left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid " +
                    "left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId " +
                    "left join OrdLine on ordheader.ordheaderid = ordline.OrdHeaderId " +
                    "left join Commodity on commodity.CommodityId=ordline.CommodityId " +
                    "left join currency on currency.CurrencyId = ordheader.CurrencyId_NetFreightChargeTot "+
                    "where l.LoadNum=@loadnum " +
                    "and (select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=24) is not null "+// deldoc
                    "order by ordheader.OrdHeaderId desc ", SQLconn);
                SQLcmd.Parameters.AddWithValue("loadnum", textBox15.Text);
                SQL_x = SQLcmd.ExecuteReader();
                XMLcnt = 0;
                while (SQL_x.Read())
                {
                    try
                    {
                        if (first)
                        {
                            string fn = "3G_SHIPMENT_" + formsetup._setup[20].integer.ToString() + ".XML";
                            formsetup._setup[20].integer++;
                            formsetup.writesetup_();

                            XMLWriter = XmlWriter.Create(fn);
                            //XMLWriter.Formatting = Formatting.Indented;

                            XMLWriter.WriteStartDocument();
                            XMLWriter.WriteComment("version 1.1 - Van der Wal/JWV");
                            getfile("KC_SHIPPL01.txt", "[HEADER]", TEMPFN);
                            process_XMLfile(ref XMLWriter, TEMPFN);
                            first = false;
                        }
                         getfile("KC_SHIPPL01.txt", "[BODY]", TEMPFN);
                        process_XMLfile(ref XMLWriter, TEMPFN);
                    }

                    catch (Exception ex)
                    {
                        writelog("error in create_XML: " + ex.Message);
                        log.AppendText("error in create_XML: " + ex.Message);
                        return;
                    }

                    //return;
                }
                if (!first)
                {
                    getfile("KC_SHIPPL01.txt", "[FOOTER]", TEMPFN);
                    process_XMLfile(ref XMLWriter, TEMPFN);
                    XMLWriter.Close();
                }
                writelog("Done");
                SQL_x.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("error in TW: " + ex.Message);
            }

        }

        private void button8_Click(object sender, EventArgs e)
        {
            check_databaseopen(1);
            SQLcmd = new SqlCommand(
"select top 1 " +            // TOP 1  want er kunnen in een consol meerdere orders voorkomen. Neem hoogste id is hoogste 999
"(select top 1 addrname  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addrname, " +
"(select top 1 addr1  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addr1, " +
"(select top 1 addr2  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addr2, " +
"(select top 1 cityname from Loc where loc.LocId=ordheader.LocIdOrig) as f_cityname, " +
"(select top 1 postalcode from Loc where loc.LocId=ordheader.LocIdOrig) as f_postalcode, " +
"(select top 1 CountryISO2 from Loc inner join Country on country.CountryId=loc.countryid where loc.LocId=ordheader.LocIdOrig) as f_country, " +
"(select top 1 addrname  from Loc where loc.LocId=ordheader.LocIddest ) as t_addrname, " +
"(select top 1 addr1  from Loc where loc.LocId=ordheader.LocIdDest) as t_addr1, " +
"(select top 1 addr2  from Loc where loc.LocId=ordheader.LocIdDest) as t_addr2, " +
"(select top 1 cityname from Loc where loc.LocId=ordheader.LocIdDest) as t_cityname, " +
"(select top 1 postalcode from Loc where loc.LocId=ordheader.LocIdDest) as t_postalcode, " +
"(select top 1 CountryISO2 from Loc inner join Country on country.CountryId=loc.countryid where loc.LocId=ordheader.LocIdDest) as t_country, " +
"(select top 1 commentvalue from OrdComment where ordcomment.ordheaderid=ordheader.OrdHeaderId and qualifierid=25) as locationid, " +
"(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=8) as ref99, " +
"(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=9) as ref45, " +
"(select top 1 locnum from loc with (NOLOCK) where loc.locid = ordleg.LocIdOrig) as f_locnum, "+
"(select top 1 locnum from loc with(NOLOCK) where loc.locid = ordleg.LocIdDest) as t_locnum, "+
"(select refnumvalue + ' ' AS 'data()' from load as l2 with (NOLOCK)  " +
"left join loadtender on loadtender.loadid  = l2.loadid  " +
"left join ShipmentLoad  with (NOLOCK) on shipmentload.loadid = l2.LoadId  " +
"left join ShipmentOrdLeg with (NOLOCK) on shipmentordleg.ShipmentId=shipmentload.ShipmentId  " +
"left join ordleg with (NOLOCK) on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid  " +
"left join OrdRefNum with (NOLOCK) on ordrefnum.OrdHeaderId=ordleg.OrdHeaderId and QualifierId=9  " +
"where l2.loadid = l.loadid FOR XML PATH('') " +
") as ref45_2, " +
//------- new okt 2019
"TradingPartnerCarrier.interstateCcId as car_ref, "+
"TradingPartner.tradingpartnername as car_name, "+ 
//"(select top 1 interstateCcId from TradingPartnerCarrier where Tradingpartnercarrier.tradingpartnerid=l.TradingPartnerIdCarrier) as car_ref, " +
//"	(select top 1 tradingpartnername from TradingPartnerCarrier left join TradingPartner on TradingPartnerCarrier.TradingPartnerId=TradingPartner.TradingPartnerId where Tradingpartnercarrier.tradingpartnerid=l.TradingPartnerIdCarrier) as car_name, "+
"* from load as l " +
"left join ShipmentLoad  on shipmentload.loadid = l.LoadId " +
"right join ShipmentOrdLeg on shipmentordleg.ShipmentId=shipmentload.ShipmentId " +
// hieronder geen outer apply doen omdat we alle orders van de load willen verwerken.
//"outer apply (select top 1 * from ShipmentOrdLeg where shipmentordleg.ShipmentId=shipmentload.ShipmentId ) shipmentOrdLeg "+
"left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid " +
"left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId " +
"left join OrdLine on ordheader.ordheaderid = ordline.OrdHeaderId " +
"left join Commodity on commodity.CommodityId=ordline.CommodityId " +
//----------- new per okt 2019
"left join  TradingPartnerCarrier on Tradingpartnercarrier.tradingpartnerid = l.TradingPartnerIdCarrier "+
"left join TradingPartner on TradingPartnerCarrier.TradingPartnerId = TradingPartner.TradingPartnerId "+
"where l.LoadNum=@loadnum " +
// hieronder de regel als deldoc verplicht is, is niet zo.
//" and (select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=24) is not null  "+
"order by ordheader.OrdHeaderId desc ", SQLconn);
            string loadnum = "L-0032719";
            SQLcmd.Parameters.AddWithValue("loadnum", loadnum);
            SQL_x = SQLcmd.ExecuteReader();
            while (SQL_x.Read())
            {
                writelog("start TW calloff for load " + SQL_x["loadnum"].ToString());
                if (TW_calloff(SQL_x["loadnum"].ToString(), 1) == 0)
                {
                }
            }
            SQL_x.Close();
        }

            private void button5_Click(object sender, EventArgs e)
        {  // zeu button, losse actie
            check_databaseopen(1);
            try
            {
                SQLcmd = new SqlCommand(
                    "select top 100 " +
                    "(select top 1 addrname  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addrname, " +
                    "(select top 1 addr1  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addr1, " +
                    "(select top 1 addr2  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addr2, " +
                    "(select top 1 cityname from Loc where loc.LocId=ordheader.LocIdOrig) as f_cityname, " +
                    "(select top 1 postalcode from Loc where loc.LocId=ordheader.LocIdOrig) as f_postalcode, " +
                    "(select top 1 CountryISO2 from Loc inner join Country on country.CountryId=loc.countryid where loc.LocId=ordheader.LocIdOrig) as f_country, " +
                    "(select top 1 addrname  from Loc where loc.LocId=ordheader.LocIddest ) as t_addrname, " +
                    "(select top 1 addr1  from Loc where loc.LocId=ordheader.LocIdDest) as t_addr1, " +
                    "(select top 1 addr2  from Loc where loc.LocId=ordheader.LocIdDest) as t_addr2, " +
                    "(select top 1 cityname from Loc where loc.LocId=ordheader.LocIdDest) as t_cityname, " +
                    "(select top 1 postalcode from Loc where loc.LocId=ordheader.LocIdDest) as t_postalcode, " +
                    "(select top 1 CountryISO2 from Loc inner join Country on country.CountryId=loc.countryid where loc.LocId=ordheader.LocIdDest) as t_country, " +
                    "(select top 1 commentvalue from OrdComment where ordcomment.ordheaderid=ordheader.OrdHeaderId and qualifierid=25) as locationid, " +
                    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=8) as ref99, " +
                    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=9) as ref45, " +
                    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=24) as deldoc,  " +
                    "(select top 1 interstateCcId from TradingPartnerCarrier where Tradingpartnercarrier.tradingpartnerid=l.TradingPartnerIdCarrier) as car_ref, " +
                    "(select top 1 tradingpartnername from TradingPartnerCarrier left join TradingPartner on TradingPartnerCarrier.TradingPartnerId=TradingPartner.TradingPartnerId where Tradingpartnercarrier.tradingpartnerid=l.TradingPartnerIdCarrier) as car_name, "+
                    "* from load as l " +
                    "left join ShipmentLoad  on shipmentload.loadid = l.LoadId " +
    //                    "right join ShipmentOrdLeg on shipmentordleg.ShipmentId=shipmentload.ShipmentId " +
                    "outer apply (select top 1 * from ShipmentOrdLeg where shipmentordleg.ShipmentId=shipmentload.ShipmentId ) shipmentOrdLeg " +
                    "left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid " +
                    "left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId " +
                    "left join OrdLine on ordheader.ordheaderid = ordline.OrdHeaderId " +
                    "left join Commodity on commodity.CommodityId=ordline.CommodityId " +
                    "left join currency on currency.CurrencyId = ordheader.CurrencyId_NetFreightChargeTot " +
                    "where l.LoadNum=@loadnum " +
                    "and (select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=24) is not null "+
                    "order by ordheader.OrdHeaderId desc ", SQLconn);
                SQLcmd.Parameters.AddWithValue("loadnum", textBox15.Text);
                SQL_x = SQLcmd.ExecuteReader();
                while (SQL_x.Read())
                {
                    linecnt = 0;
                    generate_XML(1);
                }
                SQL_x.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("error in TW: " + ex.Message);
            }


        }

        private void textBox23_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[10].dbl = todt(textBox23.Text).ToOADate();
            formsetup.writesetup_();
        }

        private void textBox24_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[32].str =textBox24.Text;
            formsetup.writesetup_();
        }

        private void run()
        {
            log.AppendText("start run....\r\n");
            button6_Click(null, null); // (check shipl)+ TW
            button1_Click(null, null); // FTP
            if (formsetup._setup[13].integer > 0)
            {
                TranwsWidedownload();
            }
            smartway_retrieve();
        }

        private void button9_Click(object sender, EventArgs e)
        {
            smartway_retrieve();
        }

        private void button10_Click(object sender, EventArgs e)
        {
            // repair op basis van repair.txt lijst met 45 codes
            try
            {
                check_databaseopen(1);
                StreamReader sr = new StreamReader("repair.txt");
                string dstr = "";
                while ((dstr = sr.ReadLine()) != null)
                {

                    SQLcmd2 = new SqlCommand(
                        "select distinct(load.loadnum) from load " +
                        "left join ShipmentLoad  on shipmentload.loadid = load.LoadId " +
                        "outer apply(select top 1 * from ShipmentOrdLeg where shipmentordleg.ShipmentId = shipmentload.ShipmentId ) shipmentOrdLeg " +
                        "left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.OrdLegId " +
                        "left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId " +
                        "inner join OrdRefNum  on ordrefnum.OrdHeaderId = ordheader.ordheaderid and charindex(@refnumvalue,ordrefnum.RefNumValue)>0 ", SQLconn2);
                    SQLcmd2.Parameters.AddWithValue("refnumvalue", dstr);
                    SQL_x2 = SQLcmd2.ExecuteReader();
                    if (SQL_x2.Read())
                    {
                        string loadnum = SQL_x2["loadnum"].ToString().Trim();
                        log.AppendText("\r\nProcess loadnum:" + loadnum); wait(100);
                        create_reply(loadnum, 1);
                        update_order_status(dstr, 1, loadnum);
                    }
                    SQL_x2.Close();

                }
                sr.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("repair error: " + ex.Message);
            }
        }

        private void textBox25_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[33].str = textBox25.Text;
            formsetup.writesetup_();
        }

        private void smartway_retrieve()
        {
            if (formsetup._setup[15].integer > 0)
            {
                try
                {
                    //--- retrieval
                    webservice.DMWSSoapClient dmws = new webservice.DMWSSoapClient();
                    webservice.LoginData login = new webservice.LoginData();
                    login.LoginName = "smartway"; login.Password = "utrecht";
                    webservice.ArrayOfBooking bookings = new webservice.ArrayOfBooking();

                    bookings = dmws.retrieve_realisations(DateTime.FromOADate(formsetup._setup[10].dbl), login);
                    log.AppendText("retrieved " + bookings.Count.ToString() + " bookings from "+ DateTime.FromOADate(formsetup._setup[10].dbl).ToString("yyyy-MM-dd HH:mm:ss")+"\r\n");

                    StreamWriter sw = new StreamWriter(formsetup._setup[32].str + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    sw.WriteLine("==0.30==" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    for (int i = 0; i < bookings.Count; i++)
                    {
                        sw.WriteLine(bookings[i].reference + "~" + bookings[i].reference2 + "~" +
                            bookings[i].sit_name + "~" + bookings[i].car_name + "~" +
                            bookings[i].action+"~"+
                            bookings[i].dt_planned.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt1.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt2.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt3.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt4.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt5.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt6.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt7.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].dt8.ToString("yyyy-MM-dd HH:mm:ss") + "~" +
                            bookings[i].errormessage);
                    }
                    sw.Close();
                    if (bookings.Count >= 1)
                    {
                        formsetup._setup[10].dbl = bookings[bookings.Count - 1].dt.AddSeconds(-10).ToOADate(); // laatste datum
                        log.AppendText("New date for retrieval: "+ DateTime.FromOADate(formsetup._setup[10].dbl).ToString("yyyy-MM-dd HH:mm:ss")+"\r\n");
                        textBox23.Text = DateTime.FromOADate(formsetup._setup[10].dbl).ToString("yyyy-MM-dd HH:mm:ss");
                        formsetup.writesetup_();
                    }
                }
                catch (Exception ex)
                {
                    log.AppendText("error in dockman webservice:" + ex.Message);
                    writelog("error in dockman webservice:" + ex.Message);
                    applicationlog("DM webservice", "error in dockman webservice:" + ex.Message, "");
                }

            }
        }
            private void TranwsWidedownload()
        {
            try
            {
                Client TWclient = new Client();
                List<string> list = TWclient.GetOrderGuids();
                log.AppendText("TW updates: " + list.Count.ToString() + " files ready\r\n");
                TWclient.DownloadOrders_100(list);

            }
            catch (Exception ex)
            {
                writelog("error in processing TW:" + ex.Message);
                log.AppendText("error in processing TW: " + ex.Message + "\r\n");
            }
        }



        int timercnt = 10;
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (!checkBox1.Checked)
                return;

            timer1.Enabled = false;
            timercnt--;
            checkBox1.Text = "autorun in " + timercnt.ToString() + " seconds";
            if (timercnt == 0)
            {
                checkBox1.Text = "Run now...";

                run();

                checkBox1.Text = "ready...";
                wait(2000);
                if (checkBox1.Checked) 
                    Close();
            }

            timer1.Enabled = true;

        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            formsetup._setup[10].integer = checkBox2.Checked ? 1 : 0;
            formsetup.writesetup_();
        }

        private void textBox17_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[24].str = textBox17.Text;
            formsetup.writesetup_();
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            formsetup._setup[11].integer = checkBox3.Checked ? 1 : 0;
            formsetup.writesetup_();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            //************************8
            if (false)
                try  // check shippl button
                {
                    check_databaseopen(1);

                    SQLcmd2 = new SqlCommand(
        "select distinct(load.LoadNum) from load " +
        "left join ShipmentLoad  on shipmentload.loadid = load.LoadId " +
         //"left join ShipmentOrdLeg on shipmentordleg.ShipmentId = shipmentload.ShipmentId " +
         "outer apply (select top 1 * from ShipmentOrdLeg where shipmentordleg.ShipmentId=shipmentload.ShipmentId ) shipmentOrdLeg " +
        "left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.OrdLegId " +
         "left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId " +
        "left join TradingPartner on tradingpartner.TradingPartnerId = ordheader.TradingPartnerIdClient and DataValue_TradingPartnerType = 'Client' " +
        "where ((load.DataValue_LoadTmsStatus in ('InTransit', 'Delivered','Closed'))  or ( load.DataValue_LoadTmsStatus in ('Accepted') and load.datetime_plannedStart<getdate()-1))   and " +
         "(select  count(*) from load as l " +
         "                    left join ShipmentLoad  on shipmentload.loadid = l.LoadId " +
         "                    right join ShipmentOrdLeg on shipmentordleg.ShipmentId = shipmentload.ShipmentId " +

         "                    left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.OrdLegId " +
         "                    left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId " +
         "                    left join ordcomment oc on oc.OrdHeaderId = ordheader.OrdHeaderId and oc.QualifierId = 27 " +// KC-status, 30 test
         "                    left join OrdRefNum orr on orr.OrdHeaderId = ordheader.OrdHeaderId and orr.QualifierId = 24 " +// Deldoc
        "                     left join OrdComment orc2 on orc2.OrdHeaderId = ordheader.OrdHeaderId and orc2.QualifierId = 26 " +
        "                     where(l.loadid = load.loadid)  and(oc.commentvalue is null) and " +
        "                      ((orr.RefNumValue is not null)OR(orc2.commentValue = 'PO')) ) > 0 " +
        "                    and(TradingPartner.TradingPartnerNum  in ('TP-00000520', 'TP-00000521', 'TP-00000344')) and ordheader.DateTime_EarlyDelivery>getdate()-77" +
        "                   order by load.LoadNum ", SQLconn2);

                    SQLcmd2 = new SqlCommand(
                    "select top 1000  l.loadnum, l.loadid, " +
                    "(select count(ordrefnum.OrdHeaderId) from ShipmentLoad with(NOLOCK) " +
                    "left join ShipmentOrdLeg with(NOLOCK) on shipmentordleg.ShipmentId = shipmentload.ShipmentId " +
                    "left join ordleg with(NOLOCK) on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid " +
                    "left join OrdRefNum with (NOLOCK) on ordrefnum.ordHeaderid = ordleg.OrdHeaderId " +
                    "where shipmentload.loadid = l.loadid and OrdRefNum.QualifierId = 24) as deldocs, " +
                    "(select count(ordleg.OrdHeaderId) from ShipmentLoad with(NOLOCK) " +
                    "left join ShipmentOrdLeg with(NOLOCK) on shipmentordleg.ShipmentId = shipmentload.ShipmentId " +
                    "left join ordleg with(NOLOCK) on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid " +
                    " where shipmentload.loadid = l.loadid) as orders " +

                    "from load as l with(NOLOCK) " +
                    "left join ShipmentLoad with(NOLOCK) on shipmentload.loadid = l.LoadId " +
                    "left join ShipmentOrdLeg with(NOLOCK)on shipmentordleg.ShipmentId = shipmentload.ShipmentId " +
                    "left join ordleg with(NOLOCK)on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid " +
                    "left join ordheader with(NOLOCK)on ordheader.ordheaderid = ordleg.OrdHeaderId " +
                    "left join TradingPartner on tradingpartner.TradingPartnerId = ordheader.TradingPartnerIdClient and DataValue_TradingPartnerType = 'Client' " +

                    "where tradingpartner.TradingPartnerNum  in ('TP-00000520', 'TP-00000521', 'TP-00000344') " +
                    "and l.DataValue_LoadTmsStatus in ( 'Intransit', 'Delivered', 'completed','Closed') " +
                    "and(select count(*) from OrdComment with (NOLOCK) where ordcomment.QualifierId = 27 and ordcomment.OrdHeaderId = ordheader.OrdHeaderId) = 0 " +
                    "and l.DateLastModified > '2018-01-01' " +
                    "and " +
                    "((select count(ordrefnum.OrdHeaderId) from ShipmentLoad with(NOLOCK) " +
                    " left join ShipmentOrdLeg with(NOLOCK) on shipmentordleg.ShipmentId = shipmentload.ShipmentId " +
                    "left join ordleg with(NOLOCK) on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid " +
                    "left join OrdRefNum with (NOLOCK) on ordrefnum.ordHeaderid = ordleg.OrdHeaderId " +
                    "where s0hipmentload.loadid = l.loadid and OrdRefNum.QualifierId = 24) >= " +
                    "(select count(ordleg.OrdHeaderId) from ShipmentLoad with(NOLOCK) " +
                    "left join ShipmentOrdLeg with(NOLOCK) on shipmentordleg.ShipmentId = shipmentload.ShipmentId " +
                    "left join ordleg with(NOLOCK) on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid " +
                    "where shipmentload.loadid = l.loadid) OR " +
                    "(select top 1  orc.commentvalue from OrdComment orc where orc.OrdHeaderId = ordheader.OrdHeaderId and orc.QualifierId = 26)='PO'  ) " +
                    "group by l.loadnum,l.loadid", SQLconn2);

                    SQL_x2 = SQLcmd2.ExecuteReader();
                    while (SQL_x2.Read())
                    {
                        writelog("create reply for load " + SQL_x2["Loadnum"].ToString());
                        create_reply(SQL_x2["Loadnum"].ToString(), 1);
                        wait(1);
                    }
                    SQL_x2.Close();
                    //---- 3Gbase
                    //---- deldoc versturen
                    SQLcmdx3 = new SqlCommand("select top 10 * from ordercomments with (NOLOCK) where orc_Q_QualifierName='KC-status' and orc_commentvalue='2'", SQLconnX3);
                    SQL_xx3 = SQLcmdx3.ExecuteReader();
                    while (SQL_xx3.Read())
                    {
                        IList<int> ordheaderid = new List<int>();

                        int ord_id = atol(SQL_xx3["orc_ord_id"].ToString());
                        writelog("delshippl: ord_id = " + ord_id.ToString());
                        SQLcmdx2 = new SqlCommand("select top 11 * from orderrefnums with (NOLOCK) where orr_ord_id=@orr_ord_id and orr_Q_qualifiername like 'po%'", SQLconnX2);
                        SQLcmdx2.Parameters.AddWithValue("orr_ord_id", ord_id);
                        SQL_xx2 = SQLcmdx2.ExecuteReader();
                        IList<string> ref45 = new List<string>();
                        while (SQL_xx2.Read())
                        {
                            ref45.Add(SQL_xx2["orr_refnumvalue"].ToString());
                        }
                        SQL_xx2.Close();

                        for (int i = 0; i < ref45.Count; i++)
                        {
                            writelog("delshippl ref45: " + ref45[i]);

                            SQLcmd2 = new SqlCommand("select  distinct(ordheaderid) from ordrefnum with (NOLOCK) where refnumvalue=@refnumvalue order by ordheaderid desc", SQLconn2);
                            SQLcmd2.Parameters.AddWithValue("refnumvalue", ref45[i]);
                            SQL_x2 = SQLcmd2.ExecuteReader();

                            while (SQL_x2.Read())
                            {
                                ordheaderid.Add(atol(SQL_x2["ordheaderid"].ToString()));
                            }
                            for (int j = 0; j < ordheaderid.Count; j++)
                            {
                                writelog("delshippl ordheaderid: " + ordheaderid[j].ToString());
                                SQL_x2.Close();

                                SQLcmd3 = new SqlCommand(
            "select distinct(load.LoadNum) from load with (NOLOCK) " +
            "                    left join ShipmentLoad with (NOLOCK) on shipmentload.loadid = load.LoadId  " +
            //        "                    left join ShipmentOrdLeg on shipmentordleg.ShipmentId=shipmentload.ShipmentId  " +
            "                    outer apply (select top 1 * from ShipmentOrdLeg with (NOLOCK) where shipmentordleg.ShipmentId=shipmentload.ShipmentId ) shipmentOrdLeg " +
            "                    left join ordleg with (NOLOCK) on ordleg.OrdLegId = ShipmentOrdLeg.OrdLegId " +
            "                    left join ordheader with (NOLOCK) on ordheader.ordheaderid = ordleg.OrdHeaderId   " +
            "					left join TradingPartner with (NOLOCK) on tradingpartner.TradingPartnerId =ordheader.TradingPartnerIdClient and DataValue_TradingPartnerType='Client' " +
            "               where ordheader.ordheaderid=@ordheaderid  ", SQLconn3);
                                SQLcmd3.Parameters.AddWithValue("ordheaderid", ordheaderid[j]);
                                SQL_x3 = SQLcmd3.ExecuteReader();
                                while (SQL_x3.Read())
                                {
                                    string loadnum = SQL_x3["loadnum"].ToString();
                                    writelog("delshippl load: " + loadnum);
                                    create_reply(SQL_x3["loadnum"].ToString(), 2); // delete shippl
                                }
                                SQL_x3.Close();
                            }
                        }
                        int orc_id = atol(SQL_xx3["orc_id"].ToString());
                        SQLcmdx2 = new SqlCommand("delete from ordercomments where orc_id=@orc_id", SQLconnX2);
                        SQLcmdx2.Parameters.AddWithValue("orc_id", orc_id);
                        SQLcmdx2.ExecuteNonQuery();
                    }
                    SQL_xx3.Close();


                }
                catch (Exception ex)
                {
                    applicationlog("FTP", "error in FTP-generate: " + ex.Message, "");
                    writelog("error in check for Shippl: " + ex.Message);
                    log.AppendText("eror in check for Shippl: " + ex.Message + "\r\n");
                }
            //*/
            // -----------------Smartway nu, was TW------------------------------------------------------------
            //if (false)
            try
            {
                log.AppendText("Check SW\r\n");
                check_databaseopen(1);
                SQLcmd2 = new SqlCommand(
"select distinct(load.LoadNum) from load " +
"                    left join ShipmentLoad  on shipmentload.loadid = load.LoadId  " +
//"                    left join ShipmentOrdLeg on shipmentordleg.ShipmentId=shipmentload.ShipmentId  " +
"                   outer apply (select top 1 * from ShipmentOrdLeg where shipmentordleg.ShipmentId=shipmentload.ShipmentId ) shipmentOrdLeg " +
"                    left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.OrdLegId " +
"                    left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId   " +
"					left join TradingPartner on tradingpartner.TradingPartnerId =ordheader.TradingPartnerIdClient and DataValue_TradingPartnerType='Client' " +
"where load.DataValue_LoadTmsStatus in ('Accepted','InTransit') and " +
"(select  count(*) from load as l  " +
"                    left join ShipmentLoad  on shipmentload.loadid = l.LoadId  " +
"                    left join ShipmentOrdLeg on shipmentordleg.ShipmentId=shipmentload.ShipmentId " +
"                    left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.OrdLegId " +
"                    left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId  " +
"					 left join ordcomment oc on oc.OrdHeaderId = ordheader.OrdHeaderId and oc.QualifierId=28 " +   // TW-status (31 test)
                                                                                                                   //"					left join OrdRefNum orr on orr.OrdHeaderId=ordheader.OrdHeaderId and orr.QualifierId=24 " +     // Deldoc
                                                                                                                   //"					left join OrdComment orc2 on orc2.OrdHeaderId=ordheader.OrdHeaderId and orc2.QualifierId=26 " + // KC-type: PO
"                    where l.loadid = load.loadid  and oc.commentvalue is null  and ordheader.User3GIdCreatedBy=12)>0  and " +    // 12 = integrationuserops
//"					TradingPartner.TradingPartnerNum  in ('TP-00000520','TP-00000521','TP-00000344') and ordheader.DateTime_EarlyDelivery>getdate()-7", SQLconn2);
"					TradingPartner.TradingPartnerNum  in ("+formsetup._setup[33].str+") and ordheader.DateTime_EarlyDelivery>getdate()-7", SQLconn2); // setup[33] is lijst van TP's

                SQL_x2 = SQLcmd2.ExecuteReader();
                while (SQL_x2.Read())
                {
                    SQLcmd = new SqlCommand(
    "select top 25 " +            // TOP 25: alle 45 nummers afmelden, meerdere calloffs  helaas.  OUD: want er kunnen in een consol meerdere orders voorkomen. Neem hoogste id is hoogste 999
    "(select top 1 addrname  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addrname, " +
    "(select top 1 addr1  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addr1, " +
    "(select top 1 addr2  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addr2, " +
    "(select top 1 cityname from Loc where loc.LocId=ordheader.LocIdOrig) as f_cityname, " +
    "(select top 1 postalcode from Loc where loc.LocId=ordheader.LocIdOrig) as f_postalcode, " +
    "(select top 1 CountryISO2 from Loc inner join Country on country.CountryId=loc.countryid where loc.LocId=ordheader.LocIdOrig) as f_country, " +
    "(select top 1 addrname  from Loc where loc.LocId=ordheader.LocIddest ) as t_addrname, " +
    "(select top 1 addr1  from Loc where loc.LocId=ordheader.LocIdDest) as t_addr1, " +
    "(select top 1 addr2  from Loc where loc.LocId=ordheader.LocIdDest) as t_addr2, " +
    "(select top 1 cityname from Loc where loc.LocId=ordheader.LocIdDest) as t_cityname, " +
    "(select top 1 postalcode from Loc where loc.LocId=ordheader.LocIdDest) as t_postalcode, " +
    "(select top 1 CountryISO2 from Loc inner join Country on country.CountryId=loc.countryid where loc.LocId=ordheader.LocIdDest) as t_country, " +
    "(select top 1 commentvalue from OrdComment where ordcomment.ordheaderid=ordheader.OrdHeaderId and qualifierid=25) as locationid, " +
    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=8) as ref99, " +
    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=9) as ref45, " +
    "(select top 1 locnum from loc with (NOLOCK) where loc.locid = ordleg.LocIdOrig) as f_locnum, " +
    "(select top 1 locnum from loc with(NOLOCK) where loc.locid = ordleg.LocIdDest) as t_locnum, " +
"(select refnumvalue + ' ' AS 'data()' from load as l2 with (NOLOCK)  " +
"left join loadtender on loadtender.loadid  = l2.loadid  " +
"left join ShipmentLoad  with (NOLOCK) on shipmentload.loadid = l2.LoadId  " +
"left join ShipmentOrdLeg with (NOLOCK) on shipmentordleg.ShipmentId=shipmentload.ShipmentId  " +
"left join ordleg with (NOLOCK) on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid  " +
"left join OrdRefNum with (NOLOCK) on ordrefnum.OrdHeaderId=ordleg.OrdHeaderId and QualifierId=9  " +
"where l2.loadid = l.loadid FOR XML PATH('') " +
") as ref45_2, " +

//------- new okt 2019
    "TradingPartnerCarrier.interstateCcId as car_ref, " +
    "TradingPartner.tradingpartnername as car_name, " +
    //"(select top 1 interstateCcId from TradingPartnerCarrier where Tradingpartnercarrier.tradingpartnerid=l.TradingPartnerIdCarrier) as car_ref, " +
    //"	(select top 1 tradingpartnername from TradingPartnerCarrier left join TradingPartner on TradingPartnerCarrier.TradingPartnerId=TradingPartner.TradingPartnerId where Tradingpartnercarrier.tradingpartnerid=l.TradingPartnerIdCarrier) as car_name, "+
    "* from load as l " +
    "left join ShipmentLoad  on shipmentload.loadid = l.LoadId " +
    "right join ShipmentOrdLeg on shipmentordleg.ShipmentId=shipmentload.ShipmentId " +
    // hieronder geen outer apply doen omdat we alle orders van de load willen verwerken.
    //"outer apply (select top 1 * from ShipmentOrdLeg where shipmentordleg.ShipmentId=shipmentload.ShipmentId ) shipmentOrdLeg "+
    "left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid " +
    "left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId " +
    "left join OrdLine on ordheader.ordheaderid = ordline.OrdHeaderId " +
    "left join Commodity on commodity.CommodityId=ordline.CommodityId " +
//----------- new per okt 2019
    "left join  TradingPartnerCarrier on Tradingpartnercarrier.tradingpartnerid = l.TradingPartnerIdCarrier " +
    "left join TradingPartner on TradingPartnerCarrier.TradingPartnerId = TradingPartner.TradingPartnerId " +
    
    "where l.LoadNum=@loadnum " +
    // hieronder de regel als deldoc verplicht is, is niet zo.
    //" and (select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=24) is not null  "+
    "order by ordheader.OrdHeaderId desc ", SQLconn);

                    string loadnum = SQL_x2["loadnum"].ToString();
                    SQLcmd.Parameters.AddWithValue("loadnum", SQL_x2["loadnum"].ToString());
                    SQL_x = SQLcmd.ExecuteReader();
                    while (SQL_x.Read())
                    {
                        writelog("start TW calloff for load " + SQL_x["loadnum"].ToString());
                        if (TW_calloff(SQL_x["loadnum"].ToString(), 1) == 0)
                        {
                            //create_reply(SQL_x2["Loadnum"].ToString(),1);
                            SQL_x.Close();
                            SQL_x2.Close();
                            writelog("--->geen loadnum in update!");
                            return;
                        }
                        writelog("update status TW load " + SQL_x["loadnum"].ToString());
                        update_order_status(SQL_x["ref45"].ToString(), 1, loadnum);
                    }
                    SQL_x.Close();
        //            create_reply(loadnum, 1); // nood
                }
                SQL_x2.Close();
            }
            catch (Exception ex)
            {
                applicationlog("FTP", "error in check for TW: " + ex.Message, "");
                writelog("error in check for TW: " + ex.Message);
                log.AppendText("eror in check for TW: " + ex.Message + "\r\n");
            }
            // -----------------TWcancel------------------------------------------------------------
            /*
            try
            {
                check_databaseopen(1);
                SQLcmd2 = new SqlCommand(
"select distinct(load.LoadNum) from load " +
"                    left join ShipmentLoad  on shipmentload.loadid = load.LoadId  " +
//"                    left join ShipmentOrdLeg on shipmentordleg.ShipmentId=shipmentload.ShipmentId  " +
"                   outer apply (select top 1 * from ShipmentOrdLeg where shipmentordleg.ShipmentId=shipmentload.ShipmentId ) shipmentOrdLeg " +
"                    left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.OrdLegId " +
"                    left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId   " +
"					left join TradingPartner on tradingpartner.TradingPartnerId =ordheader.TradingPartnerIdClient and DataValue_TradingPartnerType='Client' " +
"where " +
"  ((load.DataValue_LoadTmsStatus in ('Canceled')) or  " +
"   (select top 1 loadtender.DataValue_LoadTenderStatus from loadtender where loadtender.loadid = load.loadid order by loadtenderid desc) in ('Withdrawn')) " +
"and " +
"(select  count(*) from load as l  " +
"                    left join ShipmentLoad  on shipmentload.loadid = l.LoadId  " +
"                    left join ShipmentOrdLeg on shipmentordleg.ShipmentId=shipmentload.ShipmentId " +
"                    left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.OrdLegId " +
"                    left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId  " +
"					 left join ordcomment oc on oc.OrdHeaderId = ordheader.OrdHeaderId and oc.QualifierId=28 " +   // TW-status (31 test)
                                                                                                                   //"					left join OrdRefNum orr on orr.OrdHeaderId=ordheader.OrdHeaderId and orr.QualifierId=24 " +     // Deldoc
                                                                                                                   //"					left join OrdComment orc2 on orc2.OrdHeaderId=ordheader.OrdHeaderId and orc2.QualifierId=26 " + // KC-type: PO
"                    where l.loadid = load.loadid  and oc.commentvalue is not null  and ordheader.User3GIdCreatedBy=12)>0  and " +    // 12 = integrationuserops
"					TradingPartner.TradingPartnerNum  in ('TP-00000520','TP-00000521','TP-00000344') and ordheader.DateTime_EarlyDelivery>getdate()-7", SQLconn2);

                SQL_x2 = SQLcmd2.ExecuteReader();
                while (SQL_x2.Read())
                {
                    SQLcmd = new SqlCommand(
    "select top 200 " +
    "(select top 1 addrname  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addrname, " +
    "(select top 1 addr1  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addr1, " +
    "(select top 1 addr2  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addr2, " +
    "(select top 1 cityname from Loc where loc.LocId=ordheader.LocIdOrig) as f_cityname, " +
    "(select top 1 postalcode from Loc where loc.LocId=ordheader.LocIdOrig) as f_postalcode, " +
    "(select top 1 CountryISO2 from Loc inner join Country on country.CountryId=loc.countryid where loc.LocId=ordheader.LocIdOrig) as f_country, " +
    "(select top 1 addrname  from Loc where loc.LocId=ordheader.LocIddest ) as t_addrname, " +
    "(select top 1 addr1  from Loc where loc.LocId=ordheader.LocIdDest) as t_addr1, " +
    "(select top 1 addr2  from Loc where loc.LocId=ordheader.LocIdDest) as t_addr2, " +
    "(select top 1 cityname from Loc where loc.LocId=ordheader.LocIdDest) as t_cityname, " +
    "(select top 1 postalcode from Loc where loc.LocId=ordheader.LocIdDest) as t_postalcode, " +
    "(select top 1 CountryISO2 from Loc inner join Country on country.CountryId=loc.countryid where loc.LocId=ordheader.LocIdDest) as t_country, " +
    "(select top 1 commentvalue from OrdComment where ordcomment.ordheaderid=ordheader.OrdHeaderId and qualifierid=25) as locationid, " +
    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=8) as ref99, " +
    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=9) as ref45, " +
"(select refnumvalue + ' ' AS 'data()' from load as l2 with (NOLOCK)  " +
"left join loadtender on loadtender.loadid  = l2.loadid  " +
"left join ShipmentLoad  with (NOLOCK) on shipmentload.loadid = l2.LoadId  " +
"left join ShipmentOrdLeg with (NOLOCK) on shipmentordleg.ShipmentId=shipmentload.ShipmentId  " +
"left join ordleg with (NOLOCK) on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid  " +
"left join OrdRefNum with (NOLOCK) on ordrefnum.OrdHeaderId=ordleg.OrdHeaderId and QualifierId=9  " +
"where l2.loadid = l.loadid FOR XML PATH('') " +
") as ref45_2, " +
    "(select top 1 interstateCcId from TradingPartnerCarrier where Tradingpartnercarrier.tradingpartnerid=l.TradingPartnerIdCarrier) as car_ref, " +
    "(select top 1 tradingpartnername from TradingPartnerCarrier left join TradingPartner on TradingPartnerCarrier.TradingPartnerId=TradingPartner.TradingPartnerId where Tradingpartnercarrier.tradingpartnerid=l.TradingPartnerIdCarrier) as car_name," +
    "* from load as l " +
    "left join ShipmentLoad  on shipmentload.loadid = l.LoadId " +
    "right join ShipmentOrdLeg on shipmentordleg.ShipmentId=shipmentload.ShipmentId " +
    // hieronder geen outer apply doen omdat we alle orders van de load willen verwerken.
    //"outer apply (select top 1 * from ShipmentOrdLeg where shipmentordleg.ShipmentId=shipmentload.ShipmentId ) shipmentOrdLeg "+
    "left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid " +
    "left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId " +
    "left join OrdLine on ordheader.ordheaderid = ordline.OrdHeaderId " +
    "left join Commodity on commodity.CommodityId=ordline.CommodityId " +
    "where l.LoadNum=@loadnum " +
    // hieronder de regel als deldoc verplicht is, is niet zo.
    //" and (select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=24) is not null  "+
    "order by ordheader.OrdHeaderId desc ", SQLconn);
                    string loadnum = SQL_x2["loadnum"].ToString();
                    SQLcmd.Parameters.AddWithValue("loadnum", SQL_x2["loadnum"].ToString());
                    SQL_x = SQLcmd.ExecuteReader();
                    while (SQL_x.Read())
                    {
                        writelog("start TW calloff cancel for load " + SQL_x["loadnum"].ToString());
                        if (TW_calloff(SQL_x["loadnum"].ToString(), -1) == 0)
                        {
                            SQL_x.Close();
                            SQL_x2.Close();
                            writelog("geen loadnum in update!");
                            return;
                        }
                        writelog("update status TW cancel load " + SQL_x["loadnum"].ToString());
                        log.AppendText("Cancel TW loadnr " + SQL_x["loadnum"].ToString() + "\r\n");
                        Application.DoEvents();
                        update_order_status(SQL_x["ref45"].ToString(), 10, loadnum);  //10 = cancel calloff
                    }
                    SQL_x.Close();
                }
                SQL_x2.Close();
            }
            catch (Exception ex)
            {
                applicationlog("FTP", "error in check for TW: " + ex.Message, "");
                writelog("error in check for TW: " + ex.Message);
                log.AppendText("eror in check for TW: " + ex.Message + "\r\n");
            }
            */
          // -----------------Smartwaycancel------------------------------------------------------------
            
            try
            {
                check_databaseopen(1);
                SQLcmd2 = new SqlCommand(
"select distinct(load.LoadNum) from load " +
"                    left join ShipmentLoad  on shipmentload.loadid = load.LoadId  " +
//"                    left join ShipmentOrdLeg on shipmentordleg.ShipmentId=shipmentload.ShipmentId  " +
"                   outer apply (select top 1 * from ShipmentOrdLeg where shipmentordleg.ShipmentId=shipmentload.ShipmentId ) shipmentOrdLeg " +
"                    left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.OrdLegId " +
"                    left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId   " +
"					left join TradingPartner on tradingpartner.TradingPartnerId =ordheader.TradingPartnerIdClient and DataValue_TradingPartnerType='Client' " +
"where " +
"  ((load.DataValue_LoadTmsStatus in ('Canceled')) or  " +
"   (select top 1 loadtender.DataValue_LoadTenderStatus from loadtender where loadtender.loadid = load.loadid order by loadtenderid desc) in ('Withdrawn')) " +
"and " +
"(select  count(*) from load as l  " +
"                    left join ShipmentLoad  on shipmentload.loadid = l.LoadId  " +
"                    left join ShipmentOrdLeg on shipmentordleg.ShipmentId=shipmentload.ShipmentId " +
"                    left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.OrdLegId " +
"                    left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId  " +
"					 left join ordcomment oc on oc.OrdHeaderId = ordheader.OrdHeaderId and oc.QualifierId=28 " +   // TW-status (31 test)
                                                                                                                   //"					left join OrdRefNum orr on orr.OrdHeaderId=ordheader.OrdHeaderId and orr.QualifierId=24 " +     // Deldoc
                                                                                                                   //"					left join OrdComment orc2 on orc2.OrdHeaderId=ordheader.OrdHeaderId and orc2.QualifierId=26 " + // KC-type: PO
"                    where l.loadid = load.loadid  and oc.commentvalue is not null  and ordheader.User3GIdCreatedBy=12)>0  and " +    // 12 = integrationuserops
"					TradingPartner.TradingPartnerNum  in ('TP-00000520','TP-00000521','TP-00000344') and ordheader.DateTime_EarlyDelivery>@dt", SQLconn2);

                SQLcmd2.Parameters.AddWithValue("dt", DateTime.FromOADate(formsetup._setup[10].dbl - 3.0 / 24.0));
                SQL_x2 = SQLcmd2.ExecuteReader();
                while (SQL_x2.Read())
                {
                    SQLcmd = new SqlCommand(
    "select top 200 " +
    "(select top 1 addrname  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addrname, " +
    "(select top 1 addr1  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addr1, " +
    "(select top 1 addr2  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addr2, " +
    "(select top 1 cityname from Loc where loc.LocId=ordheader.LocIdOrig) as f_cityname, " +
    "(select top 1 postalcode from Loc where loc.LocId=ordheader.LocIdOrig) as f_postalcode, " +
    "(select top 1 CountryISO2 from Loc inner join Country on country.CountryId=loc.countryid where loc.LocId=ordheader.LocIdOrig) as f_country, " +
    "(select top 1 addrname  from Loc where loc.LocId=ordheader.LocIddest ) as t_addrname, " +
    "(select top 1 addr1  from Loc where loc.LocId=ordheader.LocIdDest) as t_addr1, " +
    "(select top 1 addr2  from Loc where loc.LocId=ordheader.LocIdDest) as t_addr2, " +
    "(select top 1 cityname from Loc where loc.LocId=ordheader.LocIdDest) as t_cityname, " +
    "(select top 1 postalcode from Loc where loc.LocId=ordheader.LocIdDest) as t_postalcode, " +
    "(select top 1 CountryISO2 from Loc inner join Country on country.CountryId=loc.countryid where loc.LocId=ordheader.LocIdDest) as t_country, " +
    "(select top 1 commentvalue from OrdComment where ordcomment.ordheaderid=ordheader.OrdHeaderId and qualifierid=25) as locationid, " +
    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=8) as ref99, " +
    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=9) as ref45, " +
"(select refnumvalue + ' ' AS 'data()' from load as l2 with (NOLOCK)  " +
"left join loadtender on loadtender.loadid  = l2.loadid  " +
"left join ShipmentLoad  with (NOLOCK) on shipmentload.loadid = l2.LoadId  " +
"left join ShipmentOrdLeg with (NOLOCK) on shipmentordleg.ShipmentId=shipmentload.ShipmentId  " +
"left join ordleg with (NOLOCK) on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid  " +
"left join OrdRefNum with (NOLOCK) on ordrefnum.OrdHeaderId=ordleg.OrdHeaderId and QualifierId=9  " +
"where l2.loadid = l.loadid FOR XML PATH('') " +
") as ref45_2, " +
    "(select top 1 interstateCcId from TradingPartnerCarrier where Tradingpartnercarrier.tradingpartnerid=l.TradingPartnerIdCarrier) as car_ref, " +
    "(select top 1 tradingpartnername from TradingPartnerCarrier left join TradingPartner on TradingPartnerCarrier.TradingPartnerId=TradingPartner.TradingPartnerId where Tradingpartnercarrier.tradingpartnerid=l.TradingPartnerIdCarrier) as car_name," +
    "* from load as l " +
    "left join ShipmentLoad  on shipmentload.loadid = l.LoadId " +
    "right join ShipmentOrdLeg on shipmentordleg.ShipmentId=shipmentload.ShipmentId " +
    // hieronder geen outer apply doen omdat we alle orders van de load willen verwerken.
    //"outer apply (select top 1 * from ShipmentOrdLeg where shipmentordleg.ShipmentId=shipmentload.ShipmentId ) shipmentOrdLeg "+
    "left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid " +
    "left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId " +
    "left join OrdLine on ordheader.ordheaderid = ordline.OrdHeaderId " +
    "left join Commodity on commodity.CommodityId=ordline.CommodityId " +
    "where l.LoadNum=@loadnum " +
    // hieronder de regel als deldoc verplicht is, is niet zo.
    //" and (select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=24) is not null  "+
    "order by ordheader.OrdHeaderId desc ", SQLconn);
                    string loadnum = SQL_x2["loadnum"].ToString();
                    SQLcmd.Parameters.AddWithValue("loadnum", SQL_x2["loadnum"].ToString());
                    SQL_x = SQLcmd.ExecuteReader();
                    while (SQL_x.Read())
                    {
                        writelog("start TW calloff cancel for load " + SQL_x["loadnum"].ToString());
                        if (TW_calloff(SQL_x["loadnum"].ToString(), -1) == 0)
                        {
                            SQL_x.Close();
                            SQL_x2.Close();
                            writelog("geen loadnum in update!");
                            return;
                        }
                        writelog("update status TW cancel load " + SQL_x["loadnum"].ToString());
                        log.AppendText("Cancel TW loadnr " + SQL_x["loadnum"].ToString() + "\r\n");
                        Application.DoEvents();
                        update_order_status(SQL_x["ref45"].ToString(), 10, loadnum);  //10 = cancel calloff
                    }
                    SQL_x.Close();
                }
                SQL_x2.Close();
            }
            catch (Exception ex)
            {
                applicationlog("FTP", "error in check for TW: " + ex.Message, "");
                writelog("error in check for TW: " + ex.Message);
                log.AppendText("eror in check for TW: " + ex.Message + "\r\n");
            }
        }
        public void create_reply(string loadid, int shippldelete)   // shippldelete=2 dan delete
        {
            bool first = true;
            XMLcnt = 0;
            string shipplfn = "KC_SHIPPL01.txt";

            wait(1);
            check_databaseopen(1);
            try
            {
                applicationlog("FTP", "generate for load " + loadid, "");

                SQLcmd = new SqlCommand(
                    "select top 100 " +
                    "(select top 1 addrname  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addrname, " +
                    "(select top 1 addr1  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addr1, " +
                    "(select top 1 addr2  from Loc where loc.LocId=ordheader.LocIdOrig) as f_addr2, " +
                    "(select top 1 cityname from Loc where loc.LocId=ordheader.LocIdOrig) as f_cityname, " +
                    "(select top 1 postalcode from Loc where loc.LocId=ordheader.LocIdOrig) as f_postalcode, " +
                    "(select top 1 CountryISO2 from Loc inner join Country on country.CountryId=loc.countryid where loc.LocId=ordheader.LocIdOrig) as f_country, " +
                    "(select top 1 addrname  from Loc where loc.LocId=ordheader.LocIddest ) as t_addrname, " +
                    "(select top 1 addr1  from Loc where loc.LocId=ordheader.LocIdDest) as t_addr1, " +
                    "(select top 1 addr2  from Loc where loc.LocId=ordheader.LocIdDest) as t_addr2, " +
                    "(select top 1 cityname from Loc where loc.LocId=ordheader.LocIdDest) as t_cityname, " +
                    "(select top 1 postalcode from Loc where loc.LocId=ordheader.LocIdDest) as t_postalcode, " +
                    "(select top 1 CountryISO2 from Loc inner join Country on country.CountryId=loc.countryid where loc.LocId=ordheader.LocIdDest) as t_country, " +
                    "(select top 1 commentvalue from OrdComment where ordcomment.ordheaderid=ordheader.OrdHeaderId and qualifierid=25) as locationid, " +
                    "(select top 1 commentvalue from OrdComment where ordcomment.ordheaderid=ordheader.OrdHeaderId and qualifierid=26) as messagetype, " +
                    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=8) as ref99, " +
                    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=9) as ref45, " +
                    "(select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=24) as deldoc,  " +
                    "(select top 1 interstateCcId from TradingPartnerCarrier where Tradingpartnercarrier.tradingpartnerid=l.TradingPartnerIdCarrier) as car_ref, " +
                    "(select top 1 tradingpartnername from TradingPartnerCarrier left join TradingPartner on TradingPartnerCarrier.TradingPartnerId=TradingPartner.TradingPartnerId where Tradingpartnercarrier.tradingpartnerid=l.TradingPartnerIdCarrier) as car_name, "+
                    "* from load as l " +
                    "left join ShipmentLoad  on shipmentload.loadid = l.LoadId " +
                    //hier wel met shipmentordleg, omdat we alle orders willen hebben.
                    "right join ShipmentOrdLeg on shipmentordleg.ShipmentId=shipmentload.ShipmentId " +
                    //             "outer apply (select top 1 * from ShipmentOrdLeg where shipmentordleg.ShipmentId=shipmentload.ShipmentId ) shipmentOrdLeg " +
                    "left join ordleg on ordleg.OrdLegId = ShipmentOrdLeg.ordlegid " +
                    "left join ordheader on ordheader.ordheaderid = ordleg.OrdHeaderId " +
                    "left join OrdLine on ordheader.ordheaderid = ordline.OrdHeaderId " +
                    "left join Commodity on commodity.CommodityId=ordline.CommodityId " +
                    "left join currency on currency.CurrencyId = ordheader.CurrencyId_NetFreightChargeTot " +
                    "where l.LoadNum=@loadnum " +
                    // GEEN PO'!      "and (select top 1 refnumvalue from ordrefnum where ordrefnum.OrdHeaderId =ordheader.OrdHeaderId and qualifierid=24) is not null "+ // deldoc
                    "order by ordheader.OrdHeaderId desc ", SQLconn);
                SQLcmd.Parameters.AddWithValue("loadnum", loadid);
                SQL_x = SQLcmd.ExecuteReader();
                string fn = "-";
                while (SQL_x.Read())
                {
                    string ref45 = SQL_x["ref45"].ToString();
                    string order = SQL_x["ordnum"].ToString();
                    string messagetype = SQL_x["messagetype"].ToString().Trim();
                    linecnt = 0;
                    writelog("generate for ref:" + ref45 + " order:" + order + " ordertype:" + messagetype);
                    if (messagetype.IndexOf("PO") == 0)
                    { //mmzeu01
                        applicationlog("FTP", "Generate ZMMEU " + order + " (" + ref45 + ")", "");

                        fn = formsetup._setup[23].str + "3G_INVOICE_" + ref45 + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".XML";
                        formsetup._setup[20].integer++;
                        formsetup.writesetup_();

                        XmlWriterSettings settings = new XmlWriterSettings();
                        settings.Indent = true;
                        //settings.IndentChars = ("\t");
                        XMLWriter = XmlWriter.Create(fn, settings);

                        XMLWriter.WriteStartDocument();

                        XMLWriter.WriteComment("version 1.2 - Van der Wal/JWV");
                        getfile("KC_ZEU01.txt", "[BODY]", TEMPFN);
                        process_XMLfile(ref XMLWriter, TEMPFN);
                        XMLWriter.Close();
                        writelog("Done");
                        backupfile(fn, "MZEU_");
                        update_order_status(ref45, 0, loadid);
                    }
                    else // STO, SO
                    {
                        if (shippldelete == 2)
                            shipplfn = "KC_SHIPPLdelete01.txt";

                        applicationlog("FTP", "Generate Shippl " + order + " (" + ref45 + ")", "");
                        if (first)
                        {
                            fn = formsetup._setup[23].str + "3G_SHIPMENT_" + ref45 + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".XML";
                            formsetup._setup[20].integer++;
                            formsetup.writesetup_();

                            XmlWriterSettings settings = new XmlWriterSettings();
                            settings.Indent = true;
                            //settings.IndentChars = ("\t");
                            XMLWriter = XmlWriter.Create(fn, settings);

                            XMLWriter.WriteStartDocument();
                            XMLWriter.WriteComment("version 1.1 - Van der Wal/JWV");
                            getfile(shipplfn, "[HEADER]", TEMPFN);
                            process_XMLfile(ref XMLWriter, TEMPFN);
                            first = false;
                        }
                        getfile(shipplfn, "[BODY]", TEMPFN);
                        process_XMLfile(ref XMLWriter, TEMPFN);
                        if (shippldelete == 2)
                            update_order_status(ref45, 2, loadid); //shipple delete
                        else
                            update_order_status(ref45, 0, loadid); //shipple create (1=TW)
                    }
                }
                if (!first)
                {
                    getfile(shipplfn, "[FOOTER]", TEMPFN);
                    process_XMLfile(ref XMLWriter, TEMPFN);
                    XMLWriter.Close();
                    backupfile(fn, "SHIPPL_");
                }
                SQL_x.Close();
            }
            catch (Exception ex)
            {
                applicationlog("FTP", "Error in generate " + ex.Message, "");
                MessageBox.Show("error in create message: " + ex.Message);
                log.AppendText("error in create message: " + ex.Message + "\r\n");
            }
        }
        public void update_order_status(string ref45, int shipple_or_TW, string loadnum)
        {               // 0=Shipple, 1=TW,  2=Shipple delete, 10=TW cancel
            string ord_id = "";
            try
            {
                string[] split = ref45.Split(' ');
                // lusje
                for (int i = 0; i < split.Length; i++)
                {
                    ref45 = split[i];


                    writelog("update_order_status1. Loadnum: " + loadnum + " Ref45: " + ref45 + " shipple/tw: " + shipple_or_TW.ToString() + " ord_id: " + ord_id);
                    //                writelog("update_order_status. Ref45: " + ref45 + " shipple/tw: " + shipple_or_TW.ToString() + " profile: " + profile.ToString());
                    // toch maar 1 doen want anders krijg je bij meer orderlines ook meer opdrachten
                    SQLcmdx = new SqlCommand("select distinct(orr_ord_id) from orderRefnums where orr_RefNumValue=@orr_refnumvalue order by orr_ord_id desc,orr_id desc", SQLconnX);
                    //hieronder join met orders om BS te filteren
                    //hieronder top 1 per 3-1-2019, hoeven niet alles te updaten
                    SQLcmdx = new SqlCommand("select top 1 orr_ord_id from orderRefnums with (NOLOCK) inner join orders with (NOLOCK) on ord_id=orr_ord_id where orr_RefNumValue=@orr_refnumvalue AND ord_profile>16 order by orr_ord_id desc,orr_id desc", SQLconnX);
                    SQLcmdx.Parameters.AddWithValue("orr_refnumvalue", ref45);
                    SQL_xx = SQLcmdx.ExecuteReader();
                    while (SQL_xx.Read())
                    {
                        ord_id = SQL_xx["orr_ord_id"].ToString();
                        writelog("update_order_status2. Loadnum: " + loadnum + " Ref45: " + ref45 + " shipple/tw: " + shipple_or_TW.ToString() + " ord_id: " + ord_id);

                        if ((shipple_or_TW == 0) || (shipple_or_TW == 2)) // 2= delete
                        {
                            SQLcmdx2 = new SqlCommand("delete from ordercomments where orc_ord_id=@orc_ord_id and orc_Q_QualifierName='KC-status'", SQLconnX2);
                        }
                        else
                        {
                            SQLcmdx2 = new SqlCommand("delete from ordercomments where orc_ord_id=@orc_ord_id and orc_Q_QualifierName='TW-status'", SQLconnX2);
                        }
                        SQLcmdx2.Parameters.AddWithValue("orc_ord_id", ord_id);
                        SQLcmdx2.ExecuteNonQuery();

                        if (shipple_or_TW == 0)
                        {  // Shipple create
                            SQLcmdx2 = new SqlCommand("insert into ordercomments (orc_ord_ID, orc_Q_QualifierName, orc_Q_Description, orc_Q_QualifierType, orc_CommentValue) " +
                                                          " values             (@orc_ord_ID, 'KC-status'       , 'KC-status'      , 'Comment',           1            ) ", SQLconnX2);
                            SQLcmdx2.Parameters.AddWithValue("orc_ord_id", ord_id);
                            SQLcmdx2.ExecuteNonQuery();
                        }
                        if (shipple_or_TW == 1)  // TW
                        {
                            SQLcmdx2 = new SqlCommand("insert into ordercomments (orc_ord_ID, orc_Q_QualifierName, orc_Q_Description, orc_Q_QualifierType, orc_CommentValue) " +
                                                      " values             (@orc_ord_ID, 'TW-status'       , 'TW-status'      , 'Comment',           1            ) ", SQLconnX2);
                            SQLcmdx2.Parameters.AddWithValue("orc_ord_id", ord_id);
                            SQLcmdx2.ExecuteNonQuery();
                        }
                        if (shipple_or_TW == 10)  // TW cancel
                        {
                            SQLcmdx2 = new SqlCommand("delete from ordercomments where orc_Q_QualifierName='TW-status' and orc_ord_ID=@orc_ord_ID", SQLconnX2);
                            SQLcmdx2.Parameters.AddWithValue("orc_ord_id", ord_id);
                            SQLcmdx2.ExecuteNonQuery();
                            writelog("-->>cancel calloff check for ord_id " + ord_id.ToString());
                        }
                        SQLcmdx2 = new SqlCommand("update orders set ord_status=5 where ord_id=@ord_id", SQLconnX2);
                        SQLcmdx2.Parameters.AddWithValue("ord_id", ord_id);
                        SQLcmdx2.ExecuteNonQuery();
                    }
                    SQL_xx.Close();
                }
            }

            catch (Exception ex)
            {
                applicationlog("FTP", "Error in update message " + ex.Message, "");
                log.AppendText("error in update_order_status: " + ex.Message);
                writelog("error in update_order_status: " + ex.Message);
            }
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            formsetup._setup[12].integer = checkBox4.Checked ? 1 : 0;
            formsetup.writesetup_();
        }
        private void textBox18_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[50].str = textBox18.Text;
            formsetup.writesetup_();
        }
        private void textBox19_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[51].str = textBox19.Text;
            formsetup.writesetup_();
        }
        private void textBox20_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[52].str = textBox20.Text;
            formsetup.writesetup_();
        }
        private void textBox21_TextChanged(object sender, EventArgs e)
        {
            formsetup._setup[53].str = textBox21.Text;
            formsetup.writesetup_();
        }

        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            formsetup._setup[15].integer = checkBox6.Checked ? 1 : 0;
            formsetup.writesetup_();
        }
    }
 

    public partial class formsetup
    {
        /*-------------------------------------------------
         *      string                int             double
         * 0    database server
         * 1    database name
         * 2    database userid
         * 3    database passwd
         * 
         * 50   
         * ..
         * 59   
         * 
         * 
         *--------------------------------------------------*/

        public const int MAXSETUP = 200;
        private const string FILE_NAME = "Setup.dat";
        private const string FILE_NAME_PROFILES = "Profiles.dat";
        private const string FILE_NAME_AUTOMATIONS = "Automations.dat";

        public struct setupstruct
        {
            public string str;
            public int integer;
            public double dbl;
        }
        public struct profilesstruct
        {
            public string profilename;
            public string[] str;// = new string[MAXSETUP];
            public int[] integer;// = new int[MAXSETUP];
            public double[] dbl;//= new double[MAXSETUP];
        }
        public struct automationstruct
        {
            public string name;
            public int profile;
            public int execActions;
            public string execSQL;
            public int checkActions;
            public string checkSQL;
            public int mailAfterRun;
            public string mailadresses;
            public string sender;
            public string subject;
            public string time_from;
            public string time_to;
            public int minutes;
            public DateTime lastrun;
            public int active;
            public string mailregel1;
            public string mailregel2;
            public int dayofweek;
        }

        public static setupstruct[] _setup = new setupstruct[MAXSETUP];
        public static profilesstruct[] profiles = new profilesstruct[MAXSETUP];
        public static automationstruct[] automations = new automationstruct[MAXSETUP];

        public static void writesetup_()
        {
            FileStream fs = new FileStream(FILE_NAME, FileMode.Create);
            BinaryWriter w = new BinaryWriter(fs);      // Create the writer for data.
            for (int i = 0; i < MAXSETUP; i++)
            {
                w.Write(_setup[i].str + "");
                w.Write(_setup[i].integer);
                w.Write(_setup[i].dbl);
            }
            w.Close();
            fs.Close();
            // --- profiles--------------------------------------------
            fs = new FileStream(FILE_NAME_PROFILES, FileMode.Create);
            w = new BinaryWriter(fs);      // Create the writer for data.
            for (int p = 0; p < profiles.Length; p++)
            {
                w.Write(profiles[p].profilename + "");
                for (int i = 0; i < MAXSETUP; i++)
                {
                    w.Write(profiles[p].str[i] + "");
                    w.Write(profiles[p].integer[i]);
                    w.Write(profiles[p].dbl[i]);
                }
            }
            w.Close();
            fs.Close();

            // --- automations--------------------------------------------
            fs = new FileStream(FILE_NAME_AUTOMATIONS, FileMode.Create);
            w = new BinaryWriter(fs);      // Create the writer for data.
            for (int p = 0; p < profiles.Length; p++)
            {
                w.Write(automations[p].name + "");
                w.Write(automations[p].profile);
                w.Write(automations[p].execActions);
                w.Write(automations[p].execSQL + "");
                w.Write(automations[p].checkActions);
                w.Write(automations[p].checkSQL + "");
                w.Write(automations[p].mailAfterRun);
                w.Write(automations[p].mailadresses + "");
                w.Write(automations[p].sender + "");
                w.Write(automations[p].subject + "");
                w.Write(automations[p].time_from + "");
                w.Write(automations[p].time_to + "");
                w.Write(automations[p].minutes);
                w.Write(automations[p].lastrun.ToOADate());
                w.Write(automations[p].active);
                w.Write(automations[p].mailregel1 + "");
                w.Write(automations[p].mailregel2 + "");
                w.Write(automations[p].dayofweek);
            }
            w.Close();
            fs.Close();
        }
        public static void readsetup_old()
        {
            if (!File.Exists(FILE_NAME))
            {
                return;
            }
            if (!File.Exists(FILE_NAME_PROFILES))
            {
                return;
            }

            for (int p = 0; p < MAXSETUP; p++)
            {
                profiles[p].integer = new int[MAXSETUP];
                profiles[p].str = new string[MAXSETUP];
                profiles[p].dbl = new double[MAXSETUP];
            }

            FileStream fs = new FileStream(FILE_NAME, FileMode.Open, FileAccess.ReadWrite);
            BinaryReader w = new BinaryReader(fs);      // Create the reader for data.
            try
            {
                for (int i = 0; i < MAXSETUP; i++)                // read data 
                {
                    _setup[i].str = w.ReadString();
                    _setup[i].integer = w.ReadInt32();
                    _setup[i].dbl = w.ReadDouble();
                }
                fs.Close();
                w.Close();

                fs = new FileStream(FILE_NAME_PROFILES, FileMode.Open, FileAccess.ReadWrite);
                w = new BinaryReader(fs);      // Create the reader for data.
                for (int p = 0; p < MAXSETUP; p++)
                {
                    profiles[p].profilename = w.ReadString();
                    for (int i = 0; i < MAXSETUP; i++)                // read data 
                    {
                        profiles[p].str[i] = w.ReadString();
                        profiles[p].integer[i] = w.ReadInt32();
                        profiles[p].dbl[i] = w.ReadDouble();
                    }
                }
                fs.Close();
                w.Close();
            }

            catch
            {
            }
            fs.Close();
            w.Close();
        }

        public static void readsetup_()
        {
            for (int p = 0; p < MAXSETUP; p++)
            {
                profiles[p].integer = new int[MAXSETUP];
                profiles[p].str = new string[MAXSETUP];
                profiles[p].dbl = new double[MAXSETUP];
            }

            if (!File.Exists(FILE_NAME))
            {
                return;
            }
            FileStream fs = new FileStream(FILE_NAME, FileMode.Open, FileAccess.ReadWrite);
            BinaryReader w = new BinaryReader(fs);      // Create the reader for data.
            try
            {
                for (int i = 0; i < MAXSETUP; i++)                // read data 
                {
                    _setup[i].str = w.ReadString();
                    _setup[i].integer = w.ReadInt32();
                    _setup[i].dbl = w.ReadDouble();
                }
            }
            catch
            {
            }
            fs.Close(); w.Close();
            try
            {
                if (!File.Exists(FILE_NAME_PROFILES))
                {
                    return;
                }

                fs = new FileStream(FILE_NAME_PROFILES, FileMode.Open, FileAccess.ReadWrite);
                w = new BinaryReader(fs);      // Create the reader for data.
                for (int p = 0; p < MAXSETUP; p++)
                {
                    profiles[p].profilename = w.ReadString();
                    for (int i = 0; i < MAXSETUP; i++)                // read data 
                    {
                        profiles[p].str[i] = w.ReadString();
                        profiles[p].integer[i] = w.ReadInt32();
                        profiles[p].dbl[i] = w.ReadDouble();
                    }
                }
                fs.Close(); w.Close();
            }
            catch
            {

            } try
            {
                if (!File.Exists(FILE_NAME_AUTOMATIONS))
                {
                    return;
                }

                fs = new FileStream(FILE_NAME_AUTOMATIONS, FileMode.Open, FileAccess.ReadWrite);
                w = new BinaryReader(fs);      // Create the reader for data.
                for (int p = 0; p < MAXSETUP; p++)
                {
                    automations[p].name = w.ReadString();
                    automations[p].profile = w.ReadInt32();
                    automations[p].execActions = w.ReadInt32();
                    automations[p].execSQL = w.ReadString();
                    automations[p].checkActions = w.ReadInt32();
                    automations[p].checkSQL = w.ReadString();
                    automations[p].mailAfterRun = w.ReadInt32();
                    automations[p].mailadresses = w.ReadString();
                    automations[p].sender = w.ReadString();
                    automations[p].subject = w.ReadString();
                    automations[p].time_from = w.ReadString();
                    automations[p].time_to = w.ReadString();
                    automations[p].minutes = w.ReadInt32();
                    automations[p].lastrun = DateTime.FromOADate(w.ReadDouble());
                    automations[p].active = w.ReadInt32();
                    automations[p].mailregel1 = w.ReadString();
                    automations[p].mailregel2 = w.ReadString();
                    automations[p].dayofweek = w.ReadInt32();
                }
                fs.Close(); w.Close();
            }
            catch
            {
            }
            fs.Close();
            w.Close();
        }
        public string recode(string ipstr)
        {
            string resstr = "";
            byte[] by = System.Text.Encoding.ASCII.GetBytes(ipstr);
            for (int i = 0; i < ipstr.Length; i++)
            {
                try
                {
                    resstr += Convert.ToChar(by[i] ^ 0x1C);
                }
                catch (Exception ex)
                {

                    Form1.writelog("error recode: " + ex.Message);
                }
            }
            return (resstr);
        }
        public void readsetup_coded()
        {
            if (!File.Exists(FILE_NAME))
            {
                return;
            }
            FileStream fs = new FileStream(FILE_NAME, FileMode.Open, FileAccess.ReadWrite);
            BinaryReader w = new BinaryReader(fs);      // Create the reader for data.
            try
            {
                for (int i = 0; i < MAXSETUP; i++)                // read data 
                {
                    _setup[i].str = recode(w.ReadString());
                    _setup[i].integer = w.ReadInt32();
                    _setup[i].dbl = w.ReadDouble();
                }
            }
            catch (Exception ex)
            {
                Form1.writelog("error readsetup_coded: " + ex.Message);
            }
            fs.Close();
            w.Close();
        }
        public void writesetup_coded()
        {
            FileStream fs = new FileStream(FILE_NAME, FileMode.Create);
            BinaryWriter w = new BinaryWriter(fs);      // Create the writer for data.
            for (int i = 0; i < MAXSETUP; i++)
            {
                w.Write(recode(_setup[i].str + ""));
                w.Write(_setup[i].integer);
                w.Write(_setup[i].dbl);
            }
            w.Close();
            fs.Close();
        }
    }

//    namespace Transwide.Client
//    {
        public class Client
        {
            private RestClient _client;

            public Client()
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;

                _client = new RestClient("https://comm2.transwide.com/vanderWal")
                {
                    Authenticator = new HttpBasicAuthenticator("vanderWal", "vxeYiez3ke2t"),
                };
            }

            public void Post()
            {
                var request = new RestRequest("/incoming/", Method.POST);

                // Json to post.
                var jsonToSend = JsonConvert.SerializeObject(new object());

                request.AddParameter("application/json; charset=utf-8", jsonToSend, ParameterType.RequestBody);
                request.RequestFormat = DataFormat.Json;

                try
                {
                    _client.ExecuteAsync(request, response =>
                    {
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            // OK
                        }
                        else
                        {
                            // NOK
                        }
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine("Something went wrong: {e}");
                }
            }

        public List<string> GetOrderGuids()
        {
            try
            {
                var request = new RestRequest("/outgoing/");
                var response = _client.Get(request);

                /*
                var result = Regex.Matches(response.Content,
                    @"{[0-9a-fA-F]{8}-[0-9a-fA-F]{8}-[0-9a-fA-F]{16}-[0-9a-fA-F]{16}}",
                    RegexOptions.IgnoreCase);

                Console.WriteLine($"Download successful. Found {result.Count} orders.");
                return new List<string>(result.Select(o => o.Value));
                */
                List<string> items = new List<string>();

                foreach (Match match in Regex.Matches(response.Content, @"{[0-9a-fA-F]{8}-[0-9a-fA-F]{8}-[0-9a-fA-F]{16}-[0-9a-fA-F]{16}}", RegexOptions.IgnoreCase))
                {
                    items.Add(match.Groups[0].Value);
                }
                return (items);
            }
            catch (Exception e)
            {
                Console.WriteLine("Something went wrong: {e}");
            }

            return null;
        }

        public int teller = 0;
        public void DownloadOrders(List<string> orderGuids)
        {
            StreamWriter sw = new StreamWriter("TW_history.txt");
            sw.AutoFlush = true;
            /*foreach (var orderGuid in orderGuids)
                          {
                                var request = new RestRequest($"/outgoing/{Uri.EscapeDataString(orderGuid)}");
                                _client.DownloadData(request).SaveAs("TW.TXT");
                                sw.WriteLine(File.ReadAllText("tw.txt"));
                                File.Delete("tw.txt");
                                // Delete(orderGuid);
                            } */
                int i = 1;
            while (i<orderGuids.Count-100) 
            {
                var orderGuid = orderGuids[i];
                var request = new RestRequest($"/outgoing/{Uri.EscapeDataString(orderGuid)}");

                _client.DownloadData(request).SaveAs("TW.TXT");

                Form1.writelog(File.ReadAllText("tw.txt"),"TW");
                File.Delete("tw.txt");
                Delete(orderGuid);
                i++;
                i += 99;
            }
            sw.Close();
        }
        public void DownloadOrders_100(List<string> orderGuids)
        {
            /*foreach (var orderGuid in orderGuids)
                          {
                                var request = new RestRequest($"/outgoing/{Uri.EscapeDataString(orderGuid)}");
                                _client.DownloadData(request).SaveAs("TW.TXT");
                                sw.WriteLine(File.ReadAllText("tw.txt"));
                                File.Delete("tw.txt");
                                // Delete(orderGuid);
                            } */
            int i = 0;
            if (formsetup._setup[31].str.Trim().Length > 0)
            {
                Directory.CreateDirectory(formsetup._setup[31].str);
            }
            while (i < orderGuids.Count)
            {
                var orderGuid = orderGuids[i];
                var request = new RestRequest($"/outgoing/{Uri.EscapeDataString(orderGuid)}");

                string opfn = formsetup._setup[31].str + DateTime.Now.ToString("ddMMyyyy-HHmmss-") + i.ToString("00") + ".txt";

                _client.DownloadData(request).SaveAs(opfn);

                Form1.writelog(File.ReadAllText(opfn), "TW");
                Delete(orderGuid);

                i++;
                if (i == 1000)
                    return;
            }
        }


        public void Delete(string orderGuid)
            {
                try
                {
                    var request = new RestRequest($"/outgoing/{Uri.EscapeDataString(orderGuid)}", Method.DELETE);
                    _client.Delete(request);

                    Console.WriteLine("Successfuly removed something!");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Something went wrong: {e}");
                }
            }
        }



//    }    
}
