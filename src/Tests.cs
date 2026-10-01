using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MaskMitra {
    static class Tests {
        static List<string> checks=new List<string>();
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add("PASS  "+message);}
        static string Hash(string path){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
        static TextLine Line(params string[] values){var l=new TextLine();float x=15;foreach(string v in values){l.Words.Add(new Word(v,new RectangleF(x,20,v.Length*10,25)));x+=v.Length*10+9;}return l;}
        static T Await<T>(Task<T> task){var until=DateTime.UtcNow.AddSeconds(25);while(!task.IsCompleted){if(DateTime.UtcNow>until)throw new TimeoutException("OCR test exceeded 25 seconds");Application.DoEvents();Thread.Sleep(10);}return task.GetAwaiter().GetResult();}
        static void Await(Task task){var until=DateTime.UtcNow.AddSeconds(25);while(!task.IsCompleted){if(DateTime.UtcNow>until)throw new TimeoutException("UI scan test exceeded 25 seconds");Application.DoEvents();Thread.Sleep(10);}task.GetAwaiter().GetResult();}
        public static Bitmap Demo() {
            var b=new Bitmap(1120,760);using(var g=Graphics.FromImage(b)) {
                g.Clear(Color.FromArgb(242,245,248));
                using(var br=new SolidBrush(Theme.Ink))g.FillRectangle(br,0,0,1120,75);
                using(var f=new Font("Segoe UI",23,FontStyle.Bold))g.DrawString("Northstar Studio",f,Brushes.White,40,18);
                using(var f=new Font("Segoe UI",12))g.DrawString("DEMO WORKSPACE",f,Brushes.White,865,27);
                g.FillRectangle(Brushes.White,34,105,1052,614);
                using(var f=new Font("Segoe UI",27,FontStyle.Bold))using(var br=new SolidBrush(Theme.Ink))g.DrawString("Project handoff",f,br,65,126);
                using(var f=new Font("Segoe UI",14))using(var br=new SolidBrush(Theme.Muted))g.DrawString("A sample screenshot. All details below are fictional.",f,br,68,183);
                using(var p=new Pen(Theme.Line))g.DrawLine(p,68,225,1040,225);
                using(var f=new Font("Segoe UI",19))using(var br=new SolidBrush(Theme.Ink)) {
                    g.DrawString("Contact:  Maya Kapoor",f,br,68,250);
                    g.DrawString("Email:  maya@example.com",f,br,68,308);
                    g.DrawString("Phone:  +91 90000 00000",f,br,68,366);
                    g.DrawString("UPI:  maya@okbank",f,br,68,424);
                    g.DrawString("Account:  1234 5678 9012",f,br,68,482);
                    g.DrawString("Send a copy to maya@example.com",f,br,68,540);
                }
                using(var br=new SolidBrush(Color.FromArgb(229,242,235)))g.FillRectangle(br,68,606,970,67);
                using(var f=new Font("Segoe UI",17,FontStyle.Bold))using(var br=new SolidBrush(Theme.Teal))g.DrawString("Ready for review  •  Design files attached",f,br,90,623);
            }return b;
        }
        static void Shot(Form form,string path){Application.DoEvents();form.PerformLayout();using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(path,ImageFormat.Png);}}
        static void Mouse(Control control,string method,MouseButtons button,int x,int y){control.GetType().GetMethod(method,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(control,new object[]{new MouseEventArgs(button,1,x,y,0)});}
        public static void Run(string folder,bool skipOcr) {
            Directory.CreateDirectory(folder);
            var size=new Size(1500,900);
            var detected=Detection.Find(new[]{Line("maya@example.com","+91","90000","00000","maya@okbank")},"",size);
            Check(detected.Count(s=>s.Kind=="Email")==1,"Email detection does not also classify an email as UPI");
            Check(detected.Count(s=>s.Kind=="Possible phone")==1,"Spaced Indian phone number is detected");
            Check(detected.Count(s=>s.Kind=="Possible UPI ID")==1,"UPI-like text is identified as a suggestion");
            var account=Detection.Find(new[]{Line("Account:","0000","1111","2222","3333")},"",size);
            Check(account.Count==1&&account[0].Kind=="Possible account / card","Account context with grouped digits is detected");
            Check(Detection.Find(new[]{Line("2026-10-01","123","800","760")},"",size).Count==0,"Short numbers and dates are not proposed as phone or account details");
            var repeated=Detection.Find(new[]{Line("Maya","Kapoor","Maya","Kapoor")},"maya kapoor",size);
            Check(repeated.Count==2&&repeated.All(s=>s.Decision==0),"Custom phrase finds repeated matches without hiding anything automatically");
            Check(Detection.Key("+91 90000 00000")==Detection.Key("+919000000000"),"Repeated detected values can be matched across spaces");
            var edged=Detection.Find(new[]{new TextLine{Words=new List<Word>{new Word("a@example.com",new RectangleF(0,0,80,25))}}},"",new Size(80,25));
            Check(edged.Count==1&&edged[0].Box.Left>=0&&edged[0].Box.Right<=80,"Detection padding is clamped to image bounds");
            bool failed=false;try{Images.Validate(10000,10000);}catch(InvalidOperationException){failed=true;}Check(failed,"Oversized images are rejected before edit allocation");
            string original=Path.Combine(folder,"fixture-original.png"),saved=Path.Combine(folder,"fixture-clean.png");
            using(var fixture=new Bitmap(300,150)){using(var g=Graphics.FromImage(fixture)){g.Clear(Color.White);g.FillRectangle(Brushes.Red,29,39,125,48);}fixture.Save(original,ImageFormat.Png);}
            string before=Hash(original);
            using(var doc=new Document()) {
                doc.Load(Images.Open(original),original);doc.Remember();doc.Marks.Add(new Mark{Kind=MarkKind.Hide,Box=new RectangleF(28.2f,38.2f,126.4f,49.4f)});
                doc.Marks.Add(new Mark{Kind=MarkKind.Highlight,Box=new RectangleF(20,30,140,70)});
                using(var render=Images.Render(doc)) {
                    Color cover=Color.FromArgb(24,32,45);bool solid=true;for(int x=29;x<154;x++)for(int y=39;y<87;y++)if(render.GetPixel(x,y).ToArgb()!=cover.ToArgb())solid=false;
                    Check(solid,"Every source pixel in a redaction is replaced, including underneath annotations");
                    Check(render.GetPixel(250,120).ToArgb()==Color.White.ToArgb(),"Unedited pixels retain their content");
                    Images.Save(render,saved,original);Check(Hash(original)==before,"Export does not change the original file");
                    failed=false;try{Images.Save(render,original,original);}catch(InvalidOperationException){failed=true;}Check(failed&&Hash(original)==before,"Export refuses to overwrite the loaded original");
                }
                using(var reload=Images.Open(saved)){Check(reload.Width==300&&reload.Height==150,"PNG round trip retains exact image dimensions");Check(reload.GetPixel(40,50).ToArgb()==Color.FromArgb(24,32,45).ToArgb(),"Saved PNG contains redacted pixels");Check(reload.PropertyIdList.Length==0,"Rendered image has no copied source metadata");}
                doc.Undo();Check(doc.Marks.Count==0,"Undo restores the previous editing state");
                doc.Suggestions.Add(new Suggestion{Kind="Email",Value="a@example.com",Box=new RectangleF(10,10,60,30)});doc.Remember();doc.Suggestions[0].Decision=1;doc.Undo();Check(doc.Suggestions[0].Decision==0,"Undo restores suggestion decisions independently");
            }
            if(!skipOcr) {
            ScanResult scan;
            using(var demo=Demo()){demo.Save(Path.Combine(folder,"demo-original.png"),ImageFormat.Png);scan=Await(LocalOcr.Read(demo));}
            var suggestions=Detection.Find(scan.Lines,"",new Size(1120,760));
            File.WriteAllLines(Path.Combine(folder,"ocr-fixture-text.txt"),scan.Lines.Select(l=>String.Join(" ",l.Words.Select(w=>w.Text))).ToArray());
            File.WriteAllLines(Path.Combine(folder,"ocr-fixture-results.txt"),suggestions.Select(s=>s.Kind+" | "+s.Value+" | "+s.Box).ToArray());
            Check(suggestions.Count(s=>s.Kind=="Email")>=2,"Actual offline Windows OCR recognizes both email occurrences in the demo");
            Check(suggestions.Any(s=>s.Kind=="Possible phone"),"Actual offline OCR finds the demo phone number");
            Check(suggestions.Any(s=>s.Kind=="Possible UPI ID"),"Actual offline OCR finds the demo UPI-like ID");
            Check(suggestions.Any(s=>s.Kind=="Possible account / card"),"Actual offline OCR finds the demo account number");
            using(var form=new MainForm(true)) {
                form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-18000,-18000);form.ShowInTaskbar=false;form.Show();Shot(form,Path.Combine(folder,"empty.png"));
                form.SetImage(Demo(),null,false);Await(form.Scan());Check(form.doc.Suggestions.Count>=5,"App's async scan populates the suggestion list");
                Check(form.doc.Suggestions.All(s=>s.Decision==0),"No automatic hiding occurs after the app scans an image");
                RectangleF display=form.canvas.ImageRect;int x1=(int)(display.Left+display.Width*0.15f),y1=(int)(display.Top+display.Height*0.33f),x2=(int)(display.Left+display.Width*0.5f),y2=(int)(display.Top+display.Height*0.39f);
                Mouse(form.canvas,"OnMouseDown",MouseButtons.Left,x1,y1);Mouse(form.canvas,"OnMouseMove",MouseButtons.Left,x2,y2);Mouse(form.canvas,"OnMouseUp",MouseButtons.Left,x2,y2);
                Check(form.doc.Marks.Count==1&&form.doc.Marks[0].Kind==MarkKind.Hide&&form.doc.Marks[0].Box.Width>300,"A drag on the scaled editor creates a manual redaction in image coordinates");
                form.doc.Undo();form.RefreshDoc(false);Check(form.doc.Marks.Count==0,"Manual redaction made by the editor can be undone");
                int emailIndex=form.doc.Suggestions.FindIndex(s=>s.Kind=="Email");form.suggestions.SelectedIndex=emailIndex;form.Decide(1,true);
                Check(form.doc.Suggestions.Where(s=>s.Kind=="Email").All(s=>s.Decision==1),"Hide matches applies to both detected emails in the editor");
                foreach(var s in form.doc.Suggestions)s.Decision=1;
                form.doc.Marks.Add(new Mark{Kind=MarkKind.Arrow,Start=new PointF(947,535),End=new PointF(840,618)});
                form.doc.Marks.Add(new Mark{Kind=MarkKind.Note,Start=new PointF(720,480),Text="Files are ready"});
                form.doc.Marks.Add(new Mark{Kind=MarkKind.Step,Start=new PointF(1010,639),Number=1});form.RefreshDoc(false);Shot(form,Path.Combine(folder,"edited.png"));
                using(var image=Images.Render(form.doc)) {image.Save(Path.Combine(folder,"demo-clean.png"),ImageFormat.Png);using(var preview=new ExportForm(image,null,0)){preview.StartPosition=FormStartPosition.Manual;preview.Location=new Point(-18000,-18000);preview.ShowInTaskbar=false;preview.Show();Shot(preview,Path.Combine(folder,"export.png"));preview.Close();}}
                form.ClientSize=new Size(1120,740);Shot(form,Path.Combine(folder,"minimum.png"));
                var task=form.Scan();form.SetImage(new Bitmap(200,150),null,false);Await(task);Check(form.doc.Suggestions.Count==0&&form.doc.Original.Width==200,"A stale scan cannot apply old suggestions to a newly loaded image");
                form.Close();
            }
            } else checks.Add("SKIP  OCR and integrated scan UI checks (explicit --skip-ocr; run full tests on Windows with OCR installed)");
            using(var fixture=Demo())using(var capture=new CaptureForm(fixture,new Rectangle(0,0,fixture.Width,fixture.Height))) {
                Mouse(capture,"OnMouseDown",MouseButtons.Left,340,280);Mouse(capture,"OnMouseMove",MouseButtons.Left,70,90);Mouse(capture,"OnMouseUp",MouseButtons.Left,70,90);
                Check(capture.Selection==new Rectangle(70,90,270,190)&&capture.DialogResult==DialogResult.OK,"Capture selection supports dragging backwards and returns exact bounds");
            }
            File.WriteAllLines(Path.Combine(folder,"PASSED.txt"),checks.ToArray());
        }
    }
}
