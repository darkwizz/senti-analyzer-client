using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SentimentAnalyzerClient
{
    class ModelDescription
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }

    class WordSentimentInfo
    {
        public string Token { get; set; }
        public double Sentiment { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Dictionary<string, Brush> _colors = new Dictionary<string, Brush>();
        private Dictionary<string, string> _models = new Dictionary<string, string>();
        private static string SERVER_URL = "https://senti-api-server.herokuapp.com";
        private static string GET_AVAILABLE_MODELS_PART = "/api/models";

        public MainWindow()
        {
            InitializeComponent();
            _LoadAvailableModels();

            _colors.Add("negative", Brushes.Red);
            _colors.Add("negative-text", Brushes.LightCoral);
            _colors.Add("neutral", Brushes.Gold);
            _colors.Add("positive", Brushes.ForestGreen);
            _colors.Add("positive-text", Brushes.LightGreen);
        }

        private void _LoadAvailableModels()
        {
            var request = (HttpWebRequest)WebRequest.Create(SERVER_URL + GET_AVAILABLE_MODELS_PART);
            request.ContentType = "application/json";
            var response = (HttpWebResponse)request.GetResponse();
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                var result = reader.ReadToEnd();
                List<ModelDescription> models = JsonConvert.DeserializeObject<List<ModelDescription>>(result);
                foreach (var model in models)
                {
                    _models.Add(model.Name, model.Url);
                    cbModels.Items.Add(model.Name);
                }
                if (models.Count > 0)
                {
                    cbModels.SelectedIndex = 0;
                }
            }
        }

        private void btnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            var range = new TextRange(rtbAnalyzerText.Document.ContentStart,
                rtbAnalyzerText.Document.ContentEnd);
            range.ApplyPropertyValue(TextElement.BackgroundProperty, null);

            string text = range.Text.Trim();
            string url = SERVER_URL + _models[cbModels.SelectedValue.ToString()];
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";

            using (var writer = new StreamWriter(request.GetRequestStream()))
            {
                string json = string.Format("{{\"text\":\"{0}\"}}", text);
                writer.Write(json);
                writer.Flush();
            }

            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException)
            {
                MessageBox.Show("No input text");
            }
            if (response == null)
            {
                return;
            }

            string result;
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                result = reader.ReadToEnd();
            }
            if (response.StatusCode == HttpStatusCode.OK)
            {
                List<WordSentimentInfo> infos = JsonConvert.DeserializeObject<List<WordSentimentInfo>>(result);

                double sentimentSum = 0;
                foreach (var info in infos)
                {
                    sentimentSum += info.Sentiment;
                    int tokenSentiment = (int)(info.Sentiment * 100);
                    if (tokenSentiment < 40 || tokenSentiment > 60)
                    {
                        TextRange whole = new TextRange(rtbAnalyzerText.Document.ContentStart,
                            rtbAnalyzerText.Document.ContentEnd);
                        TextRange colored = _FindTextInRange(whole, info.Token);
                        if (tokenSentiment < 40)
                        {
                            colored.ApplyPropertyValue(TextElement.BackgroundProperty, _colors["negative-text"]);
                        }
                        else
                        {
                            colored.ApplyPropertyValue(TextElement.BackgroundProperty, _colors["positive-text"]);
                        }
                    }
                }

                int resultSentiment = (int)(sentimentSum / infos.Count * 100);
                pbTextSentiment.Value = resultSentiment;
                Brush brush = _colors["neutral"];
                if (resultSentiment < 40)
                {
                    brush = _colors["negative"];
                }
                else if (resultSentiment > 60)
                {
                    brush = _colors["positive"];
                }
                pbTextSentiment.Foreground = brush;
            }
        }

        /// <summary>
        /// 
        /// Taken from StackOverflow (https://stackoverflow.com/questions/22229741/wpf-richtextbox-application-find-text-spanning-multiple-runs)
        /// because it's 4 hours before ddl. Huge "thank you" for that guy
        /// 
        /// </summary>
        /// <param name="range"></param>
        /// <param name="searchText"></param>
        /// <returns></returns>
        private TextRange _FindTextInRange(TextRange range, string searchText)
        {
            int offset = range.Text.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
            if (offset < 0)
                return null;  // Not found

            var start = _GetTextPositionAtOffset(range.Start, offset);
            TextRange result = new TextRange(start, _GetTextPositionAtOffset(start, searchText.Length));

            return result;
        }

        /// <summary>
        /// 
        /// This method too
        /// 
        /// </summary>
        /// <param name="position"></param>
        /// <param name="characterCount"></param>
        /// <returns></returns>
        private TextPointer _GetTextPositionAtOffset(TextPointer position, int characterCount)
        {
            while (position != null)
            {
                if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    int count = position.GetTextRunLength(LogicalDirection.Forward);
                    if (characterCount <= count)
                    {
                        return position.GetPositionAtOffset(characterCount);
                    }

                    characterCount -= count;
                }

                TextPointer nextContextPosition = position.GetNextContextPosition(LogicalDirection.Forward);
                if (nextContextPosition == null)
                    return position;

                position = nextContextPosition;
            }

            return position;
        }
    }
}
