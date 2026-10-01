using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace MaskMitra {
    sealed class ScanResult { public List<TextLine> Lines=new List<TextLine>();public string Language; }
    static class LocalOcr {
        // Bridge the operating system's WinRT metadata without requiring a Windows SDK install.
        static Task<T> Finish<T>(IAsyncOperation<T> operation) {
            var done=new TaskCompletionSource<T>();
            operation.Completed=(op,status)=>{try{if(status==AsyncStatus.Canceled)done.TrySetCanceled();else if(status==AsyncStatus.Error)done.TrySetException(op.ErrorCode);else done.TrySetResult(op.GetResults());}catch(Exception ex){done.TrySetException(ex);}finally{op.Close();}};
            return done.Task;
        }
        public static async Task<ScanResult> Read(Bitmap source) {
            var engine=OcrEngine.TryCreateFromLanguage(new Language("en-US"))??OcrEngine.TryCreateFromUserProfileLanguages();
            if(engine==null)throw new InvalidOperationException("No Windows OCR language is installed. Add English OCR in Windows language options, then reopen MaskMitra. Manual Hide still works.");
            var output=await ReadRegion(source,engine);
            // Overlapping bands recover lines that the full-page layout analysis can miss.
            if(source.Height>=400)for(int band=0;band<3;band++) {
                int top=Math.Max(0,source.Height*band/3-55),bottom=Math.Min(source.Height,source.Height*(band+1)/3+55);
                using(var crop=source.Clone(new Rectangle(0,top,source.Width,bottom-top),PixelFormat.Format24bppRgb)) {
                    var extra=await ReadRegion(crop,engine);
                    foreach(var line in extra.Lines){foreach(var word in line.Words){var r=word.Box;r.Y+=top;word.Box=r;}output.Lines.Add(line);}
                }
            }
            return output;
        }
        static async Task<ScanResult> ReadRegion(Bitmap source,OcrEngine engine) {
            int max=Math.Min(3200,(int)OcrEngine.MaxImageDimension);float factor=Math.Min(2,Math.Min((float)max/source.Width,(float)max/source.Height));
            int width=Math.Max(1,(int)(source.Width*factor)),height=Math.Max(1,(int)(source.Height*factor));
            byte[] encoded;
            using(var scaled=new Bitmap(width,height))using(var g=Graphics.FromImage(scaled))using(var memory=new MemoryStream()) {g.Clear(Color.White);g.DrawImage(source,new Rectangle(0,0,width,height));scaled.Save(memory,ImageFormat.Png);encoded=memory.ToArray();}
            using(var stream=new InMemoryRandomAccessStream()) {
                using(var writer=new DataWriter(stream.GetOutputStreamAt(0))) {writer.WriteBytes(encoded);await Finish<uint>(writer.StoreAsync());await Finish<bool>(writer.FlushAsync());}
                stream.Seek(0);var decoder=await Finish<BitmapDecoder>(BitmapDecoder.CreateAsync(stream));
                using(var bitmap=await Finish<SoftwareBitmap>(decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied))) {
                    var result=await Finish<OcrResult>(engine.RecognizeAsync(bitmap));
                    if(result.TextAngle.HasValue&&Math.Abs(result.TextAngle.Value)>3)throw new InvalidOperationException("This image has tilted text. Suggestions are paused to avoid misplaced masks. Use Manual Hide or load a straight screenshot.");
                    var output=new ScanResult{Language=engine.RecognizerLanguage.DisplayName};
                    float sx=(float)source.Width/width,sy=(float)source.Height/height;
                    foreach(var line in result.Lines) {var item=new TextLine();foreach(var word in line.Words) {var r=word.BoundingRect;item.Words.Add(new Word(word.Text,new RectangleF((float)r.X*sx,(float)r.Y*sy,(float)r.Width*sx,(float)r.Height*sy)));}output.Lines.Add(item);}return output;
                }
            }
        }
    }
}
