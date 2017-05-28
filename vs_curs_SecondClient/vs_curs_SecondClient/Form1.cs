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

namespace vs_curs_SecondClient
{
    public partial class Form1 : Form
    {

        #region data
        int T = 1;//интервал прогнозирования
        int _M;//Длительность интервала (количество отсчетов)
        double[,] S;//Модел сигнала, массив отсчетов
        double[] Um = new double[5];//амплитуный спектр
        double[] Am = new double[5];//амплитуда
        double[] Ph = new double[5];//фаза
        int[] f = new int[5];//частоты по гармоникам
        double[,] _W;
        double[,] P_LMS;//Прогнозируемый график
        double[,] P_Vin;//Прогнозируемый график
        double[,] Error_LMS;//Ошибка
        double[,] Error_Vin;//Ошибка
        const double SQE = 0.01;//Среднеквадратичное значение помех
        double Mu;
        int _N;//Порядок фильтра
        int _L;//Длительность интервала проверки качества
        int _Km;
        string pathname = "x2.txt";
        #endregion

        public Form1()
        {
            InitializeComponent();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            NumberFormatInfo formatInfo = (NumberFormatInfo)CultureInfo.GetCultureInfo("en-US").NumberFormat.Clone();
            formatInfo.NumberDecimalSeparator = ",";
            int count = 0;
            string line;
            StreamReader file =
                new StreamReader(pathname);
            SetLog("Begin reading" + Environment.NewLine);
            SetProgressBar(0);
            while ((line = file.ReadLine()) != null)
            { 
                count++;
                if (count == (Int32)numericUpDown1.Value)
                    break;
            }
            S = new double[4, count];
            int maxprogres = count;
            file.BaseStream.Position = 0;
            count = 0;
            while ((line = file.ReadLine()) != null)
            {
                string[] separate = line.Split('\t');
                
                S[0, count] = Convert.ToDouble(separate[0], formatInfo);
                try
                {
                    S[1, count] = Convert.ToDouble(separate[1], formatInfo);
                }
                catch (FormatException exp)
                {
                    S[1, count] = 0;
                }
                S[2, count] = Convert.ToDouble(separate[2], formatInfo);
                S[3, count] = Convert.ToDouble(separate[3], formatInfo);
                SetProgressBar((int)(100 * count / maxprogres));
                count++;
                if (count == S.GetLength(1))
                    break;
                if (count % 50000 == 0)
                {
                    SetLog("Counts: " + count.ToString() + Environment.NewLine);
                    textBox2.Refresh();
                }

            }
            file.Close();
            SetProgressBar(100);
            _Km = (Int32)numericUpDown1.Value-1;
            Mu = (double)numericUpDown2.Value;
            _N = (Int32)numericUpDown3.Value;
            _M = (Int32)numericUpDown4.Value;
            T = (Int32)numericUpDown5.Value;
            _L=_M;

            SetLog("Convertation succesfull" + Environment.NewLine);
            SetLog("Counts: " + count.ToString() + Environment.NewLine);
            textBox2.Refresh();

            if (checkBox1.Checked == true)
            {
                VinnerCalculate();
            }
            if (checkBox2.Checked == true)
            {
                LMSCalculate();
            }
        }

