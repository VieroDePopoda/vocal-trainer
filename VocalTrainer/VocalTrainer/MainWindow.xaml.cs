using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.Wave;
using ZedGraph;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;
using System.Threading.Tasks;

namespace VocalTrainer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Функционал

        private int KeysOffset = 2;
        private int KeyHeight = 155;
        private int KeyWidth = 30;
        private int DiezHeight = 100;
        private int DiezWiddth = 20;
        private int CurrentAnswers = 0;
        private int GlobalIndex = 0;
        private int ExerciseNumber = 1;
        private int timerTick = 0;
        private bool isDeviceChecked = false;
        private bool messageChecked = false;

        private byte StartOctave = 1;
        private byte EndOctave = 7;

        private List<string> notesList = new List<string>();
        private List<int> frequency = new List<int>();
        private Queue<string> queue = new Queue<string>();

        private AudioSintezator _sintezator = new AudioSintezator();

        private SolidColorBrush _diezColor = Brushes.Black;
        private SolidColorBrush _genericColor = Brushes.White;

        static int RATE = 48000;
        static int N;
        static double Fn = RATE / 2;
        double max_value = 0;
        double min_value = 999999;

        public Dictionary<int, string> assocNotesList = new Dictionary<int, string>();

        private int device { get; set; }

        private DispatcherTimer timer = new DispatcherTimer();

        public WaveIn waveIn;
        public MainWindow()
        {
            InitializeComponent();

            comboBox.Background = Brushes.Red;
            button.Visibility = Visibility.Hidden;
            button_Start.Visibility = Visibility.Hidden;

            timer.Tick += new EventHandler(Timer_Tick);
            timer.Interval = TimeSpan.FromMilliseconds(200);
            

            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            int keyPosition = KeysOffset;

            for (byte octave = StartOctave; octave <= EndOctave; ++octave) // Генерация клавиш пианино
            {
                for (int i = 0; i < 12; ++i) // 12 полутонов в октаве
                {
                    TextBlock currentKey = new TextBlock();

                    var note = new Note(octave, (Tones)i);

                    currentKey.Text = note.ToString();
                    currentKey.Name = GenerateKeyName(note);

                    if (!note.isDiez()) //Диез идут как черные клавиши
                    {
                        currentKey.Width = KeyWidth;
                        currentKey.Height = KeyHeight;

                        currentKey.Background = _genericColor;

                        currentKey.Margin = new Thickness(keyPosition, 0, 0, 0);
                        currentKey.Padding = new Thickness(0, 130, 0, 0);

                        currentKey.SetValue(Panel.ZIndexProperty, 0);

                        keyPosition += KeyWidth + KeysOffset;

                        currentKey.Foreground = Brushes.Black;

                        currentKey.FontSize = 14;
                    }
                    else
                    {
                        currentKey.Width = DiezWiddth;
                        currentKey.Height = DiezHeight;

                        currentKey.Background = _diezColor;

                        currentKey.Margin = new Thickness(keyPosition - DiezWiddth / 2 + KeysOffset / 2,
                            0, 0, 0);
                        currentKey.Padding = new Thickness(0, 75, 0, 0);

                        currentKey.SetValue(Panel.ZIndexProperty, 1);

                        currentKey.Foreground = Brushes.White;

                        currentKey.FontSize = 10;
                    }

                    currentKey.TextAlignment = TextAlignment.Center;

                    currentKey.FontWeight = FontWeights.Bold;
                    currentKey.Focusable = true;


                    currentKey.HorizontalAlignment = HorizontalAlignment.Left;
                    currentKey.VerticalAlignment = VerticalAlignment.Top;

                    currentKey.PreviewMouseLeftButtonDown += PianoKeyDown;
                    currentKey.PreviewMouseLeftButtonUp += PianoKeyUp;

                    currentKey.MouseEnter += PianoKeyPreview;

                    currentKey.MouseLeave += PianoKeyLeave;


                    KeysGrid.Children.Add(currentKey);
                }
            }

            midiNotesToCollection(); //Берем текстовые названия нот на миди и добавляем в коллекцию
        }

        private string GenerateKeyName(Note note)
        {
            return note.ToString().Replace('#', 'd');
        }

        private void PianoKeyLeave(object sender, MouseEventArgs e)
        {
            var obj = (TextBlock)sender;

            Note note = ParseKeyName(obj.Name);

            if (!note.isDiez()) obj.Background = Brushes.White;
            else obj.Background = Brushes.Black;
        }
        private void PianoKeyPreview(object sender, MouseEventArgs e)
        {
            var obj = (TextBlock)sender;

            obj.Background = Brushes.Gray;
        }

        private void PianoKeyDown(object sender, MouseButtonEventArgs e)
        {
            var obj = (TextBlock)sender;
            obj.Background = Brushes.LightGreen;

            Note note = ParseKeyName(obj.Name);

            _sintezator.PlayTone(note.Octave, note.Tone);
        }
        private void PianoKeyUp(object sender, MouseButtonEventArgs e)
        {
            var obj = (TextBlock)sender;

            Note note = ParseKeyName(obj.Name);

            if (!note.isDiez()) obj.Background = Brushes.LightGreen;
            else obj.Background = Brushes.Black;

            _sintezator.StopPlaying(note.Octave, note.Tone);
        }

        internal static Note ParseKeyName(string data)
        {
            return Note.FromString(data.Replace('#', 'd'));
        }

        private void ComboBox_Initialized(object sender, EventArgs e)
        {
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var device = WaveIn.GetCapabilities(i);
                comboBox.Items.Add($"{i}: {device.ProductName} {device.Channels}");
            }
        }
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) // Select device
        {
            device = comboBox.SelectedIndex;
            button_Start.Visibility = Visibility.Visible;
            isDeviceChecked = true;
        }

        void waveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            //данные из буфера распределяем в массив
            byte[] buffer = e.Buffer;
            N = buffer.Length;
            int bytesRecorded = e.BytesRecorded;
            Complex[] sig = new Complex[bytesRecorded / 2];
            for (int i = 0, j = 0; i < e.BytesRecorded; i += 2, j++)
            {
                short sample = (short)((buffer[i + 1] << 8) | buffer[i + 0]);
                sig[j] = sample / 32768f;
            }

            Fourier.Forward(sig, FourierOptions.Matlab);
            // обнуляем спектр на небольших частотах
            for (int i = 0; i < 35 * sig.Length / Fn; i++)
            {
                sig[i] = 0;
            }

            write(sig);
        }

        void waveIn_Stop()
        {
            waveIn.StopRecording();
            waveIn.Dispose();
        }

        private void Button_stop(object sender, RoutedEventArgs e) // Stop button
        {
            try
            {
                waveIn_Stop();
            }
            catch
            {

            }
        }

        private void write(Complex[] signal) //zapis' chastot
        {
            PointPairList list1 = new PointPairList();

            int max_index = 0;
            int freq = 0;
            double K = signal.Length / 2;

            for (int i = 0; i < K; i++)
            {
                list1.Add(i * Fn / K, Complex.Abs(signal[i]) / N * 2);
            }

            foreach (PointPair i in list1)
            {
                if (i.Y > list1[max_index].Y)
                {
                    max_index = list1.IndexOf(i);
                }
            }
            freq = (int)list1[max_index].X;

            if (max_value < freq) max_value = freq;
            if (min_value > freq) min_value = freq;

            label3.Content = min_value;
            label4.Content = max_value;

            int s = freq;

            for (int i = 0; i < frequency.Count(); i++) // Показываем какая нота сейчас играет
            {
                if (s == frequency[i])
                {
                    highlightMyNote(notesList[i]);
                }

            }
        }

        private void Button_Start_Click(object sender, RoutedEventArgs e) //Start recording
        {
            waveIn_startRecording();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            timerTick++;
        }

        void waveIn_startRecording()
        {
            min_value = 9999999;
            max_value = 0;

            waveIn = new WaveIn();
            waveIn.DeviceNumber = device;
            waveIn.WaveFormat = new WaveFormat(RATE, 1);
            waveIn.DataAvailable += waveIn_DataAvailable;
            waveIn.StartRecording();
           

            button.Visibility = Visibility.Visible;
            button_Start.Visibility = Visibility.Hidden;
            acceptButton.Visibility = Visibility.Visible;
        }
        void midiNotesToCollection() //Берем текстовые названия нот на миди и добавляем в коллекцию
        {
            IEnumerable<TextBlock> collection = KeysGrid.Children.OfType<TextBlock>();

            foreach (TextBlock f in collection)
            {
                if (f.Text.Contains("#") != true) notesList.Add(f.Text);

            }
            //Добавляем список частот в лист, из которого будем расчитывать частоту ноты исходя из формулы Ч.Ноты[i] = Нота[i]
            frequency.Add(27);
            frequency.Add(31);
            frequency.Add(33);
            frequency.Add(35);
            frequency.Add(40);
            frequency.Add(45);
            frequency.Add(50);
            frequency.Add(60);
            frequency.Add(70);
            frequency.Add(80);
            frequency.Add(90);

            frequency.Add(100);//С1 - Е7
            frequency.Add(110);
            frequency.Add(120);
            frequency.Add(130);
            frequency.Add(150);
            frequency.Add(160);
            frequency.Add(170);
            frequency.Add(200);
            frequency.Add(220);
            frequency.Add(250);
            frequency.Add(260); //C4
            frequency.Add(290);
            frequency.Add(330);
            frequency.Add(350);
            frequency.Add(390);
            frequency.Add(440);
            frequency.Add(490);
            frequency.Add(520); //C5
            frequency.Add(590);
            frequency.Add(660);
            frequency.Add(700);
            frequency.Add(780);
            frequency.Add(880);
            frequency.Add(990); //C6

            frequency.Add(1050);
            frequency.Add(1170);
            frequency.Add(1320);
            frequency.Add(1400);
            frequency.Add(1570);
            frequency.Add(1760);
            frequency.Add(1980);
            frequency.Add(2090); //C7
            frequency.Add(2350);
            frequency.Add(2640);
            frequency.Add(2790);
            frequency.Add(3140);
            frequency.Add(3520);
            frequency.Add(3950);

            for (int i = 0; i < frequency.Count; i++) //Весь диапазон
            {
                assocNotesList.Add(frequency[i], notesList[i]);
            }
        }

        void highlightMyNote(string note) //Подсвечиваем ноту, которая взята голосом
        {
            IEnumerable<TextBlock> collection = KeysGrid.Children.OfType<TextBlock>();

            foreach (TextBlock textBlock in collection)
            {

                if (CurrentAnswers == 5) { CurrentAnswers = 0; GlobalIndex++; ContinueExercise(); timerTick = 0; timer.Stop(); }

                if (textBlock.Text == note  && textBlock.Foreground != Brushes.LawnGreen)
                {
                    if(timer.IsEnabled == true) { timerTick = 0; timer.Stop(); }

                    textBlock.Background = Brushes.LightGray;
                }
                else if (textBlock.Text == note && textBlock.Foreground == Brushes.LawnGreen)
                {
                    if (textBlock.Text == queue.Peek() && queue != null)
                    {
                        textBlock.Background = Brushes.Green;

                        timer.Start();

                        if (timerTick >= 1)
                        {
                            queue.Dequeue(); //Удаляем ноту из очереди
                            timerTick = 0;
                            CurrentAnswers++;
                            textBlock.Foreground = Brushes.Black;
                        }

                        if (CurrentAnswers == 3) { highlightMyKey(queue.Peek()); highlightMyKey(queue.Last()); }
                    }
                }
                else if (textBlock.Text.Contains("#") == true)
                    textBlock.Background = Brushes.Black;
                else
                    textBlock.Background = Brushes.White;
            }
        }

        public void highlightMyKey(string note) //Тот же самый метод, но уже для упражнений
        {
            IEnumerable<TextBlock> collection = KeysGrid.Children.OfType<TextBlock>();

            foreach (TextBlock textBlock in collection)
            {
                if (textBlock.Text == note)
                {
                    textBlock.Foreground = Brushes.LawnGreen;
                }
            }
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e) //Тут мы уже формируем диапазон из которого будут составлены упражнения
        {
            foreach (KeyValuePair<int, string> keyValue in assocNotesList.ToList())
            {
                if (min_value > keyValue.Key || max_value < keyValue.Key)
                {
                    assocNotesList.Remove(keyValue.Key);
                }
            }

            Task.Delay(250).ContinueWith(_ =>
            {
                MessageBox.Show("Готово!");
                MessageBox.Show($"Ваш диапазон от {assocNotesList.First()} до {assocNotesList.Last()}");
            }
                );
        }

        private void Button_accept2_Click(object sender, RoutedEventArgs e) //Диапазон вбиваемый ручками
        {
            if (minFreq != null & maxFreq != null)
            {
                min_value = Convert.ToDouble(minFreq.Text);
                max_value = Convert.ToDouble(maxFreq.Text);

                foreach (KeyValuePair<int, string> keyValue in assocNotesList.ToList())
                {
                    if (min_value > keyValue.Key || max_value < keyValue.Key)
                    {
                        assocNotesList.Remove(keyValue.Key);
                    }
                }

                Task.Delay(250).ContinueWith(_ =>
                {
                    MessageBox.Show("Готово!");
                    MessageBox.Show($"Ваш диапазон от {assocNotesList.First()} до {assocNotesList.Last()}");
                }
                    );
            }
            else
            {
                MessageBox.Show("Заполнены не все поля.");
            }
        }
        #endregion

        #region UI блок
            private void ImgStart_MouseDown(object sender, MouseButtonEventArgs e) //Старт упражнений
            {
                if (isDeviceChecked == true)
                {
                    Button_Start_Click(sender, e);
                    imgStart.Visibility = Visibility.Hidden;
                    imgStop.Visibility = Visibility.Visible;

                    Button_Click(sender, e);
                }
                else
                {
                    MessageBox.Show("Пожалуйста выберите устройство записи!");
                }
            }

            private void ImgStop_MouseDown(object sender, MouseButtonEventArgs e) //Стоп упражнений
            {
                IEnumerable<TextBlock> collection = KeysGrid.Children.OfType<TextBlock>();

                foreach (TextBlock textBlock in collection)
                {
                    if (textBlock.Text.Contains("#") == true)
                        textBlock.Foreground = Brushes.White;
                    else
                        textBlock.Foreground = Brushes.Black;
                }

                imgStart.Visibility = Visibility.Visible;
                imgStop.Visibility = Visibility.Hidden;
                Button_stop(sender, e);
            }

            private void DiapasonButton_Click(object sender, MouseButtonEventArgs e) //Настройка микрофона и диапазона
            {
                if (AudioGrid.Visibility == Visibility.Visible)
                {
                    Button_stop(sender, e);
                    button_Start.Visibility = Visibility.Visible;
                    button.Visibility = Visibility.Hidden;
                    acceptButton.Visibility = Visibility.Hidden;
                }

                if (AudioGrid.Visibility == Visibility.Hidden) AudioGrid.Visibility = Visibility.Visible;
                    else AudioGrid.Visibility = Visibility.Hidden;    
        }
        private void ImgEmptyBox_MouseEnter(object sender, MouseEventArgs e)
        {
            imgHead.Text = "Это блок с подсказками";
            imgText.Text = "Они будут автоматически появляться если навести курсор на любой элемент программы";
        }

        private void ImgStart_MouseEnter(object sender, MouseEventArgs e)
        {
            imgHead.Text = "Начать упражнения";
            imgText.Text = "При нажатии начнется воспроизведение упражнений.\n(УБЕДИТЕСЬ что вы измерили свой голосовой диапазон перед этим).";
        }

        private void ImgStop_MouseEnter(object sender, MouseEventArgs e)
        {
            imgHead.Text = "Закончить упражнения";
            imgText.Text = "При нажатии вы закончите все упражнения.";
        }

        private void ImgMicrpophone_MouseEnter(object sender, MouseEventArgs e)
        {
            imgHead.Text = "Настройки";
            imgText.Text = "Здесь вы сможете выбрать микрофон и определить голосовой диапазон.";
        }

        private void ScrollViewer_MouseEnter(object sender, MouseEventArgs e)
        {
            imgHead.Text = "MIDI фортепиано";
            imgText.Text = "Эмуляция настоящего фортепиано. Просто кликните на любую клавишу и убедитесь сами.";
        }

        private void ImgPrev_MouseEnter(object sender, MouseEventArgs e)
        {
            imgHead.Text = "Предыдущее упражнение";
            imgText.Text = "Перейти к предыдущему упражнению.";
        }

        private void ImgNext_MouseEnter(object sender, MouseEventArgs e)
        {
            imgHead.Text = "Следующее упражнение";
            imgText.Text = "Перейти к следующему упражнению.";
        }

        private void ComboBox_MouseEnter(object sender, MouseEventArgs e)
        {
            imgHead.Text = "Выбор устройства записи";
            imgText.Text = "Выберите микрофон который вы хотите подключить.";
        }

        private void Button_Start_MouseEnter(object sender, MouseEventArgs e)
        {
            imgHead.Text = "Старт";
            imgText.Text = "После нажатия кнопки постарайтесь взять самую низкую/высокую удобную для вас ноту(Выключать не обязательно)";
        }

        private void Button_Clear(object sender, RoutedEventArgs e)
        {
            max_value = 0;
            min_value = 99999999;
        }

        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            imgHead.Text = "Сброс";
            imgText.Text = "Сбросить значения мин./макс. частот";
        }

        private void AcceptButton_MouseEnter(object sender, MouseEventArgs e)
        {
            imgHead.Text = "Подтвердить";
            imgText.Text = "Подтверждение мин./макс. значений и настройка упражнений под ваш диапазон.";
        }

        private void ImgNext_Copy_MouseEnter(object sender, MouseEventArgs e)
        {
            imgHead.Text = "Следующая последовательность";
            imgText.Text = "На случай если ноты не хотят отмечаться как <выполненные>";
        }

        private void Drag_Completed(object sender, EventArgs e)
        {
            sliderValue.Text = ($"{Math.Round(MySlider.Value, 0)}");
            SetVolume(Convert.ToInt32(MySlider.Value));
        }

        public void SetVolume(int level) //Изменяем громкость микшера
        {
            MMDeviceEnumerator MMDE = new MMDeviceEnumerator();

            MMDeviceCollection DevCol = MMDE.EnumerateAudioEndPoints(DataFlow.All, DeviceState.All);

            foreach (MMDevice dev in DevCol)
            {
                if (dev.State == DeviceState.Active)
                {
                    var newVolume = (float)Math.Max(Math.Min(level, 100), 0) / (float)100;

                    dev.AudioEndpointVolume.MasterVolumeLevelScalar = newVolume;

                    dev.AudioEndpointVolume.Mute = level == 0;
                }
            }
        }

        private void ApplicationExit(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Svernut(object sender, EventArgs e)
        {
            this.Hide();

            System.Windows.Forms.NotifyIcon ni = new System.Windows.Forms.NotifyIcon();
            ni.Icon = new System.Drawing.Icon("../../image/Midi.ico");
            ni.Visible = true;
            ni.Text = "Vocal Trainer\nКликните что бы открыть приложение";
            ni.Click += (object send, EventArgs f) =>
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                ni.Dispose();
            };
        }

        private void MySlider_MouseEnter(object sender, MouseEventArgs e)
        {
            imgHead.Text = "Громкость";
            imgText.Text = "Передвиньте ползунок для изменения громкости";
        }

        #endregion

        #region Упражнения

        public void ExerciseOne(int i, int ExerciseNumber)
        {
            string currentNote = "";
            int currentKey;

            CurrentAnswers = 0;

            try
            {
                waveIn_Stop();
            }
            catch(Exception e)
            {

            }

            IEnumerable<TextBlock> collection = KeysGrid.Children.OfType<TextBlock>();

            foreach (TextBlock textBlock in collection)
            {
                if (textBlock.Text.Contains("#") == true)
                    textBlock.Background = Brushes.Black;
                else
                    textBlock.Background = Brushes.White;
            }

                if (ExerciseNumber == 1) //1 упражнение
                {
                    for (int move = 0; move < 5; move++)
                    {
                        try
                        {
                            if (move < 3)
                            {
                                currentNote = assocNotesList.Values.ElementAt(i);
                                currentKey = assocNotesList.Keys.ElementAt(i);
                            }
                            else if(move == 3)
                            {
                                currentNote = assocNotesList.Values.ElementAt(i - 2);
                                currentKey = assocNotesList.Keys.ElementAt(i - 2);
                            }
                            else if(move == 4)
                            {
                                currentNote = assocNotesList.Values.ElementAt(i - 4);
                                currentKey = assocNotesList.Keys.ElementAt(i - 4);
                            }

                            queue.Enqueue(currentNote);

                            _sintezator.PlayTone(currentNote);

                            System.Threading.Thread.Sleep(500);

                            highlightMyKey(currentNote);
                        }
                        catch { }

                        i++;
                    }
                i -= 2;
                textExersice.Content = ExerciseNumber;
                    if (messageChecked == false)
                    {
                        messageChecked = true;
                        MessageBox.Show("Упражнение 1: Упражнение на вибрацию." +
                        "\nСожмите губы вместе. Слегка вытяните вперед так, что бы при выдохе они вибрировали и издавали звук *брр*" +
                        "\nВыполнять упражнение стоит в последовательности 1-2-3-2-1, где 1 - первая нота, а " +
                        "3 - последняя.\nУдерживайте ноту на протяжении 1 секудны, что бы правильное взятие ноты было засчитано.");
                    }
                }

            if(ExerciseNumber == 2)
            {
                for (int move = 0; move < 5; move++)
                {
                    try
                    {
                            currentNote = assocNotesList.Values.ElementAt(i);
                            currentKey = assocNotesList.Keys.ElementAt(i);

                        queue.Enqueue(currentNote);

                        _sintezator.PlayTone(currentNote);

                        System.Threading.Thread.Sleep(500);

                        highlightMyKey(currentNote);
                    }
                    catch { }

                    i++;
                }
                textExersice.Content = ExerciseNumber;
                if (messageChecked == false)
                {
                    messageChecked = true;
                    MessageBox.Show("Упражнение 2: Упражнение на вибрацию.\nСожмите губы вместе. Слегка вытяните вперед так, что бы при выдохе они вибрировали и издавали звук *брр*\nУпражнение так же можно выполнить без вибрации на звук *МИ-МИ-МИ*. Произносить стоит громки и четко.\nВыполнять упражнение стоит в последовательности 1-2-3-4-5, где 1 - первая нота, а 5 - последняя.\nУдерживайте ноту на протяжении 1 секудны, что бы правильное взятие ноты было засчитано.");
                }
            }

            if (ExerciseNumber == 3)
            {
                for (int move = 0; move <= 5; move++)
                {
                    try
                    {
                        if (move == 1)
                        {
                            currentNote = assocNotesList.Values.ElementAt(i);
                            currentKey = assocNotesList.Keys.ElementAt(i);
                        }
                        else if(move == 2)
                        {
                            currentNote = assocNotesList.Values.ElementAt(i + 1);
                            currentKey = assocNotesList.Keys.ElementAt(i + 1);
                        }
                        else if(move == 3)
                        {
                            currentNote = assocNotesList.Values.ElementAt(i - 1);
                            currentKey = assocNotesList.Keys.ElementAt(i - 1);
                        }
                        else if(move == 4)
                        {
                            currentNote = assocNotesList.Values.ElementAt(i);
                            currentKey = assocNotesList.Keys.ElementAt(i);
                        }
                        else if(move == 5)
                        {
                            currentNote = assocNotesList.Values.ElementAt(i - 3);
                            currentKey = assocNotesList.Keys.ElementAt(i - 3);
                        }

                        queue.Enqueue(currentNote);

                        _sintezator.PlayTone(currentNote);

                        System.Threading.Thread.Sleep(500);

                        highlightMyKey(currentNote);
                    }
                    catch { }

                    i++;
                }
                textExersice.Content = ExerciseNumber;
                if (messageChecked == false)
                {
                    messageChecked = true;
                    MessageBox.Show("Упражнение 3: Упражнение на вибрацию.\nСожмите губы вместе. Слегка вытяните вперед так, что бы при выдохе они вибрировали и издавали звук *брр*\nВыполнять упражнение стоит в последовательности 1-3-2-4-2, где 1 - первая нота, а 4 - последняя.\nУдерживайте ноту на протяжении 1 секудны, что бы правильное взятие ноты было засчитано.");
                }
            }
            waveIn_startRecording();
        }

        

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ExerciseNumber = 1;
            messageChecked = false;
            ContinueExercise();
        }

        private void ContinueExercise()
        {
            if (GlobalIndex*3 >= assocNotesList.Count) { ExerciseNumber++; MessageBox.Show("Упражнение закончено. Переходим к следующему"); messageChecked = false; }
            if(ExerciseNumber > 3) { MessageBox.Show("Поздравляем! Вы выполнили все упражнения!"); }
            else ExerciseOne(GlobalIndex, ExerciseNumber);
        }

        private void ImgNext_MouseDown(object sender, MouseButtonEventArgs e)
        {
            messageChecked = false;
            IEnumerable<TextBlock> collection = KeysGrid.Children.OfType<TextBlock>();

            foreach (TextBlock textBlock in collection)
            {
                if (textBlock.Text.Contains("#") == true)
                    textBlock.Foreground = Brushes.White;
                else
                    textBlock.Foreground = Brushes.Black;
            }

                ExerciseNumber++;
            GlobalIndex = 0;
            if (ExerciseNumber > 3) ExerciseNumber = 3;
            ContinueExercise();
        }

        private void ImgPrev_MouseDown(object sender, MouseButtonEventArgs e)
        {
            messageChecked = false;
            IEnumerable<TextBlock> collection = KeysGrid.Children.OfType<TextBlock>();

            foreach (TextBlock textBlock in collection)
            {
                if (textBlock.Text.Contains("#") == true)
                    textBlock.Foreground = Brushes.White;
                else
                    textBlock.Foreground = Brushes.Black;
            }

                ExerciseNumber--;
            GlobalIndex = 0;
            if (ExerciseNumber > 3) ExerciseNumber = 3;
            ContinueExercise();
        }

        private void ImgNext_Copy_MouseDown(object sender, MouseButtonEventArgs e)
        {
            IEnumerable<TextBlock> collection = KeysGrid.Children.OfType<TextBlock>();

            foreach (TextBlock textBlock in collection)
            {
                if (textBlock.Text.Contains("#") == true)
                    textBlock.Foreground = Brushes.White;
                else
                    textBlock.Foreground = Brushes.Black;
            }
                CurrentAnswers = 5;
        }

        #endregion
    }
}
