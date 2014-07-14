using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.Util;
using Emgu.CV.Structure;
using System.IO;

namespace TryEasyNeuroField
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        int NeuroType = 0; //Тип обучаемой сети
        List<Neuron> HiddenLayer; //Собственно сам скрытый слой
        Neuron GoodOut; //Нейрон хорошего выхода
        Neuron BadOut; //Нейрон плохого выхода
        double[] InputImage = new double[10]; //Входное изображение
        double[] ThetaIn = new double[10]; //Выходное изображение
        private static Random P = new Random((int)DateTime.Now.Ticks); //Генератор рэндома для рисования изображения
        int LastDetected = 0;//Что нашли?
        double[] Svertka = new double[3]; //Первое ядро для свёрточной сети
        double[] Svertka2 = new double[3]; //Второе ядро дял свёрточной сети
        Image<Bgr, Byte> Video = new Image<Bgr, byte>(450, 10, new Bgr(255, 255, 255)); //Входящий поток
        Image<Bgr, Byte> VideoKernel1 = new Image<Bgr, byte>(450, 8, new Bgr(255, 255, 255)); //Что у нас в первом ядре сидит
        Image<Bgr, Byte> VideoKernel2 = new Image<Bgr, byte>(450, 8, new Bgr(255, 255, 255)); //Что у нас во втором ядре сидит
        int Pos = 0; //положение текущей строки вывода в видео
        double sigpol = 5; //Положение центра сигнала
        private void button1_Click(object sender, EventArgs e)
        {
            //Создаём сеть
            GenerateNS();
            //Переключаем интерфейс
            ButtonOnOff();
            NeuroType = 1; //Сеть: обычная, без свёртки
            for (int l = 0; l < 10000; l++) //Обучим на 10000 примерах
            {
                ///////
                //По очереди обучаем на "положительном" и на отрицательном примере
                //-Создаём изображение
                //-Скармливаем его нейронам
                //-Рассчитываем выход нейронов, выход сети по ним
                //-Вводим ошибку на выходной слой
                //-Вводим ошибку с выходного на скрытый слой
                //-Обновляем веса скрытого слоя
                //-Обновляем веса выходного слоя
                int o = 0;
                int o2 = 0;
                Math.DivRem(l, 2, out o);
                Math.DivRem(l + 1, 2, out o2);
                GenerateII(o); // Создаём "изображение" с "o" объектов на нём.
                for (int i = 0; i < HiddenLayer.Count; i++) //Для каждого нейрона
                {
                    HiddenLayer[i].TakeInput(InputImage); //Нейрон ест входную информацию
                    HiddenLayer[i].Calculate();  //Нейрон рассчитывает выход
                }
                GoodOut.TakeInput(HiddenLayer); //Выходной нейрон, обучаемый на положительные примеры ест информацию
                BadOut.TakeInput(HiddenLayer); //Выходной нейрон, обучаемый на отрицательные примеры ест информацию
                GoodOut.Calculate(); //Считаем выход
                BadOut.Calculate(); //Считаем выход
                GoodOut.ThetaForExit(o); //Начинаем обратно распространять ошибку на положительный неёрон
                BadOut.ThetaForExit(o2); //Начинаем распространять ошибку на отрицательный нейрон
                for (int i = 0; i < HiddenLayer.Count; i++)
                {
                    HiddenLayer[i].ThetaForNode(GoodOut, BadOut, i); //Скрытый слой зрабирает ошибку с нейронов выхода
                }
                for (int i = 0; i < HiddenLayer.Count; i++)
                {
                    HiddenLayer[i].CorrectWeight((10000.0 - l) / 100.0); //Скрытый слой обновляет веса
                }
                GoodOut.CorrectWeight((10000.0 - l) / 10000.0); //Выход обновляет веса
                BadOut.CorrectWeight((10000.0 - l) / 10000.0); //Выход обновляет веса
            }
            label1.Text = "Обучено";
            
        }
        private void button2_Click(object sender, EventArgs e)
        {
            //Определяемся, какая у на сеть, свёрточная, или обычная.
            if (NeuroType == 1)
                OneEx(); //Обычная сеть
            if (NeuroType == 2)
                OneExSv(); //Свёрточная сеть
            //Отрисуем распределение входного сигнала
            Image<Bgr, Byte> Out = new Image<Bgr, byte>(901, 210);
            //Найдем минимумы и максимумы
            double max = int.MinValue;
            double min = int.MaxValue;
            for (int i = 0; i < InputImage.Length; i++)
            {
                if (max < InputImage[i])
                    max = InputImage[i];
                if (min> InputImage[i])
                    min = InputImage[i];
            }
            //Отрисуем сигнал
            for (int i = 1; i < InputImage.Length; i++)
            {

                Point Start = new Point((i - 1) * 100, (int)(205- 200.0 * (InputImage[i - 1] - min) / (max - min)));
                Point Stop = new Point((i) * 100, (int)(205- 200.0 * (InputImage[i] - min) / (max - min)));
                Out.Draw(new LineSegment2D(Start, Stop), new Bgr(0, 0, 255), 3);
                imageBox1.Image = Out;
            }
        }

        /// <summary>
        /// Свёрточная сеть, генерация произвольно расположенного сигнала
        /// </summary>
        private void OneEx()
        {
            OneEx_dis(0);
        }
        /// <summary>
        /// Свёрточная сеть, генерация сигнала в положении pos
        /// </summary>
        /// <param name="pos">Положение сигнала</param>
        private void OneEx(double pos)
        {
            OneEx_dis(pos);
        }
        /// <summary>
        ///Свёрточная сеть, опеределяем объект, распооженный в положении pos
        /// </summary>
        /// <param name="pos">положение сигнала. Если pos=0, распологаем его произвольным образом</param>
        private void OneEx_dis(double pos)
        {
            //-Создаём изображение
            //-Скармливаем его нейронам
            //-Рассчитываем выход нейронов, выход сети по ним
            GenerateII(comboBox1.SelectedIndex, pos);//-Создаём изображение
            for (int i = 0; i < HiddenLayer.Count; i++)
            {
                HiddenLayer[i].TakeInput(InputImage); //-Скармливаем его нейронам
                HiddenLayer[i].Calculate(); //Выход нейронов
            }
            GoodOut.TakeInput(HiddenLayer); 
            BadOut.TakeInput(HiddenLayer);
            GoodOut.Calculate();//Считаем выход сети
            BadOut.Calculate();//Считаем выход сети
            //Вывод результата
            textBox2.Text = GoodOut.RESULT.ToString();
            textBox3.Text = BadOut.RESULT.ToString();
            if (GoodOut.RESULT > BadOut.RESULT)
                LastDetected = 1;
            else
                LastDetected = 0;
        }
        /// <summary>
        /// Генерируем выборку
        /// </summary>
        /// <param name="SignalNum">Количество сигналов в выборке</param>
        private void GenerateII(int SignalNum)
        {
            MainImGenerator(SignalNum, 0);
        }
        /// <summary>
        /// Генерируем выборку
        /// </summary>
        /// <param name="SignalNum">Количество сигналов в выборке</param>
        /// <param name="p">Положение сигналов</param>
        private void GenerateII(int SignalNum, double p)
        {
            MainImGenerator(SignalNum, p);
        }
        /// <summary>
        /// Генерируем выборку
        /// </summary>
        /// <param name="SignalNum">Количество сигналов в выборке</param>
        /// <param name="p">Положение сигналов</param>
        private void MainImGenerator(int SignalNum, double pos)
        {
            //Генерируем "кадр"
            for (int i = 0; i < InputImage.Length; i++)
            {
                InputImage[i] = generateGaussianNoise(1); //Заполняем пиксель шумом
            }
            //Генерируем сигналы
            //Сигнал - это гаусианна, сгенерируем её
            for (int j = 0; j < SignalNum; j++)
            {
                double CountOfSegment = 10000.0; //Количество сегментов, которые будем генерировать
                double dE = double.Parse(textBox5.Text) / CountOfSegment; 
                double place = 0;
                //Если положение "0" то значит оно не задано, генерируем произхвольное
                //Если задано, используем предустановленное положение
                if (pos == 0)
                    place = P.NextDouble() * 9.0 + 0.5;
                else
                    place = pos;
                for (int i = 0; i < CountOfSegment; i++)
                {
                    // sigma^2 = 0.0625
                    // sigma=0.25
                    // 2*sigma=0.5
                    // Значит находясь в центре пикселя 95% энергии попадёт в пиксель
                    double current = generateGaussianNoise(0.0625);
                    double CP = current + place;
                    if ((CP > 0) && (CP < 10))
                    {
                        InputImage[(int)CP] += dE; //Добавем энергию в попавшийся пиксель
                    }
                }
            }
            for (int i = 0; i < InputImage.Length; i++)
            {
                InputImage[i] = InputImage[i] / 100;// Нормировачка, чтобы все значения были меньше единицы
            }
        }
        
 /// <summary>
 /// Генерация нормального распределения алгоритмом Бокса-Мюллера
 /// </summary>
 /// <param name="variance">Квадрат мат.ожидания</param>
 /// <returns>Значение функции распределения</returns>
        private double generateGaussianNoise(double variance)
        {

	        double rand1, rand2;
            
            rand1 = P.NextDouble();
	        if(rand1 < 1e-100) rand1 = 1e-100;
	        rand1 = -2 * Math.Log(rand1);
            rand2 = P.NextDouble() * 2 * Math.PI;

            return Math.Sqrt(variance * rand1) * Math.Cos(rand2);
        }
        /// <summary>
        /// Задаём нейронную сеть на старте
        /// </summary>
        private void GenerateNS()
        {
            int NeuronCountInHiddenLayer = 8; //Количество нейронов в скрытом слое
            HiddenLayer = new List<Neuron>();
            for (int i = 0; i < NeuronCountInHiddenLayer; i++)
            {
                List<int> Root = new List<int>(); //Входы нейрона
                for (int j = -1; j < 2; j++) //Нацеливаем входы на изображение
                    Root.Add(1 + j + i);
                List<int> Leaf = new List<int>(); //Выходы нейрона
                Leaf.Add(0); //Положительный нейрон
                Leaf.Add(1); //Отрицательный нейрон
                double AP = int.Parse(textBox4.Text); //Параметр активации
                HiddenLayer.Add(new Neuron(Root, Leaf, 0, 0, AP, P)); //Создаём нейрон скрытого слоя с такими характеристиками
            }
            List<int> Root2 = new List<int>(); //Входы для выходных нейронов
            for (int j = 0; j < NeuronCountInHiddenLayer; j++)
                Root2.Add(j);
            List<int> Leaf2 = new List<int>();
            Leaf2.Add(0); //Выходы для выходных нейронов
            GoodOut = new Neuron(Root2, Leaf2, 0, 0, int.Parse(textBox4.Text), P); //Выходной положительный нейрон
            BadOut = new Neuron(Root2, Leaf2, 0, 0, int.Parse(textBox4.Text), P); //Выходной отрицательный нейрон
            VideoKernel1 = new Image<Bgr, byte>(450, 8, new Bgr(255, 255, 255)); //Изорбажение для последующей отрисовки активности сети
            VideoKernel2 = new Image<Bgr, byte>(450, 8, new Bgr(255, 255, 255));//Изорбажение для последующей отрисовки активности сети
        }
        /// <summary>
        /// Создаём свёрточную нейронную сеть
        /// </summary>
        private void GenerateNSCov()
        {
            int NeuronCountInHiddenLayer = 8;
            HiddenLayer = new List<Neuron>();
            //Для свёрточной сети у нас будет два ядра - все нейроны будут продублированны
            for (int i = 0; i < NeuronCountInHiddenLayer; i++)
            {
                List<int> Root = new List<int>();//Входы нейрона
                for (int j = -1; j < 2; j++) //Пиксели изображения, которые нейрон берёт
                    Root.Add(1 + j + i);
                List<int> Leaf = new List<int>();
                Leaf.Add(0);
                Leaf.Add(1);
                double AP = int.Parse(textBox4.Text);
                HiddenLayer.Add(new Neuron(Root, Leaf, 1, 0, AP, P)); //Создаём нейрон
                HiddenLayer.Add(new Neuron(Root, Leaf, 1, 0, AP, P)); //Создаём нейрон
            }
            //Инициализируем матрицы свёрточной сети
            for (int i = 0; i < Svertka.Length; i++) 
            {
                Svertka[i] = P.NextDouble() / 10.0;
                Svertka2[i] = P.NextDouble() / 10.0;
            }
            List<int> Root2 = new List<int>();//Входы для выходных нейронов
            for (int j = 0; j < 2*NeuronCountInHiddenLayer; j++)
                Root2.Add(j);
            List<int> Leaf2 = new List<int>();
            Leaf2.Add(0);
            GoodOut = new Neuron(Root2, Leaf2, 0, 0, int.Parse(textBox4.Text), P);//Выходной положительный нейрон
            BadOut = new Neuron(Root2, Leaf2, 0, 0, int.Parse(textBox4.Text), P);//Выходной отрицательный нейрон
            VideoKernel1 = new Image<Bgr, byte>(450, 16, new Bgr(255, 255, 255));//Изорбажение для последующей отрисовки активности сети
            VideoKernel2 = new Image<Bgr, byte>(450, 16, new Bgr(255, 255, 255));//Изорбажение для последующей отрисовки активности сети
        }
        private void button3_Click(object sender, EventArgs e)
        {
            //Обсчитаем 1000 примеров
            if (NeuroType == 1)
                CalcMany(); //Для не свёрточной сети
            if (NeuroType == 2)
                CalcManySv(); //Для свёрточной сети
        }

        private void CalcMany()
        {
            int l = 0;
            //Для 1000 примеров
            for (int k = 0; k < 1000; k++)
            {
                //-Создаём изображение
                //-Скармливаем его нейронам
                //-Рассчитываем выход нейронов, выход сети по ним
                GenerateII(comboBox1.SelectedIndex); //-Создаём изображение
                for (int i = 0; i < HiddenLayer.Count; i++)
                {
                    HiddenLayer[i].TakeInput(InputImage);//-Скармливаем его нейронам
                    HiddenLayer[i].Calculate();//-Рассчитываем выход нейронов
                }
                GoodOut.TakeInput(HiddenLayer);
                BadOut.TakeInput(HiddenLayer);
                GoodOut.Calculate();// выход сети по ним
                BadOut.Calculate();// выход сети по ним
                if (GoodOut.RESULT > BadOut.RESULT)
                    l++;
            }
            textBox1.Text = l.ToString();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox1.SelectedIndex = 0;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            //Обучение свёрточной сети
            //Создаём сеть
            GenerateNSCov();
            //Переключение режима кнопок
            ButtonOnOff();
            //Тип сети
            NeuroType = 2;
            for (int l = 0; l < 10000; l++)
            {
                ///////
                //По очереди обучаем на "положительном" и на отрицательном примере
                //-Создаём изображение
                //-Скармливаем его нейронам
                //-Рассчитываем выход нейронов, выход сети по ним
                //-Вводим ошибку на выходной слой
                //-Вводим ошибку с выходного на скрытый слой
                //-Обновляем веса ядра свёртки
                //-Обновляем веса выходного слоя
                int o = 0;
                int o2 = 0;
                Math.DivRem(l, 2, out o);
                Math.DivRem(l + 1, 2, out o2);
                GenerateII(o); //-Создаём изображение
                for (int i = 0; i < HiddenLayer.Count/2; i++)
                {
                    HiddenLayer[i * 2].TakeInput(InputImage);//-Скармливаем изображение нейронам
                    HiddenLayer[i*2].Calculate(Svertka); //Считаем с применением нашего ядра свёртки
                    HiddenLayer[i * 2 + 1].TakeInput(InputImage);//-Скармливаем изображение нейронам
                    HiddenLayer[i * 2 + 1].Calculate(Svertka2);//Считаем с применением нашего второго ядра свёртки
                }
                GoodOut.TakeInput(HiddenLayer);//Загрузим в выходной нейрон входные
                BadOut.TakeInput(HiddenLayer);//Загрузим в выходной нейрон входные
                GoodOut.Calculate();//Посчитаем выход сети
                BadOut.Calculate();//Посчитаем выход сети
                GoodOut.ThetaForExit(o);//-Вводим ошибку на выходной слой
                BadOut.ThetaForExit(o2);//-Вводим ошибку на выходной слой
                for (int i = 0; i < HiddenLayer.Count / 2; i++)
                {
                    HiddenLayer[i * 2].ThetaForNode(GoodOut, BadOut, i * 2);//-Вводим ошибку с выходного на скрытый слой
                    HiddenLayer[i * 2 + 1].ThetaForNode(GoodOut, BadOut, i * 2 + 1);//-Вводим ошибку с выходного на скрытый слой
                }
                for (int i = 0; i < HiddenLayer.Count/2; i++)
                {
                    HiddenLayer[i * 2].CorrectWeight((10000.0 - l) / (8 * 100.0), ref Svertka);//-Обновляем веса ядра свёртки
                    HiddenLayer[i * 2 + 1].CorrectWeight((10000.0 - l) / (8 * 100.0), ref Svertka2);//-Обновляем веса ядра свёртки
                }
                GoodOut.CorrectWeight((10000.0 - l) / 10000.0);//-Обновляем веса выходного слоя
                BadOut.CorrectWeight((10000.0 - l) / 10000.0);//-Обновляем веса выходного слоя
            }
            label1.Text = "Обучено";
        }
        /// <summary>
        /// Вырубаем ненужные кнопки после обучения
        /// </summary>

        private void ButtonOnOff()
        {
            button1.Enabled = false;
            button6.Enabled = false;
            button2.Enabled = true;
            button3.Enabled = true;
            textBox4.Enabled = false;
            textBox5.Enabled = false;
            button4.Enabled = true;
            label1.Text = "Обучаемся...";
            comboBox1.Enabled = true;
        }

        /// <summary>
        /// Для свёрточной сети считаем одине объект
        /// </summary>
        private void OneExSv()
        {

            OneExSv_dis(0);
        }
        /// <summary>
        /// Для свёрточной сети считаем одине объект с положением Pos
        /// </summary>
        private void OneExSv(double pos)
        {

            OneExSv_dis(pos);
        }

        /// <summary>
        /// Для свёрточной сети считаем одине объект с положением Pos
        /// </summary>
        private void OneExSv_dis(double pos)
        {
            //-Создаём изображение
            //-Скармливаем его нейронам
            //-Рассчитываем выход нейронов, выход сети по ним
            GenerateII(comboBox1.SelectedIndex, pos);
            for (int i = 0; i < HiddenLayer.Count / 2; i++)
            {
                HiddenLayer[i * 2].TakeInput(InputImage);
                HiddenLayer[i * 2].Calculate(Svertka);
                HiddenLayer[i * 2 + 1].TakeInput(InputImage);
                HiddenLayer[i * 2 + 1].Calculate(Svertka2);
            }
            GoodOut.TakeInput(HiddenLayer);
            BadOut.TakeInput(HiddenLayer);
            GoodOut.Calculate();
            BadOut.Calculate();
            textBox2.Text = GoodOut.RESULT.ToString();
            textBox3.Text = BadOut.RESULT.ToString();
            if (GoodOut.RESULT > BadOut.RESULT)
                LastDetected = 1;
            else
                LastDetected = 0;
        }
        /// <summary>
        /// Рассчитывем для 1000 объектов
        /// </summary>
        private void CalcManySv()
        {
            int l = 0;
            for (int k = 0; k < 1000; k++)
            {
                //-Создаём изображение
                //-Скармливаем его нейронам
                //-Рассчитываем выход нейронов, выход сети по ним
                GenerateII(comboBox1.SelectedIndex);
                for (int i = 0; i < HiddenLayer.Count / 2; i++)
                {
                    HiddenLayer[i * 2].TakeInput(InputImage);
                    HiddenLayer[i * 2].Calculate(Svertka);
                    HiddenLayer[i * 2 + 1].TakeInput(InputImage);
                    HiddenLayer[i * 2 + 1].Calculate(Svertka2);
                }
                GoodOut.TakeInput(HiddenLayer);
                BadOut.TakeInput(HiddenLayer);
                GoodOut.Calculate();
                BadOut.Calculate();
                if (GoodOut.RESULT > BadOut.RESULT)
                    l++;
            }
            textBox1.Text = l.ToString();
        }
        /// <summary>
        /// Запускаем крутиться изоббражение снизу
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button4_Click_1(object sender, EventArgs e)
        {
            if (timer1.Enabled == false)
            {
                button4.Text = "Остановить";
                timer1.Enabled = true;
            }
            else
            {
                button4.Text = "Динамика";
                timer1.Enabled = false;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            //На таймере создаём новый пример, обсчитываем его, выводим
            sigpol += (P.NextDouble()-0.5); //Положение сигнала на входе
            //Отрежем сигнал, если он сбоку
            if (sigpol < 1.5)
                sigpol = 1.5;
            //Отрежем сигнал, если он сбоку
            if (sigpol > 8.5)
                sigpol = 8.5;
            //Выбираем какой из сетей считаем
            if (NeuroType == 1)
                OneEx(sigpol);//Обычной 
            if (NeuroType == 2)
                OneExSv(sigpol);//Свёрточной
            if (LastDetected == 0) //Отображаем нашли ли объект или нет
            {
                label7.Text = "Объект отсутствует";
                label7.ForeColor = Color.Red;
            }
            else
            {
                label7.Text = "Объект обнаружен";
                label7.ForeColor = Color.Green;
            }
            //Найдём минимумы и максимумы входной картинки
            double max = int.MinValue;
            double min = int.MaxValue;
            for (int i = 0; i < InputImage.Length; i++)
            {
                if (max < InputImage[i])
                    max = InputImage[i];
                if (min > InputImage[i])
                    min = InputImage[i];
            }
            //Отрисуем входную картинку
            for (int i = 0; i < InputImage.Length; i++)
            {
                byte E = (byte)(255.0 * (InputImage[i] - min) / (max - min));
                Video.Data[i, Pos, 0] = E;
                Video.Data[i, Pos, 1] = E;
                Video.Data[i, Pos, 2] = E;
            }
            //Найдём минимумы и максимумы того что подаётся на первый нейрон
            max = int.MinValue;
            min = int.MaxValue;
            for (int i = 0; i < HiddenLayer.Count; i++)
            {
                if (max < HiddenLayer[i].RESULT*GoodOut.mass[i])
                    max = HiddenLayer[i].RESULT * GoodOut.mass[i];
                if (min > HiddenLayer[i].RESULT * GoodOut.mass[i])
                    min = HiddenLayer[i].RESULT * GoodOut.mass[i];
            }
            //Отрисуем входную картинку первого нейрона
            for (int i = 0; i < HiddenLayer.Count; i++)
            {
                byte E = (byte)(255.0 * (HiddenLayer[i].RESULT * GoodOut.mass[i] - min) / (max - min));
                VideoKernel1.Data[i, Pos, 0] = E;
                VideoKernel1.Data[i, Pos, 1] = E;
                VideoKernel1.Data[i, Pos, 2] = E;
            }
            //Найдём минимумы и максимумы того что подаётся на второй нейрон
            max = int.MinValue;
            min = int.MaxValue;
            for (int i = 0; i < HiddenLayer.Count; i++)
            {
                if (max < HiddenLayer[i].RESULT * BadOut.mass[i])
                    max = HiddenLayer[i].RESULT * BadOut.mass[i];
                if (min > HiddenLayer[i].RESULT * BadOut.mass[i])
                    min = HiddenLayer[i].RESULT * BadOut.mass[i];
            }
            //Отрисуем входную картинку второго нейрона
            for (int i = 0; i < HiddenLayer.Count; i++)
            {
                byte E = (byte)(255.0 * (HiddenLayer[i].RESULT * BadOut.mass[i] - min) / (max - min));
                VideoKernel2.Data[i, Pos, 0] = E;
                VideoKernel2.Data[i, Pos, 1] = E;
                VideoKernel2.Data[i, Pos, 2] = E;
            }


            Pos++;
            if (Pos == Video.Width)
                Pos = 0;
            imageBox2.Image = Video;
            imageBox3.Image = VideoKernel1;
            imageBox4.Image = VideoKernel1;
        }

      

    }
    /// <summary>
    /// Собственно сам нейрон
    /// </summary>
    public class Neuron
    {
        public double[] input; //Что у нейрона на входе
        public double[] mass; //Коэффициенты входных синапсов
        public int[] Root; //Номера элементов из которых нейрон берёт входную информацию
        public double RESULT; //Рассчитанный выход нейрона
        public int[] Leaf; //Какие элементы у нейрона на выходе
        public double ActivationParametr; //Параметр активации
        public double BPThetta; //Ошибка нейрона
 
        /// <summary>
        /// Корректируем вес нейрона
        /// </summary>
        /// <param name="Speed">Скорость коррекции</param>
        public void CorrectWeight(double Speed)
        {
            for (int i = 0; i < mass.Length; i++)
            {
                //Старый вес + скорость*ошибку*вход*результат*(1-результат)
                mass[i] = mass[i] + Speed * BPThetta * input[i] * RESULT * (1 - RESULT);
            }
        }
        /// <summary>
        /// Корректируем вес нейрона для свёрточных сетей. По сути вес свёртки
        /// </summary>
        /// <param name="Speed"></param>
        /// <param name="massOut"></param>
        public void CorrectWeight(double Speed, ref double[] massOut)
        {
            for (int i = 0; i < mass.Length; i++)
            {
                //Старый вес + скорость*ошибку*вход*результат*(1-результат)
                massOut[i] = massOut[i] + Speed * BPThetta * input[i] * RESULT * (1 - RESULT);
            }
        }
        /// <summary>
        /// Считаем ошибку дял выходного нейрона
        /// </summary>
        /// <param name="ans">Что должно быть на выходе</param>
        public void ThetaForExit(double ans)
        {
            BPThetta = ans - RESULT;
        }
        /// <summary>
        /// Считаем ошибку для нейрона скрытой сети
        /// </summary>
        /// <param name="t1">Положительный нейрон выхода</param>
        /// <param name="t2">Отрицателньый нейрон выхода</param>
        /// <param name="myname">Номер текущего нейрона</param>
        public void ThetaForNode(Neuron t1, Neuron t2, int myname)
        {
            //Величина ошибки считается как обратная проекция нейрона
            BPThetta = t1.mass[myname] * t1.BPThetta + t2.mass[myname] * t2.BPThetta;
        }
        /// <summary>
        /// Считать входную информацию
        /// </summary>
        /// <param name="II">Входной массив изображения</param>
        public void TakeInput(double[] II)
        {
            for (int i = 0; i < input.Length; i++)
            {
                input[i] = II[Root[i]];
            }
        }
        /// <summary>
        /// Считать входную информацию из скрытого слоя
        /// </summary>
        /// <param name="II">Скрытый слой</param>
        public void TakeInput(List<Neuron> II)
        {
            for (int i = 0; i < input.Length; i++)
            {
                input[i] = II[Root[i]].RESULT;
            }
        }
        /// <summary>
        /// Рассчитать нейрон
        /// </summary>
        /// <returns></returns>

        public double Calculate()
        {
            RESULT = 0;
            //Сумма всех входных импульсов
            for (int i = 0; i < input.Length; i++)
                RESULT += input[i] * mass[i];
            //Активация нейрона
            RESULT = 1 / (1 + Math.Exp(-2 * ActivationParametr * RESULT));
            return RESULT;
        }
        /// <summary>
        /// Рассчитать нейрон для свёрточной сети
        /// </summary>
        /// <param name="massOut"></param>
        /// <returns></returns>
        public double Calculate(double[] massOut)
        {
            RESULT = 0;
            for (int i = 0; i < input.Length; i++)
                RESULT += input[i] * massOut[i];
            RESULT = 1 / (1 + Math.Exp(-2 * ActivationParametr * RESULT));
            return RESULT;
        }
        /// <summary>
        /// Инициализация нейрона
        /// </summary>
        /// <param name="CH">Входной набор</param>
        /// <param name="FM">Выходной набор</param>
        /// <param name="InitType">Тип инициализации, 0- заполнить рандомными значениями все веса
        /// 1 - не заполнять и использовать для заполнения константу</param>
        /// <param name="constanta">Которую будем использовать для заполнения</param>
        /// <param name="AP">Активационный параметр</param>
        /// <param name="P">генератор рандома, чтобы все нейроны разными вышли</param>
        public Neuron(List<int> CH, List<int> FM, int InitType, double constanta, double AP, Random P)
        {
            ActivationParametr = AP;
            input = new double[CH.Count];
            mass = new double[CH.Count];
            Root = new int[CH.Count];
            for (int i=0;i<CH.Count;i++)
            {
                input[i] = 0;
                Root[i] = CH[i];
                if (InitType == 0)
                {
                    mass[i] = P.NextDouble() / 10.0;
                }
                else
                    mass[i] = constanta;
            }
            Leaf = new int[FM.Count];
            for (int i = 0; i < FM.Count; i++)
            {
                Leaf[i] = FM[i];
            }
        }
    }
}