        private void VinnerCalculate()
        {

            int N = _N;
            int M = _M;
            P_Vin = new double[4, M + 1];
            Error_Vin = new double[4, M + 1];
            int count = 0;
            SetProgressBar(count);

            for (int index = 0; index < 4; index++)
            {
            double[] fR = new double[N + 1];
            double[] Yk = new double[M + 1];
            double[] d = new double[M + 1];
            
            double koef = Convert.ToDouble(1 / (double)(M - N - T));
                SetLog(Environment.NewLine + "VinnerCalculate begining: " + (index+1).ToString() + Environment.NewLine);
                //Оценки элементов корреляционной матрицы сигнала
                SetLog("Оценки элементов корреляционной матрицы сигнала" + Environment.NewLine);
                for (int j = 0; j <= N; j++)
                {
                    for (int k = N; k <= M - T; k++)
                    {
                        fR[j] += S[index, k] * S[index, k - j];
                    }
                    fR[j] = fR[j] * koef;
                }

                //Кореляционная матрица сигнла помехи
                SetLog("Кореляционная матрица сигнла помехи" + Environment.NewLine);
                double[,] Rx = new double[N + 1, N + 1];
                for (int i = 0; i <= N; i++)
                {
                    for (int j = i; j <= N; j++)
                    {
                        Rx[j, i] = fR[j - i];
                        Rx[i, j] = fR[j - i];
                    }
                }
                SetProgressBar(count += 5);


                //Вектор взаимных корреляций между отсчетами образца
                SetLog("Вектор взаимных корреляций между отсчетами образца" + Environment.NewLine);
                double[,] P = new double[1, N + 1];
                for (int j = 0; j <= N; j++)
                {
                    for (int k = N; k < M - T; k++)
                    {
                        P[0, j] += S[index, k + T] * S[index, k - j];
                    }
                    P[0, j] = P[0, j] * koef;
                }
                
                //Транспонированная матрица
                SetLog("Транспонированная матрица" + Environment.NewLine);
                double[,] Ptrans = new double[N + 1, 1];
                    for (int j = 0; j <= N; j++)
                    {
                        Ptrans[j, 0] = P[0, j];
                    }
                SetProgressBar(count += 5);

                //Обратная матрица
                SetLog("Обратная матрица");
                double[,] Rxrev = new double[N + 1, N + 1];
                Rxrev = ObrMatrix(Rx,1);

                //Параметры оптимально прогнозирующего фильтра
                SetLog("Параметры оптимально прогнозирующего фильтра" + Environment.NewLine);
                double[,] W = new double[N + 1, N + 1];
                W = Multiplication(Rxrev, Ptrans); 

                //Прогнозируемое значение
                SetLog("Прогнозируемое значение" + Environment.NewLine);
                for (int k = N; k < M; k++)
                {
                    for (int j = 0; j <= N; j++)
                    {
                        Yk[k] += S[index, k - j] * W[j, 0];
                    }
                }
                SetProgressBar(count += 5);

                for (int i = 0; i < M; i++)
                {
                    P_Vin[index, i] = Yk[i];
                }

                //Ошибка прогнозирования в k точке
                SetLog("Ошибка прогнозирования в k точке" + Environment.NewLine);
                for (int k = N; k < M; k++)
                {
                    d[k] = S[index, k] - Yk[k - T];
                }
                
                for (int i = 0; i < M; i++)
                {
                    Error_Vin[index, i] = d[i];
                }
                SetProgressBar(count += 5);

                //СКЗ ошибки прогнозирования
                SetLog("СКЗ ошибки прогнозирования" + Environment.NewLine);
                double SQd = 0;
                double preSQd = 0;
                int L = _L;
                for (int k = 1; k < M; k++)
                {
                    preSQd += d[k] * d[k];
                }
                SQd = Math.Sqrt((1 / L) * preSQd);

                SetProgressBar(count += 5);
            }

            SetProgressBar(100);

            //Графики
             CreateGraph(Vinner_S0_ZGC, S, P_Vin, 0, "Prognoz s0");
             CreateGraph(Vinner_S1_ZGC, S, P_Vin, 1, "Prognoz s1");
             CreateGraph(Vinner_S2_ZGC, S, P_Vin, 2, "Prognoz s2");
             CreateGraph(Vinner_S3_ZGC, S, P_Vin, 3, "Prognoz s3");
             SetProgressBar(0);

        }

        //Обратная матрица
        public double[,] ObrMatrix(double[,] A, int cn)
        {

            double[,] Mat;
            Mat = MulMatxix(A, cn);

            int info = 0;
            alglib.matinv.matinvreport port = new alglib.matinv.matinvreport();
            alglib.matinv.rmatrixinverse(ref Mat, Mat.GetLength(1), ref info, port);

            Mat = DivMatxix(Mat, cn);

            return Mat;
        }

