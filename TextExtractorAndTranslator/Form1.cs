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
            public string theWord { get; private set; } = "";

            private bool isloadSuccessfully() 
            {
                return DocumentTitle.Contains("Google");
            }

            public GoogleTranslateWebsite()
            {
                Navigate("https://translate.google.com/?hl=ar&sl=en&tl=ar&op=translate");
            }

            HtmlElement toTranslateTextarea = null;
            public void setToTranslate(string text)
            {

                theWord = text;
                
                if (!isloadSuccessfully())
                    return;
                
                if(toTranslateTextarea == null)
                {
                    HtmlElementCollection textareas = Document.GetElementsByTagName("textarea");
                    if (textareas.Count <= 0)
                        return;

                    for (int i = 0; i < textareas.Count; i++)
                        if (textareas[i].GetAttribute("className") == "er8xn")
                            toTranslateTextarea = textareas[i];
                }
                toTranslateTextarea.InnerText = text.Trim();

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

        string[] extractPDFText(string PDFPath)
        {
            List<string> pages = new List<string>();
            using (iTextSharp.text.pdf.PdfReader reader = new iTextSharp.text.pdf.PdfReader(PDFPath))
            {
                for (int pageNo = 1; pageNo <= reader.NumberOfPages; pageNo++)
                {
                    iTextSharp.text.pdf.parser.ITextExtractionStrategy strategy = new iTextSharp.text.pdf.parser.SimpleTextExtractionStrategy();
                    string text = iTextSharp.text.pdf.parser.PdfTextExtractor.GetTextFromPage(reader, pageNo, strategy);
                    text = Encoding.UTF8.GetString(ASCIIEncoding.Convert(Encoding.Default, Encoding.UTF8, Encoding.Default.GetBytes(text)));
                    pages.Add(text);
                }
            }
            return pages.ToArray();
        }

        object extractIMGText(string IMGPath)
        {
            try
            {
                using (var api = Patagames.Ocr.OcrApi.Create())
                {
                    api.Init(Patagames.Ocr.Enums.Languages.English);
                    return new { success = true, contant = api.GetTextFromImage(IMGPath) };
                }
            }
            catch (Exception ex)
            {
                return new { success = false,  contant = ex.Message };
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
        RichTextBox page = new RichTextBox();
        RichTextBox TranslateTo = new RichTextBox();
        GoogleTranslateWebsite website = new GoogleTranslateWebsite();
        ToolStripProgressBar progress = new ToolStripProgressBar();
        string theWord = "";
        bool onGoogle = true;
        Panel Translator = new Panel();
        OpenFileDialog IMGFile = new OpenFileDialog();
        OpenFileDialog PDFFile = new OpenFileDialog();
        int fontSize = 18;
        ToolStripItem previousPage = new ToolStripMenuItem("<");
        ToolStripTextBox pageNumber = new ToolStripTextBox();
        ToolStripItem nextPage = new ToolStripMenuItem(">");
        string[] pages;
        int currentPage = 0;


        private void Form1_Load(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Maximized;

            ToolStripContainer toolStripContainer = new ToolStripContainer();
            toolStripContainer.Dock = DockStyle.Fill;
            MenuStrip topMenuStrip = new MenuStrip();
            MenuStrip bottomMenuStrip = new MenuStrip();
            this.Controls.Add(toolStripContainer);

            progress.Maximum = 100;
            progress.MarqueeAnimationSpeed = 5;
            toolStripContainer.TopToolStripPanel.Controls.Add(topMenuStrip);
            toolStripContainer.BottomToolStripPanel.Controls.Add(bottomMenuStrip);
            progress.Alignment = ToolStripItemAlignment.Right;
            progress.AutoSize = false;
            progress.Width = 700;

            Translator.Width = Screen.PrimaryScreen.Bounds.Width / 2;
            toolStripContainer.ContentPanel.Controls.Add(Translator);
            Translator.Dock = DockStyle.Right;

            website.Dock = DockStyle.Fill;

            TranslateTo.Dock = DockStyle.Fill;
            TranslateTo.RightToLeft = RightToLeft.Yes;
            TranslateTo.ReadOnly = true;
            TranslateTo.BackColor = Color.White;
            TranslateTo.Font = new Font("Arial", fontSize);
            Translator.Controls.Add(website);

            toolStripContainer.ContentPanel.Controls.Add(page);
            page.Font = new Font("Arial", fontSize);
            page.Dock = DockStyle.Left;
            page.Width = Screen.PrimaryScreen.Bounds.Width / 2;
            page.MouseUp += setWordToTranslate;
            page.MouseDown += addToDictionary;

            ToolStripItem minus = new ToolStripMenuItem("-");
            bottomMenuStrip.Items.Add(minus);
            minus.AutoSize = false;
            minus.Width = 100;
            minus.Height = 30;
            minus.Font = new Font("Arial", 10);
            minus.Click += fontSizeDecrement;

            ToolStripItem plus = new ToolStripMenuItem("+");
            bottomMenuStrip.Items.Add(plus);
            plus.AutoSize = false;
            plus.Width = 100;
            plus.Height = 30;
            plus.Font = new Font("Arial", 10);
            plus.Click += fontSizeIncrement;
  
            bottomMenuStrip.Items.Add(previousPage);
            previousPage.Enabled = false;
            previousPage.AutoSize = false;
            previousPage.Width = 200;
            previousPage.Height = 30;
            previousPage.Font = new Font("Arial", 10);
            previousPage.Click += goToPreviousPage;

            bottomMenuStrip.Items.Add(pageNumber);
            pageNumber.Enabled = false;
            pageNumber.TextBox.TextAlign = HorizontalAlignment.Center;
            pageNumber.Leave += pageNumberLeave;
            pageNumber.KeyDown += pageNumberEnterd;

            bottomMenuStrip.Items.Add(nextPage);
            nextPage.Enabled = false;
            nextPage.AutoSize = false;
            nextPage.Width = 200;
            nextPage.Height = 30;
            nextPage.Font = new Font("Arial", 10);
            nextPage.Click += goToNextPage;


            ToolStripItem offline = new ToolStripMenuItem("Offline");
            offline.Alignment = ToolStripItemAlignment.Right;
            offline.Click += addToDictionary;
            offline.Click += switchFromAndToOflineOrOnline;
            topMenuStrip.Items.Add(offline);

            ToolStripItem google = new ToolStripMenuItem("Google");
            google.Alignment = ToolStripItemAlignment.Right;
            google.Click += addToDictionary;
            google.Click += switchFromAndToOflineOrOnline;
            topMenuStrip.Items.Add(google);

            ToolStripItem fromPDF = new ToolStripMenuItem("extract from PDF");
            topMenuStrip.Items.Add(fromPDF);
            fromPDF.Click += extractFromPDF;

            ToolStripItem fromIMG = new ToolStripMenuItem("extract from Image");
            topMenuStrip.Items.Add(fromIMG);
            fromIMG.Click += extractFromIMG;

            bottomMenuStrip.Items.Add(progress);
            progress.Height = 20;

            FormClosing += addToDictionary;
            FormClosing += FormClosingEvent;
        }

        private void pageNumberEnterd(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                pageNumberLeave(sender, e);
        }

        private void pageNumberLeave(object sender, EventArgs e)
        {
                ToolStripTextBox textBox = (ToolStripTextBox)sender;
                string input = "0" + string.Join("", textBox.TextBox.Text.ToCharArray().Where((c) => char.IsDigit(c)).ToArray());

                if (Convert.ToInt32(input) < 1)
                    currentPage = 1;
                else if (Convert.ToInt32(input) > pages.Length)
                    currentPage = pages.Length;
                else
                    currentPage = Convert.ToInt32(input);

                page.Text = pages[currentPage - 1];
                textBox.TextBox.Text = currentPage.ToString();
        }

        private void goToNextPage(object sender, EventArgs e)
        {
            if (currentPage < pages.Length)
            {
                page.Text = pages[++currentPage - 1];
                pageNumber.Text = currentPage.ToString();
            }
        }

        private void goToPreviousPage(object sender, EventArgs e)
        {
            if(currentPage > 1)
            {
                page.Text = pages[--currentPage - 1];
                pageNumber.Text = currentPage.ToString();
            }
        }

        private void fontSizeIncrement(object sender, EventArgs e)
        {
            if (fontSize < 72)
            {
                page.Font = new Font("Arial", ++fontSize);
                TranslateTo.Font = new Font("Arial", fontSize);
            }
        }

        private void fontSizeDecrement(object sender, EventArgs e)
        {
            if (fontSize > 8)
            {
                page.Font = new Font("Arial", --fontSize);
                TranslateTo.Font = new Font("Arial", fontSize);
            }
        }

        private async void extractFromPDF(object sender, EventArgs e)
        {
            ToolStripMenuItem button = (ToolStripMenuItem)sender;
            PDFFile.Filter = "PDF Files| *.pdf";
            if (PDFFile.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    for(int i = 0; i < button.Owner.Items.Count; i++)
                        if(button.Owner.Items[i].Text.Contains("extract"))
                            button.Owner.Items[i].Enabled = false;
                    progress.Style = ProgressBarStyle.Marquee;
                    pages = await Task.Run(() => extractPDFText(PDFFile.FileName));
                    page.Text = pages[0];
                    currentPage = 1;
                    pageNumber.Text = currentPage.ToString();
                    if (pages.Length > 1)
                        previousPage.Enabled = pageNumber.Enabled = nextPage.Enabled = true;
                }
                catch (Exception ex)
                {

                    progress.Style = ProgressBarStyle.Blocks;
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    for (int i = 0; i < button.Owner.Items.Count; i++)
                        if (button.Owner.Items[i].Text.Contains("extract"))
                            button.Owner.Items[i].Enabled = true;
                    progress.Style = ProgressBarStyle.Blocks;
                }
            }
            
        }

        private async void extractFromIMG(object sender, EventArgs e)
        {
            ToolStripMenuItem button = (ToolStripMenuItem)sender;
            if (IMGFile.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    for (int i = 0; i < button.Owner.Items.Count; i++)
                        if (button.Owner.Items[i].Text.Contains("extract"))
                            button.Owner.Items[i].Enabled = false;
                    progress.Style = ProgressBarStyle.Marquee;
                    dynamic respond = await Task.Run(() => extractIMGText(IMGFile.FileName));
                    if (!respond.success)
                        throw new Exception(respond.contant);
                    page.Text = respond.contant;
                    previousPage.Enabled = pageNumber.Enabled = nextPage.Enabled = false;
                    currentPage = 0;
                    pageNumber.Text = "";
                }
                catch (Exception ex)
                {
                    progress.Style = ProgressBarStyle.Blocks;
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    for (int i = 0; i < button.Owner.Items.Count; i++)
                        if (button.Owner.Items[i].Text.Contains("extract"))
                            button.Owner.Items[i].Enabled = true;
                    progress.Style = ProgressBarStyle.Blocks;
                }
            }
        }

        private void FormClosingEvent(object sender, FormClosingEventArgs e)
        {
          

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
        }

        private void switchFromAndToOflineOrOnline(object sender, EventArgs e)
        {
            if (onGoogle)
            {
                Translator.Controls.Remove(website);
                Translator.Controls.Add(TranslateTo);
                onGoogle = false;
            }
            else
            {
                Translator.Controls.Remove(TranslateTo);
                Translator.Controls.Add(website);
                onGoogle = true;
            }
        }

        private void addToDictionary(object sender, object e)
        {
            string word = theWord;
            if (word != "")
            {
                string meaning = (onGoogle) ? website.getMeaning() : getMeaningFromDatabase(word);

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
                if (translatedWords.Count > 0)
                    if (meaning == translatedWords.ElementAt(translatedWords.Count - 1).Value)
                    {
                        translatedWords.Add(word, getMeaningFromDatabase(word));
                        return;
                    }
                translatedWords.Add(word, meaning);
            }
        }

        private void setWordToTranslate(object sender, MouseEventArgs e)
        {
            //RichTextBox page = (RichTextBox)sender;
            if (page.SelectedText.Trim() != "")
            {
                theWord = page.SelectedText.Replace("\n", " ").Replace("  ", " ").Trim();
                if (onGoogle)
                    website.setToTranslate(page.SelectedText.Replace("\n", " ").Replace("  ", " ").Trim());
                else
                    TranslateTo.Text = getMeaningFromDatabase(theWord);
            }
        }
    }
}
