using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using Windows.Data.Pdf;
using Windows.Storage.Streams;
using Windows.Storage;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Media;
using Windows.Media.Ocr;
using Windows.UI.Xaml.Shapes;
using Windows.UI;
using Windows.UI.Popups;
using System.Diagnostics;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace OCR
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        PdfDocument document = null;
        PdfPage pdfPage = null;
        uint page = 0;

        public MainPage()
        {
            this.InitializeComponent();
        }
        private async void button_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker fileOpenPicker = new FileOpenPicker();
            fileOpenPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            fileOpenPicker.FileTypeFilter.Add(".pdf");
            fileOpenPicker.ViewMode = PickerViewMode.Thumbnail;

            var inputFile = await fileOpenPicker.PickSingleFileAsync();
            document = await PdfDocument.LoadFromFileAsync(inputFile);
            pdfPage = document.GetPage(0);

            RenderPage(pdfPage);
        }

        private async void RenderPage(PdfPage pdfPage)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            canvas.Children.Clear();

            await pdfPage.PreparePageAsync();

            StorageFolder tempFolder = ApplicationData.Current.TemporaryFolder;
            StorageFile jpgFile = await tempFolder.CreateFileAsync(Guid.NewGuid().ToString() + ".png", CreationCollisionOption.GenerateUniqueName);
            PdfPageRenderOptions renderOptions = new PdfPageRenderOptions();
            renderOptions.DestinationHeight = (uint)(pdfPage.Size.Height * 2.0);
            renderOptions.DestinationWidth = (uint)(pdfPage.Size.Width * 2.0);

            canvas.Width = renderOptions.DestinationWidth;
            canvas.Height = renderOptions.DestinationHeight;

            if (jpgFile != null)
            {
                IRandomAccessStream randomStream = await jpgFile.OpenAsync(FileAccessMode.ReadWrite);

                await pdfPage.RenderToStreamAsync(randomStream, renderOptions);
                await randomStream.FlushAsync();

                randomStream.Dispose();
                pdfPage.Dispose();
                //await DisplayImageFileAsync(jpgFile);
            }

            SoftwareBitmap softwareBitmap;
            BitmapImage image = new BitmapImage();
            ImageBrush backgroundBrush = new ImageBrush();

            using (IRandomAccessStream stream = await jpgFile.OpenAsync(FileAccessMode.Read))
            {
                // Create the decoder from the stream
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                image.SetSource(stream);
                backgroundBrush.ImageSource = image;

                // Get the SoftwareBitmap representation of the file
                softwareBitmap = await decoder.GetSoftwareBitmapAsync();
            }

            OcrEngine engine = OcrEngine.TryCreateFromUserProfileLanguages();
            var result = await engine.RecognizeAsync(softwareBitmap);
            textBox.Text = "";
            var left = canvas.GetValue(Canvas.LeftProperty);
            var top = canvas.GetValue(Canvas.TopProperty);
            foreach (OcrLine line in result.Lines)
            {
                Rectangle lineRect = new Rectangle();

                textBox.Text += line.Text + "\r\n";
                foreach (OcrWord word in line.Words)
                {
                    Rectangle r = new Rectangle();
                    r.Margin = new Thickness(word.BoundingRect.Left, word.BoundingRect.Top, 0.0, 0.0);
                    r.Width = word.BoundingRect.Width;
                    r.Height = word.BoundingRect.Height;
                    r.Stroke = new SolidColorBrush(Colors.Blue);
                    canvas.Children.Add(r);
                    canvas.Background = backgroundBrush;
                }
            }

            // Looking for line "Duration of agreement"
            for (int i = 0; i < result.Lines.Count; i++)
            {
                OcrLine line = result.Lines[i];

                if (line.Text.Contains("Customer Name"))
                {
                    clientName.Text = line.Text.Substring(13);
                    //MessageDialog dlg = new MessageDialog(clientName.Text);
                    //await dlg.ShowAsync();
                }
            }

            sw.Stop();
            MessageDialog md = new MessageDialog(string.Format("Page processed in {0} milliseconds", sw.ElapsedMilliseconds));
            await md.ShowAsync();
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            page--;
            if (page < 0) page = 0;

            pdfPage = document.GetPage(page);
            RenderPage(pdfPage);
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            page++;
            if (page >= document.PageCount) page = document.PageCount - 1;

            pdfPage = document.GetPage(page);
            RenderPage(pdfPage);
        }
    }
}