        //Для вычисления W
        public double[,] Multiplication(double[,] a, double[,] b)
        {
            if (a.GetLength(1) != b.GetLength(0)) throw new Exception("Матрицы нельзя перемножить");
            double[,] r = new double[a.GetLength(0), b.GetLength(1)];
            for (int i = 0; i < a.GetLength(0); i++)
            {
                for (int j = 0; j < b.GetLength(1); j++)
                {
                    for (int k = 0; k < b.GetLength(0); k++)
                    {
                        r[i, j] += a[i, k] * b[k, j];
                    }
                }
            }
            return r;
        }

        //Для обратной матрицы
        public double[,] MulMatxix(double[,] A, double kf)
        {
            double[,] resMat = new double[A.GetLength(0), A.GetLength(1)];

            for (int i = 0; i < A.GetLength(0); i++)
                for (int j = 0; j < A.GetLength(1); j++)
                {
                    resMat[i, j] = A[i, j] * kf;
                }
            return resMat;
        }

        //Для обратной матрицы
        public double[,] DivMatxix(double[,] A, double kf)
        {
            double[,] resMat = new double[A.GetLength(0), A.GetLength(1)];

            for (int i = 0; i < A.GetLength(0); i++)
                for (int j = 0; j < A.GetLength(1); j++)
                {
                    resMat[i, j] = A[i, j] / kf;
                }
            return resMat;
        }

        /*
        public double[,] ObrMatrix(double[,] Rx)
        {
            double[,] E;
            double temp;
            E= new double[Rx.GetLength(0),Rx.GetLength(1)];
            for (int i = 0; i < E.GetLength(0); i++)
            {
                for (int j = 0; j < E.GetLength(1); j++)
                {
                    if (i == j)
                        E[i, j] = 1;
                    else
                        E[i, j] = 0;
                }
            }

            for (int i = 0; i < Rx.GetLength(0); i++)
            {
                for (int j = 0; j < Rx.GetLength(1); j++)
                {
                    Rx[i, j] *= 10000;
                }
            }

                //Находим обратную матрицу.
                //Прямой ход метода Гаусса-Жордана. Получаем нули под главной диагональю
                for (int k = 0; k < _N; k++)
                {
                    temp = Rx[k, k];
                    for (int j = 0; j < _N; j++)
                    {
                        E[k, j] /= temp;
                        Rx[k, j] /= temp;
                    }
                    for (int i = 1 + k; i < _N; i++)
                    {
                        temp = Rx[i, k];
                        for (int j = 0; j < _N; j++)
                        {
                            E[i, j] -= E[k, j] * temp;
                            Rx[i, j] -= Rx[k, j] * temp;
                        }
                    }
                }
            //Обратный ход метода Гаусса-Жордана. Получаем нули над главной диагональю
            for (int k =_N - 1; k > 0; k--)
            {
                for (int i = k - 1; i >= 0; i--)
                {
                    temp = Rx[i,k];

                    for (int j = 0; j < _N; j++)
                    {
                        Rx[i,j] -= Rx[k,j] * temp;
                        E[i,j] -= E[k,j] * temp;
                    }
                }
            }
            //Присваеваем элементы матрицы. чтобы Rx стала обратной, а не единичной.
            for (int i = 0; i < _N; i++)
                for (int j = 0; j < _N; j++)
                    Rx[i,j] = E[i,j];

            for (int i = 0; i < Rx.GetLength(0); i++)
            {
                for (int j = 0; j < Rx.GetLength(1); j++)
                {
                   Rx[i, j] /= 10;
                }
            }

            return Rx;
        }
        */

        //int info = 0;
        ////alglib.matinv.matinvreport port = new alglib.matinv.matinvreport();
        ////alglib.matinv.rmatrixinverse(ref Mat, Mat.GetLength(1), ref info, port);
        //alglib.matinvreport rep;
        //alglib.rmatrixinverse(ref Mat, Mat.GetLength(0), out info, out rep);

