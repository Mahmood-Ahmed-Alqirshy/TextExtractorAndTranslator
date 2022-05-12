using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace TextExtractorAndTranslator
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        public class GoogleTranslateWebsite : WebBrowser
        {
            private string theWord = "";

            private bool isloadSuccessfully() 
            { 
                return DocumentText.Contains("Google"); 
            }

            public GoogleTranslateWebsite()
            {
                Navigate("https://translate.google.com/?hl=ar&sl=en&tl=ar&op=translate");
            }

            public void setToTranslate(string text)
            {

                theWord = text;
                if (!isloadSuccessfully())
                    return;
                HtmlElementCollection textareas = Document.GetElementsByTagName("textarea");
                if (textareas.Count <= 0)
                    return;

                for(int i = 0; i < textareas.Count; i++)
                    if (textareas[i].GetAttribute("className") == "er8xn")
                        textareas[i].InnerText = text.Trim();
            }

            public string getToTranslate()
            {
                if (!isloadSuccessfully())
                    return "";
                HtmlElementCollection textareas = Document.GetElementsByTagName("textarea");
                if (textareas.Count <= 0)
                    return "";
                for (int i = 0; i < textareas.Count; i++)
                    if (textareas[i].GetAttribute("className") == "er8xn")
                        return (textareas[i].InnerText ?? "").Trim();
                return "";
            }

            public string getMeaning()
            {
                if (!isloadSuccessfully())
                    return getMeaningFromDatabase(theWord);

                HtmlElementCollection allSpans = Document.GetElementsByTagName("span");

                List<HtmlElement> meaning = new List<HtmlElement>();
                foreach (HtmlElement span in allSpans)
                    if (span.GetAttribute("className") == "Q4iAWc" || span.GetAttribute("className") == "kgnlhe")
                        meaning.Add(span);
                
                if (meaning.Count <= 0)
                    return getMeaningFromDatabase(theWord);

                string data = "";
                foreach (HtmlElement mean in meaning)
                    data += mean.InnerText + ",";

                if (data.Length > 0)
                {
                    string result = "";
                    string[] words = data.Remove(data.Length - 1).Split(',').Distinct().ToArray();
                    for (int i = 0; i < words.Length; i++)
                        result += (i == 0) ? words[i] + ((words.Length > 1) ? ".\n" : ".") : (i == words.Length - 1) ? words[i] + "." : words[i] + ", ";
                    return result;
                }
                else
                    return "";
            }
        }

        string extractPDFText(string PDFPath)
        {
            StringBuilder sb = new StringBuilder();
            using (iTextSharp.text.pdf.PdfReader reader = new iTextSharp.text.pdf.PdfReader(PDFPath))
            {
                for (int pageNo = 1; pageNo <= reader.NumberOfPages; pageNo++)
                {
                    iTextSharp.text.pdf.parser.ITextExtractionStrategy strategy = new iTextSharp.text.pdf.parser.SimpleTextExtractionStrategy();
                    string text = iTextSharp.text.pdf.parser.PdfTextExtractor.GetTextFromPage(reader, pageNo, strategy);
                    text = Encoding.UTF8.GetString(ASCIIEncoding.Convert(Encoding.Default, Encoding.UTF8, Encoding.Default.GetBytes(text)));
                    sb.Append(text);
                }
            }
            return sb.ToString();
        }

        string extractIMGText(string IMGPath)
        {
            using (var api = Patagames.Ocr.OcrApi.Create())
            {
                api.Init(Patagames.Ocr.Enums.Languages.English);
                return api.GetTextFromImage(IMGPath);
            }
        }

        static string getMeaningFromDatabase(string word)
        {
            string modifiyWord = "";
            for (int i = 0; i < word.Length; i++)
                modifiyWord += (i == 0) ? word[i].ToString().ToUpper() : word[i].ToString().ToLower();
            using (System.Data.SQLite.SQLiteConnection connection = new System.Data.SQLite.SQLiteConnection("Data Source=data.sqlite"))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = $"SELECT * FROM 'engAraDictionary' WHERE eng = \"{modifiyWord}\";";

                using (System.Data.SQLite.SQLiteDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        reader.Read();
                        return reader.GetString(2);
                    }
                    else
                        return "Not found you have to connect to the internet";
                }
            }
        }

        Dictionary<string, string> translatedWords = new Dictionary<string, string>();
        void addToDictionary(string wordOnlineSource, string meaningOnlineSource, bool onGoogle = true, string wordOfflineSource = "", string meaningOfflineSource = "")
        {
            string word = (onGoogle) ? wordOnlineSource : wordOfflineSource;
            string meaning = (onGoogle) ? meaningOnlineSource : meaningOfflineSource;
            if ((word == "") || meaning == "")
                return;

            for (int i = 0; i < translatedWords.Count; i++)
            {
                if (word == translatedWords.ElementAt(i).Key)
                {
                    if (meaning.Length != translatedWords.ElementAt(i).Value.Length && onGoogle)
                    {
                        translatedWords.Remove(translatedWords.ElementAt(i).Key);
                        translatedWords.Add(word, meaning);
                    }
                    return;
                }
            }
            if(translatedWords.Count > 0)
                if (meaning == translatedWords.ElementAt(translatedWords.Count - 1).Value)
                {
                    translatedWords.Add(word, getMeaningFromDatabase(word));
                    return;
                }
            translatedWords.Add(word, meaning);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Maximized;

            string theWord = "";
            int fontSize = 18;

            ToolStripContainer toolStripContainer = new ToolStripContainer();
            toolStripContainer.Dock = DockStyle.Fill;
            MenuStrip menuStrip = new MenuStrip();
            this.Controls.Add(toolStripContainer);

            ToolStripProgressBar progress = new ToolStripProgressBar();
            progress.Maximum = 100;
            progress.Style = ProgressBarStyle.Blocks;
            toolStripContainer.TopToolStripPanel.Controls.Add(menuStrip);
            progress.Alignment = ToolStripItemAlignment.Left;
            progress.AutoSize = false;
            progress.Width = 614;
            progress.Height = 20;

            Panel Translator = new Panel();
            Translator.Width = Screen.PrimaryScreen.Bounds.Width / 2;
            toolStripContainer.ContentPanel.Controls.Add(Translator);
            Translator.Dock = DockStyle.Right;

            GoogleTranslateWebsite website = new GoogleTranslateWebsite();
            website.Dock = DockStyle.Fill;

            RichTextBox TranslateTo = new RichTextBox();
            TranslateTo.Dock = DockStyle.Fill;
            TranslateTo.RightToLeft = RightToLeft.Yes;
            TranslateTo.ReadOnly = true;
            TranslateTo.BackColor = Color.White;
            TranslateTo.Font = new Font("Arial", fontSize);

            Translator.Controls.Add(website);
            bool onGoogle = true;

            RichTextBox page = new RichTextBox();
            toolStripContainer.ContentPanel.Controls.Add(page);
            page.Font = new Font("Arial", fontSize);
            page.Dock = DockStyle.Left;
            page.Width = Screen.PrimaryScreen.Bounds.Width / 2;
            page.MouseUp += delegate {

                if (page.SelectedText.Trim() != "")
                {
                    theWord = page.SelectedText.Trim();
                    if (onGoogle)
                        website.setToTranslate(page.SelectedText.Trim());
                    else
                        TranslateTo.Text = getMeaningFromDatabase(theWord);
                }
                progress.Value = 0;
            };
            page.MouseDown += delegate
            {
                if(theWord != "")
                    addToDictionary(website.getToTranslate(), website.getMeaning(), onGoogle, theWord, getMeaningFromDatabase(theWord));
            };

            ToolStripItem offline = new ToolStripMenuItem("Offline");
            offline.Alignment = ToolStripItemAlignment.Right;
            offline.Click += delegate
            {
                if (theWord != "")
                    addToDictionary(website.getToTranslate(), website.getMeaning(), onGoogle, theWord, getMeaningFromDatabase(theWord));
                if (onGoogle)
                {
                Translator.Controls.Remove(website);
                Translator.Controls.Add(TranslateTo);
                }
                onGoogle = false;

            };
            menuStrip.Items.Add(offline);

            ToolStripItem google = new ToolStripMenuItem("Google");
            google.Alignment = ToolStripItemAlignment.Right;
            google.Click += delegate
            {
                if (theWord != "")
                    addToDictionary(website.getToTranslate(), website.getMeaning(), onGoogle, theWord, getMeaningFromDatabase(theWord));
                if (!onGoogle)
                {
                Translator.Controls.Remove(TranslateTo);
                Translator.Controls.Add(website);
                }
                onGoogle = true;
            };
            menuStrip.Items.Add(google);

            ToolStripItem fromPDF = new ToolStripMenuItem("extract from PDF");
            menuStrip.Items.Add(fromPDF);
            OpenFileDialog PDFFile = new OpenFileDialog();
            fromPDF.Click += delegate
            {
                    progress.Value = 20;
                    PDFFile.Filter = "PDF Files| *.pdf";
                    if (PDFFile.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            page.Text = extractPDFText(PDFFile.FileName);
                            progress.Value = 100;
                        }
                        catch (Exception ex)
                        {

                            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            progress.Value = 0;
                        }
                    }
                    else
                        progress.Value = 0;
            };

            ToolStripItem fromIMG = new ToolStripMenuItem("extract from Image");
            menuStrip.Items.Add(fromIMG);
            OpenFileDialog IMGFile = new OpenFileDialog();
            fromIMG.Click += delegate
            {
                    progress.Value = 20;
                    if (IMGFile.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            page.Text = extractIMGText(IMGFile.FileName);
                            progress.Value = 100;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            progress.Value = 0;
                        }
                    }
                    else
                        progress.Value = 0;
            };

            ToolStripItem minus = new ToolStripMenuItem("-");
            menuStrip.Items.Add(minus);
            minus.Click += delegate
            {
                if (fontSize > 8)
                {
                    page.Font = new Font("Arial", --fontSize);
                    TranslateTo.Font = new Font("Arial", fontSize);
                }
            };

            ToolStripItem plus = new ToolStripMenuItem("+");
            menuStrip.Items.Add(plus);
            plus.Click += delegate
            {
                if (fontSize < 72)
                {
                    page.Font = new Font("Arial", ++fontSize);
                    TranslateTo.Font = new Font("Arial", fontSize);
                }
            };

            menuStrip.Items.Add(progress);

            FormClosing += delegate
            {
                if (theWord != "")
                    addToDictionary(website.getToTranslate(), website.getMeaning(), onGoogle, theWord, getMeaningFromDatabase(theWord));

                string Data = "";
                foreach (KeyValuePair<string, string> entry in translatedWords)
                {
                    Data += entry.Key + "\n";
                    Data += entry.Value + "\n\n";
                }

                if (Data != "")
                {
                    SaveFileDialog saveFile = new SaveFileDialog();
                    saveFile.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    saveFile.FileName = "Untitled.txt";
                    saveFile.CreatePrompt = false;
                    string savePath = "";
                    if (saveFile.ShowDialog() == DialogResult.OK)
                    {
                        savePath = saveFile.FileName;
                        if (Path.GetExtension(saveFile.FileName).ToLower() != ".txt")
                            savePath += ".txt";
                        using (StreamWriter wordsList = new StreamWriter(savePath))
                            wordsList.WriteLine(Data);
                    }
                }
            };
        }
    }
}
