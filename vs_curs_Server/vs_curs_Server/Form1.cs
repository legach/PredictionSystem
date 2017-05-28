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


namespace vs_curs_Server
{
    public partial class Form1 : Form
    {

        string pathname = "example.txt";
        Thread newServer = null;
        TcpListener server = null;
        public Form1()
        {
            InitializeComponent();
        }

        public void SetLog(string str)
        {
            if (textBox3.InvokeRequired) 
                textBox3.Invoke(new Action<string>((s) => textBox3.Text += s), str);
            else textBox3.Text += str;
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


        public void TpcServer() // Функция сервера
        {
            
            try
            {
                Int32 port = Convert.ToInt32(textBox1.Text); //порт сервера
                IPAddress localAddr = IPAddress.Parse("127.0.0.1");//ip-адрес сервера (интерфейс)

                //TcpListener - класс TCP-сервера из .Net Framework Class Library
                server = new TcpListener(localAddr, port);

                // начинаем ожидание подсоединений клиентов на интерфейсе localAddr и порту port
                server.Start();

                // буффер для приема сообщений и соответствующая ему строка для вывода на экран
                Byte[] bytes = new Byte[1024];
                Byte[] msg = null;
                FileStream fs = null;
                String data;
                long col=0;

                //ответ клиенту
                String answer_message;

                //цикл обработки подсоединений клиентов
                while (true)
                {
                    SetLog("Waiting for a connection... " + Environment.NewLine);
                    // Ждем соединения клиента
                    TcpClient client = server.AcceptTcpClient();
                    //Ура! Кто-то подсоединился!
                    SetLog("Connected!" + Environment.NewLine);
                    // вводим поток stream для чтения и записи через установленное соединение
                    NetworkStream stream = client.GetStream();
                    int RecByte = stream.Read(bytes, 0, bytes.Length);
                    if (RecByte > 0)
                    {
                        // преобразуем принятые данные в строку ASCII string.
                        data = System.Text.Encoding.ASCII.GetString(bytes, 0, RecByte);
                        //печатаем то, что получили
                        SetLog("Received: " + data + Environment.NewLine);
                        //анализируем запрос клиента и вычисляем результат
                        switch (data)
                        {
                            case "$TakePlease$":
                                answer_message = "$Ok$";
                                SetLog("Sent: " + answer_message + Environment.NewLine);
                                msg = System.Text.Encoding.ASCII.GetBytes(answer_message);
                                stream.Write(msg, 0, msg.Length);
                                SetLog("Processing..."+Environment.NewLine);
                                using (fs = new FileStream(pathname,
                                        FileMode.Create,
                                        FileAccess.Write,
                                        FileShare.None,
                                        4096,
                                        FileOptions.SequentialScan))
                                {
                                    col=CopyStream(stream, fs, 4096);
                                    fs.Close();
                                    SetLog("Received file " + col.ToString() + Environment.NewLine);
                                }
                                col = 0;
                                break;

                            case "$GivePlease$":
                                answer_message = "$Ok$";
                                SetLog("Sent: " + answer_message + Environment.NewLine);
                                msg = System.Text.Encoding.ASCII.GetBytes(answer_message);
                                stream.Write(msg, 0, msg.Length);

                                fs = new FileStream(pathname, FileMode.Open, FileAccess.Read);
                                col=CopyStream(fs, stream, 4096);
                                fs.Close();
                                SetLog("Sending file" + col.ToString()+Environment.NewLine);
                                col = 0;
                                break;
                        }
                    }

                    // закрываем соединение
                    client.Close();
                }
            }
            catch (SocketException expt)
            {
                SetLog("SocketException:" + expt + Environment.NewLine);
            }
            catch (ThreadAbortException expt)
            {
                //SetLog("SocketException:" + expt + Environment.NewLine);
            }
            finally
            {
                // Stop listening for new clients.
                server.Stop();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            newServer = new Thread(new ThreadStart(TpcServer));
            newServer.Start(); // Вызываем поток
        }

        private void button2_Click(object sender, EventArgs e)
        {
            server.Stop();
            newServer.Abort();
            SetLog("Server stopped");
        }



        
    }
}