        private void LMSCalculate()
        {
            P_LMS = new double[4, _Km];
            Error_LMS = new double[4, _Km];
            _W = new double[_Km + 1, _N];
            int progres = 0;
            SetProgressBar(0);
            for (int i = 0; i < 4; i++)
            {
                double[] buffer = new double[_Km];
                double[] ERRbuffer = new double[_Km];
                buffer = CalcLMS(S, i, _N, _Km, Mu, out _W, out ERRbuffer);
                for (int j = 0; j < P_LMS.GetLength(1); j++)
                {
                    P_LMS[i, j] = buffer[j];
                    Error_LMS[i, j] = ERRbuffer[j];
                    progres++;
                    if((progres%10)==0)
                        SetProgressBar((int)(100 * progres / P_LMS.Length));
                }
            }
            SetProgressBar(100);
            statusStrip1.Refresh();
            //Графики
            CreateGraph(LMS_S0_ZGC, S, P_LMS, 0, "Prognoz s0");
            CreateGraph(LMS_S1_ZGC, S, P_LMS, 1, "Prognoz s1");
            CreateGraph(LMS_S2_ZGC, S, P_LMS, 2, "Prognoz s2");
            CreateGraph(LMS_S3_ZGC, S, P_LMS, 3, "Prognoz s3");
            SetProgressBar(0);
            statusStrip1.Refresh();
        }

        public void SetProgressBar(int c)
        {
            this.toolStripProgressBar1.ProgressBar.Value = c;
            this.toolStripProgressBar1.ProgressBar.Refresh();
        }

        public void SetStatusLabel(string s)
        {
            this.toolStripStatusLabel1.Text = s;
            statusStrip1.Refresh();
        }

        private double[] CalcLMS(double[,] X, int num_Graph, int N, int Km, double mu, out double[,] W, out double[] Er)
        {
            W = new double[Km + 1, N];
            Er = new double[Km];
            double[] Y = new double[Km];
            double E;

            for (int n = N; n < Km; n++)
            {
                for (int j = 0; j < N; j++)
                {
                    Y[n] += W[n-1, j] * X[num_Graph, n - j];
                }

                E = X[num_Graph, n] - Y[n-T];

                for (int j = 0; j < N; j++)
                {
                    W[n, j] = W[n-1, j] + mu * E * X[num_Graph, n - j];
                }

            }
            for (int k = 0; k < Km; k++)
            {
                Er[k] = Y[k] - X[num_Graph, k];
            }
                return Y;
        }


        //long CopyStream(Stream input, Stream output, int bufferSize)
        //Копирование потоков бинарного типа передачи
        //1ый параметр - входной поток
        //2ый параметр - выходной поток
        //3ый параметр - размер байтового массива для буфера

