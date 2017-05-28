using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.IO;
using System.Globalization;
using ZedGraph;

namespace vs_curs_FirstClient
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        #region data
        const int T = 1;//интервал прогнозирования
        int TM;
        double[,] S;//Модел сигнала, массив отсчетов
        double[] E ;//Случайная величина
        double[] Y;
        double[] Um = new double[5];//амплитуный спектр
        double[] Am = new double[5];//амплитуда
        double[] Ph = new double[5];//фаза
        int[] f = new int[5];//частоты по гармоникам
        const double SQE = 0.01;//Среднеквадратичное значение помех
        const double Mu = 0.04;
        int Fd=50;//частота дискретизации
        double Fs = 0.5;//Частота среза фильтров нижних частот
        double Td,Ts;//Период дескритизации
        double L1, L2, L3, C;//параметры нестационарности

        string[] WW = new string[11];
        string[] PP = new string[10];
        string[] QQ = { "0", "200", "400", "600", "800", "1000", "1200", "1400", "1600", "1800", "2000" };
        string pathname = "x1.txt";
        #endregion

        private void generate()
        {
            byte[] StoFile;
            NumberFormatInfo formatInfo = (NumberFormatInfo)CultureInfo.GetCultureInfo("en-US").NumberFormat.Clone();
            formatInfo.NumberDecimalSeparator = ".";
            TM = Convert.ToInt32(TMBox.Text);
            Y = new double[TM];
            S = new double[4, TM];
            Fd = Convert.ToInt32(FdBox.Text);
            Fs = Convert.ToDouble(FsBox.Text, formatInfo);
            L1 = Convert.ToDouble(L1Box.Text, formatInfo);
            L2 = Convert.ToDouble(L2Box.Text, formatInfo);
            L3 = Convert.ToDouble(L3Box.Text, formatInfo);
            C = Convert.ToDouble(CBox.Text, formatInfo);
            Random rnd = new Random();
            Td = 1 / (double)Fd;
            Ts = 1 / (2 * Math.PI * Fs);
            FileStream fs = new FileStream(pathname,
                                        FileMode.Create,
                                        FileAccess.Write,
                                        FileShare.None,
                                        4096,
                                        FileOptions.SequentialScan);
            //Коэф. цифрового фиьтра
            double A = Td / (Td + Ts);
            double B = Ts / (Td + Ts);

            for (int k = 1; k < TM; k++)
            {
                Y[k] = A * Math.Pow(-1, rnd.Next(-1,1)) * rnd.NextDouble() + B * Y[k - 1];
                for (int j = 0; j < 4; j++)
                {
                    S[j, k] = Km(j, k) * (A * Y[k] + B * S[j, k - 1]);
                    StoFile = Encoding.ASCII.GetBytes(S[j,k].ToString()+"\t");
                    fs.Write(StoFile, 0, StoFile.Length);
                }
                StoFile = Encoding.ASCII.GetBytes("\n");
                fs.Write(StoFile, 0, StoFile.Length);
            }
            SetLog("Generation succesfull" + Environment.NewLine);
            fs.Close();

            CreateGraph(zedGraphControl1, S, 0);
            CreateGraph(zedGraphControl2, S, 1);
            CreateGraph(zedGraphControl3, S, 2);
            CreateGraph(zedGraphControl4, S, 3);
            SetSize();
        }

        private double Km(int version, int index)
        {
            switch (version)
            {
                case 0:
                    return 1-C;
                case 1:
                    return 1 - L1 * index / TM;
                case 2:
                    return 1 + L2 * (index / TM) * (index / TM);
                case 3:
                    return L3 * Math.Exp(index / TM);
                default:
                    return 0;
            }
        }

        //Копирование потоков бинарного типа передачи
        private static long CopyStream(Stream input, Stream output, int bufferSize, long max)
        {
            byte[] buffer = new byte[bufferSize];
            int count = 0;
            long total = 0;
            
            Form1 fr = new Form1();
            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, count);
                total += count;
            }

            return total;
        }
        public void SetLog(string str)
        {
            if (textBox2.InvokeRequired)
                textBox2.Invoke(new Action<string>((s) => textBox2.Text += s), str);
            else textBox2.Text += str;
        }

        private void ClientTcp()
        {
            
            //Соединяемся с удаленным устройством
            try
            {
                //Устанавливаем удаленную конечную точку для сокета
                string[] addr = maskedTextBox1.Text.Split(',');
                string HostName = Convert.ToInt32(addr[0]).ToString() + "." +
                                    Convert.ToInt32(addr[1]).ToString() + "." +
                                    Convert.ToInt32(addr[2]).ToString() + "." +
                                    Convert.ToInt32(addr[3]).ToString();
                
                IPHostEntry ipHost = Dns.GetHostEntry(HostName);
                IPAddress ipAddr = ipHost.AddressList[0];
                Int32 port = Convert.ToInt32(textBox1.Text);
                //IPEndPoint ipEndPoint = new IPEndPoint(ipAddr, Convert.ToInt32(textBox1.Text));//номер порта
                TcpClient client = new TcpClient(HostName,port);
                
                //Соединяем сокет с удаленной конечной точкой
                SetLog("Connection..."+ipAddr.ToString()+Environment.NewLine);
                NetworkStream stream = client.GetStream();
                //Декодинг приветствия
                string hello="$TakePlease$";
                byte[] msg = Encoding.ASCII.GetBytes(hello);
                //отправляем данные через сокет
                stream.Write(msg,0,msg.Length);
                SetLog(hello + Environment.NewLine);
                
                //Получаем ответ от удаленного устройства
                byte[] bytes = new byte[1024];
                int RecByte = stream.Read(bytes,0,bytes.Length);
                SetLog("Server reply : "+ Encoding.ASCII.GetString(bytes, 0, RecByte)+Environment.NewLine);



                //Проверка установки соединения
                if (Encoding.ASCII.GetString(bytes, 0, RecByte) == "$Ok$")
                {
                    //Декодинг сообщенияя
                    //byte[] data = Encoding.ASCII.GetBytes(_answer);
                    //отправляем данные через сокет
                    //stream.Write(data, 0, data.Length);
                    long col = 0;
                    using (FileStream fs = new FileStream(pathname, FileMode.Open, FileAccess.Read))
                    {
                        col = CopyStream(fs, stream, 4096, fs.Length);
                        fs.Close();
                    SetLog("File sending " + col.ToString()+Environment.NewLine);
                    
                    }
                    SetLog("Server says : " + Encoding.ASCII.GetString(bytes, 0, RecByte) + Environment.NewLine);
                }
                //Освобождаем сокет
                stream.Close();
                client.Close();

            }
            catch (Exception e)
            {
                SetLog(e.ToString());
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (S != null)
            {
                Thread Client = new Thread(new ThreadStart(ClientTcp));
                Client.Start(); // Вызываем поток
                //sending(answer);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            generate();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            SetSize();
        }

        private void SetSize()
        {
           zedGraphControl1.Location = new Point( 10, 10 );
           zedGraphControl1.Size = new Size(tabPage1.Width - 20,
                                   tabPage1.Height - 20);
           zedGraphControl2.Location = new Point(10, 10);
           zedGraphControl2.Size = new Size(tabPage2.Width - 20,
                                   tabPage2.Height - 20);
           zedGraphControl3.Location = new Point(10, 10);
           zedGraphControl3.Size = new Size(tabPage3.Width - 20,
                                   tabPage3.Height - 20);
           zedGraphControl4.Location = new Point(10, 10);
           zedGraphControl4.Size = new Size(tabPage4.Width - 20,
                                   tabPage4.Height - 20);
        }

        private void CreateGraph( ZedGraphControl zgc, double[,] func, int index )
        {
           // get a reference to the GraphPane    
            GraphPane myPane = zgc.GraphPane;

            // Очистим список кривых на тот случай, если до этого сигналы уже были нарисованы
            myPane.CurveList.Clear();

           // Set the Titles    
           myPane.Title.Text = "Graphic";
           myPane.XAxis.Title.Text = "X";
           myPane.YAxis.Title.Text = "Y";

           // Make up some data arrays based on the Sine function    
            double x, y;
           PointPairList list1 = new PointPairList();
           //PointPairList list2 = new PointPairList();
           for ( int i = 0; i < func.GetLength(1); i++ )
           {
              x = (double)i ;
              y = func[index,i]; 
               list1.Add( x, y );
           }

           // Generate a red curve with diamond    // symbols, and "Porsche" in the legend    
            LineItem myCurve = myPane.AddCurve( "S",
                 list1, Color.Red, SymbolType.None );
            int xMax = Convert.ToInt32(UppBox.Text),
                xMin = Convert.ToInt32(LowBox.Text);
            myPane.XAxis.Scale.Max = xMax+20;
            myPane.XAxis.Scale.Min = xMin-20;
            // Вызываем метод AxisChange (), чтобы обновить данные об осях.
            // В противном случае на рисунке будет показана только часть графика,
            // которая умещается в интервалы по осям, установленные по умолчанию
            zgc.AxisChange();

            // Обновляем график
            zgc.Invalidate();
        }
    }
}