        private static long CopyStream(Stream input, Stream output, int bufferSize)
        {
            byte[] buffer = new byte[bufferSize];
            int count = 0;
            long total = 0;

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
            textBox2.Refresh();
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
                TcpClient client = new TcpClient(HostName, port);

                //Соединяем сокет с удаленной конечной точкой
                SetLog("Connection..." + ipAddr.ToString() + Environment.NewLine);
                NetworkStream stream = client.GetStream();
                //Декодинг приветствия
                string hello = "$GivePlease$";
                byte[] msg = Encoding.ASCII.GetBytes(hello);
                //отправляем данные через сокет
                stream.Write(msg, 0, msg.Length);
                SetLog(hello + Environment.NewLine);

                //Получаем ответ от удаленного устройства
                byte[] bytes = new byte[1024];
                int RecByte = stream.Read(bytes, 0, bytes.Length);
                SetLog("Server reply : " + Encoding.ASCII.GetString(bytes, 0, RecByte) + Environment.NewLine);



                //Проверка установки соединения
                if (Encoding.ASCII.GetString(bytes, 0, RecByte) == "$Ok$")
                {
                    //Декодинг сообщенияя
                    //byte[] data = Encoding.ASCII.GetBytes(_answer);
                    //отправляем данные через сокет
                    //stream.Write(data, 0, data.Length);
                    long col = 0;
                    FileStream fs = new FileStream(pathname,
                                        FileMode.Create,
                                        FileAccess.Write,
                                        FileShare.None,
                                        4096,
                                        FileOptions.SequentialScan);
                    col=CopyStream(stream, fs, 4096);
                    fs.Close();
                    SetLog("File received " + col.ToString()+Environment.NewLine);
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
                Thread Client = new Thread(new ThreadStart(ClientTcp));
                Client.Start(); // Вызываем поток
                //sending(answer);
        }

        private void SetSize()
        {
            Vinner_S0_ZGC.Location = new Point(10, 10);
            Vinner_S0_ZGC.Size = new Size(Vinner_S0.Width - 20,
                                    Vinner_S0.Height - 20);
            Vinner_S1_ZGC.Location = new Point(10, 10);
            Vinner_S1_ZGC.Size = new Size(Vinner_S1.Width - 20,
                                    Vinner_S1.Height - 20);
            Vinner_S2_ZGC.Location = new Point(10, 10);
            Vinner_S2_ZGC.Size = new Size(Vinner_S2.Width - 20,
                                    Vinner_S2.Height - 20);
            Vinner_S3_ZGC.Location = new Point(10, 10);
            Vinner_S3_ZGC.Size = new Size(Vinner_S3.Width - 20,
                                    Vinner_S3.Height - 20);
            
            LMS_S0_ZGC.Location = new Point(10, 10);
            LMS_S0_ZGC.Size = new Size(LMS_S0.Width - 20,
                                    LMS_S0.Height - 20);
            LMS_S1_ZGC.Location = new Point(10, 10);
            LMS_S1_ZGC.Size = new Size(LMS_S1.Width - 20,
                                    LMS_S1.Height - 20);
            LMS_S2_ZGC.Location = new Point(10, 10);
            LMS_S2_ZGC.Size = new Size(LMS_S2.Width - 20,
                                    LMS_S2.Height - 20);
            LMS_S3_ZGC.Location = new Point(10, 10);
            LMS_S3_ZGC.Size = new Size(LMS_S3.Width - 20,
                                    LMS_S3.Height - 20);
        }

        private void CreateGraph(ZedGraphControl zgc, double[,] func, int index, string name)
        {
            // get a reference to the GraphPane    
            GraphPane myPane = zgc.GraphPane;

            // Очистим список кривых на тот случай, если до этого сигналы уже были нарисованы
            myPane.CurveList.Clear();

            // Set the Titles    
            myPane.Title.Text = name;
            myPane.XAxis.Title.Text = "X";
            myPane.YAxis.Title.Text = "Y";

            // Make up some data arrays based on the Sine function    
            double x, y1;
            PointPairList list1 = new PointPairList();
            for (int i = 0; i < func.GetLength(1); i++)
            {
                x = (double)i;
                y1 = func[index, i];
                list1.Add(x, y1);

            }

            // Generate a red curve with diamond    // symbols, and "Porsche" in the legend    
            LineItem myCurve1 = myPane.AddCurve("Err",
                 list1, Color.Red, SymbolType.None);

            int xMax = func.GetLength(1),
                xMin = 0;
            myPane.XAxis.Scale.Max = xMax + 20;
            myPane.XAxis.Scale.Min = xMin - 20;
            // Вызываем метод AxisChange (), чтобы обновить данные об осях.
            // В противном случае на рисунке будет показана только часть графика,
            // которая умещается в интервалы по осям, установленные по умолчанию
            zgc.AxisChange();

            // Обновляем график
            zgc.Invalidate();
        }
        
        private void CreateGraph(ZedGraphControl zgc, double[,] func, double[,] prognoz, int index, string name)
        {
            // get a reference to the GraphPane    
            GraphPane myPane = zgc.GraphPane;

            // Очистим список кривых на тот случай, если до этого сигналы уже были нарисованы
            myPane.CurveList.Clear();

            // Set the Titles    
            myPane.Title.Text = name;
            myPane.XAxis.Title.Text = "X";
            myPane.YAxis.Title.Text = "Y";

            // Make up some data arrays based on the Sine function    
            double x, y1,y2;
            PointPairList list1 = new PointPairList();
            PointPairList list2 = new PointPairList();
            for (int i = 0; i < prognoz.GetLength(1); i++)
            {
                x = (double)i;
                try
                {
                    y1 = func[index, i];
                    list1.Add(x, y1);
                }
                catch (IndexOutOfRangeException exp)
                {

                }
                
                y2 = prognoz[index, i];
                list2.Add(x, y2);

            }

            // Generate a red curve with diamond    // symbols, and "Porsche" in the legend    
            LineItem myCurve1 = myPane.AddCurve("S",
                 list1, Color.Red, SymbolType.None);
            LineItem myCurve2 = myPane.AddCurve("P",
                 list2, Color.Blue, SymbolType.Diamond);

            int xMax = prognoz.GetLength(1),
                xMin = 0;
            myPane.XAxis.Scale.Max = xMax + 20;
            myPane.XAxis.Scale.Min = xMin - 20;
            // Вызываем метод AxisChange (), чтобы обновить данные об осях.
            // В противном случае на рисунке будет показана только часть графика,
            // которая умещается в интервалы по осям, установленные по умолчанию
            zgc.AxisChange();

            // Обновляем график
            zgc.Invalidate();
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox3.Checked == true)
                groupBox2.Enabled = true;
            else
            {
                groupBox2.Enabled = false;
                if (checkBox2.Checked == true)
                {
                        //Графики
                        CreateGraph(LMS_S0_ZGC, S, P_LMS, 0, "Prognoz s0");
                        CreateGraph(LMS_S1_ZGC, S, P_LMS, 1, "Prognoz s1");
                        CreateGraph(LMS_S2_ZGC, S, P_LMS, 2, "Prognoz s2");
                        CreateGraph(LMS_S3_ZGC, S, P_LMS, 3, "Prognoz s3");
                }

                if (checkBox1.Checked == true)
                {
                        //Графики
                        CreateGraph(Vinner_S0_ZGC, S, P_Vin, 0, "Prognoz s0");
                        CreateGraph(Vinner_S1_ZGC, S, P_Vin, 1, "Prognoz s1");
                        CreateGraph(Vinner_S2_ZGC, S, P_Vin, 2, "Prognoz s2");
                        CreateGraph(Vinner_S3_ZGC, S, P_Vin, 3, "Prognoz s3");
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (radioButton1.Checked == true)
            {
                if (checkBox1.Checked == true)
                {
                    if (Error_Vin != null)
                    {
                        //Ошибки
                        CreateGraph(Vinner_S0_ZGC, Error_Vin, 0, "Err s0");
                        CreateGraph(Vinner_S1_ZGC, Error_Vin, 1, "Err s1");
                        CreateGraph(Vinner_S2_ZGC, Error_Vin, 2, "Err s2");
                        CreateGraph(Vinner_S3_ZGC, Error_Vin, 3, "Err s3");
                    }
                }
                if (checkBox2.Checked == true)
                {
                    if (Error_LMS != null)
                    {
                        //Ошибки
                        CreateGraph(LMS_S0_ZGC, Error_LMS, 0, "Err s0");
                        CreateGraph(LMS_S1_ZGC, Error_LMS, 1, "Err s1");
                        CreateGraph(LMS_S2_ZGC, Error_LMS, 2, "Err s2");
                        CreateGraph(LMS_S3_ZGC, Error_LMS, 3, "Err s3");
                    }
                }
            }

            if (radioButton2.Checked == true)
            {
               //оригинал
               CreateGraph(Vinner_S0_ZGC, S, 0, "Original s0");
               CreateGraph(Vinner_S1_ZGC, S, 1, "Original s1");
               CreateGraph(Vinner_S2_ZGC, S, 2, "Original s2");
               CreateGraph(Vinner_S3_ZGC, S, 3, "Original s3");

               CreateGraph(LMS_S0_ZGC, S, 0, "Original s0");
               CreateGraph(LMS_S1_ZGC, S, 1, "Original s1");
               CreateGraph(LMS_S2_ZGC, S, 2, "Original s2");
               CreateGraph(LMS_S3_ZGC, S, 3, "Original s3");
            }
        }


    }
}
