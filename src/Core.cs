using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MaskMitra {
    enum MarkKind { Hide, Arrow, Highlight, Note, Step }
    sealed class Mark {
        public MarkKind Kind; public RectangleF Box; public PointF Start, End; public string Text = ""; public int Number;
        public Mark Clone() { return (Mark)MemberwiseClone(); }
    }
    sealed class Word {
        public string Text; public RectangleF Box;
        public Word(string text, RectangleF box) { Text=text; Box=box; }
    }
    sealed class TextLine { public List<Word> Words=new List<Word>(); }
    sealed class Suggestion {
        public string Kind, Value; public RectangleF Box; public int Decision; // 0 review, 1 hide, 2 keep
        public Suggestion Clone() { return (Suggestion)MemberwiseClone(); }
    }
    sealed class State {
        public List<Mark> Marks; public List<Suggestion> Suggestions;
    }
    sealed class Document : IDisposable {
        public Bitmap Original; public string SourcePath; public int Version;
        public List<Mark> Marks=new List<Mark>(); public List<Suggestion> Suggestions=new List<Suggestion>();
        readonly Stack<State> undo=new Stack<State>();
        public bool CanUndo { get { return undo.Count>0; } }
        public void Remember() {
            if(undo.Count>=60) { var recent=undo.Take(59).Reverse().ToArray(); undo.Clear(); foreach(var s in recent)undo.Push(s); }
            undo.Push(new State { Marks=Marks.Select(x=>x.Clone()).ToList(),Suggestions=Suggestions.Select(x=>x.Clone()).ToList() });
        }
        public void Undo() { if(!CanUndo)return; var s=undo.Pop(); Marks=s.Marks; Suggestions=s.Suggestions; }
        public void Load(Bitmap bitmap,string path) { if(Original!=null)Original.Dispose(); Original=bitmap; SourcePath=path; Marks.Clear(); Suggestions.Clear();undo.Clear();Version++; }
        public void Dispose() { if(Original!=null) { Original.Dispose(); Original=null; } }
    }
    static class Detection {
        static readonly Regex email = new Regex(@"[A-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Z0-9-]+(?:\.[A-Z0-9-]+)+",RegexOptions.IgnoreCase);
        static readonly Regex upi = new Regex(@"(?<![\w@])[A-Z0-9._-]{2,}@[A-Z][A-Z0-9]{1,}(?![\w.])",RegexOptions.IgnoreCase);
        static readonly Regex digits = new Regex(@"(?<!\w)\+?\d(?:[\d ()-]*\d)?(?!\w)");
        public static List<Suggestion> Find(IEnumerable<TextLine> lines,string custom,Size size) {
            var found=new List<Suggestion>();
            string[] terms=(custom??"").Split(new[]{';','\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(t=>t.Trim()).Where(t=>t.Length>=2).Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToArray();
            foreach(var line in lines) {
                var text=new StringBuilder(); var spans=new List<Tuple<int,int,Word>>();
                foreach(var w in line.Words) { if(text.Length>0)text.Append(' '); int start=text.Length; text.Append(w.Text); spans.Add(Tuple.Create(start,text.Length,w)); }
                string value=text.ToString(); var used=new List<Tuple<int,int>>();
                Action<int,int,string> add=(start,length,kind)=> {
                    if(found.Count>=500||used.Any(p=>start<p.Item2&&start+length>p.Item1))return;
                    var words=spans.Where(p=>p.Item1<start+length&&p.Item2>start).Select(p=>p.Item3).ToArray();if(words.Length==0)return;
                    RectangleF r=words[0].Box;foreach(var w in words.Skip(1))r=RectangleF.Union(r,w.Box);
                    r.Inflate(Math.Max(3,r.Height*0.16f),Math.Max(3,r.Height*0.16f));r=RectangleF.Intersect(r,new RectangleF(Point.Empty,size));
                    if(r.Width<=0||r.Height<=0)return;
                    string matched=value.Substring(start,length);
                    if(found.Any(s=>{var overlap=RectangleF.Intersect(r,s.Box);return Key(s.Value)==Key(matched)&&overlap.Width*overlap.Height>0.5f*Math.Min(r.Width*r.Height,s.Box.Width*s.Box.Height);}))return;
                    found.Add(new Suggestion { Kind=kind,Value=matched,Box=r });used.Add(Tuple.Create(start,start+length));
                };
                foreach(Match m in email.Matches(value))add(m.Index,m.Length,"Email");
                foreach(Match m in upi.Matches(value))add(m.Index,m.Length,"Possible UPI ID");
                string withoutDates=Regex.Replace(value,@"\b\d{4}-\d{1,2}-\d{1,2}\b",m=>new string(' ',m.Length));
                foreach(Match m in digits.Matches(withoutDates)) {
                    string d=new string(m.Value.Where(char.IsDigit).ToArray());
                    if(d.Length<10||d.Length>19)continue;
                    bool accountContext=Regex.IsMatch(value,@"\b(account|a/c|acct|card|iban)\b",RegexOptions.IgnoreCase);
                    string kind=accountContext?"Possible account / card":(d.Length==10||((d.Length==11||d.Length==12||d.Length==13)&&m.Value.StartsWith("+"))||d.Length==12&&d.StartsWith("91"))?"Possible phone":"Possible ID / account";
                    add(m.Index,m.Length,kind);
                }
                foreach(string term in terms) { int pos=0;while(pos<value.Length) { pos=value.IndexOf(term,pos,StringComparison.OrdinalIgnoreCase);if(pos<0)break;add(pos,term.Length,"Custom text");pos+=term.Length; } }
            }
            return found;
        }
        public static string Key(string s) { return Regex.Replace(s??"",@"\s+","").ToLowerInvariant(); }
    }
    static class Images {
        public const long MaxPixels=24000000;
        public static void Validate(int width,int height) { if(width<1||height<1||width>16000||height>16000||(long)width*height>MaxPixels)throw new InvalidOperationException("Use an image up to 24 megapixels and 16,000 pixels per side."); }
        public static Bitmap CleanCopy(Image source) {
            Validate(source.Width,source.Height);var b=new Bitmap(source.Width,source.Height,PixelFormat.Format24bppRgb);
            using(var g=Graphics.FromImage(b)) { g.Clear(Color.White);g.DrawImage(source,new Rectangle(0,0,b.Width,b.Height)); }return b;
        }
        public static Bitmap Open(string path) {
            if(new FileInfo(path).Length>80*1024*1024)throw new InvalidOperationException("Please choose an image smaller than 80 MB.");
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var src=Image.FromStream(stream,true,true)) {
                Validate(src.Width,src.Height);
                try { if(src.PropertyIdList.Contains(0x112)) { int o=src.GetPropertyItem(0x112).Value[0]; RotateFlipType[] map={RotateFlipType.RotateNoneFlipNone,RotateFlipType.RotateNoneFlipNone,RotateFlipType.RotateNoneFlipX,RotateFlipType.Rotate180FlipNone,RotateFlipType.Rotate180FlipX,RotateFlipType.Rotate90FlipX,RotateFlipType.Rotate90FlipNone,RotateFlipType.Rotate270FlipX,RotateFlipType.Rotate270FlipNone};if(o>0&&o<9)src.RotateFlip(map[o]); } }catch(ArgumentException){}
                return CleanCopy(src);
            }
        }
        public static RectangleF Rect(PointF a,PointF b) { return RectangleF.FromLTRB(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X),Math.Max(a.Y,b.Y)); }
        public static Bitmap Render(Document doc) {
            if(doc.Original==null)throw new InvalidOperationException("Open an image first.");var b=CleanCopy(doc.Original);
            using(var g=Graphics.FromImage(b)) {
                g.SmoothingMode=SmoothingMode.AntiAlias;
                foreach(var mark in doc.Marks.Where(m=>m.Kind!=MarkKind.Hide))DrawMark(g,mark,b.Size);
                // Redactions are always drawn last. No annotation can reveal or paint over hidden content.
                foreach(var s in doc.Suggestions.Where(s=>s.Decision==1))Cover(g,s.Box,b.Size);
                foreach(var mark in doc.Marks.Where(m=>m.Kind==MarkKind.Hide))Cover(g,mark.Box,b.Size);
            }return b;
        }
        public static void Cover(Graphics g,RectangleF r,Size bounds) {
            var box=Rectangle.Intersect(Rectangle.FromLTRB((int)Math.Floor(r.Left),(int)Math.Floor(r.Top),(int)Math.Ceiling(r.Right),(int)Math.Ceiling(r.Bottom)),new Rectangle(Point.Empty,bounds));
            var old=g.SmoothingMode;g.SmoothingMode=SmoothingMode.None;using(var brush=new SolidBrush(Color.FromArgb(24,32,45)))g.FillRectangle(brush,box);g.SmoothingMode=old;
        }
        public static void DrawMark(Graphics g,Mark m,Size size) {
            float scale=Math.Max(1,Math.Min(size.Width,size.Height)/700f);Color ink=Color.FromArgb(219,75,45);
            if(m.Kind==MarkKind.Highlight)using(var br=new SolidBrush(Color.FromArgb(95,255,210,50)))g.FillRectangle(br,m.Box);
            if(m.Kind==MarkKind.Arrow)using(var p=new Pen(ink,Math.Max(3,3*scale)))using(var cap=new AdjustableArrowCap(5,6,true)) {p.CustomEndCap=cap;g.DrawLine(p,m.Start,m.End);}
            if(m.Kind==MarkKind.Note)using(var f=new Font("Segoe UI",Math.Max(17,18*scale),FontStyle.Bold,GraphicsUnit.Pixel)) {
                SizeF t=g.MeasureString(m.Text,f,Math.Max(40,(int)(size.Width-m.Start.X-12)));var rect=new RectangleF(m.Start.X,m.Start.Y,t.Width+14,t.Height+10);
                using(var bg=new SolidBrush(Color.FromArgb(255,241,173)))g.FillRectangle(bg,rect);using(var br=new SolidBrush(Color.FromArgb(32,39,50)))g.DrawString(m.Text,f,br,new RectangleF(m.Start.X+7,m.Start.Y+5,t.Width,t.Height));
            }
            if(m.Kind==MarkKind.Step) {
                float radius=18*scale;using(var br=new SolidBrush(ink))g.FillEllipse(br,m.Start.X-radius,m.Start.Y-radius,radius*2,radius*2);
                using(var f=new Font("Segoe UI",18*scale,FontStyle.Bold,GraphicsUnit.Pixel))using(var sf=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString(m.Number.ToString(),f,Brushes.White,new RectangleF(m.Start.X-radius,m.Start.Y-radius,radius*2,radius*2),sf);
            }
        }
        public static void Save(Bitmap image,string path,string sourcePath) {
            if(!String.IsNullOrEmpty(sourcePath)&&String.Equals(Path.GetFullPath(path),Path.GetFullPath(sourcePath),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Choose a different name to keep the original image safe.");
            // Encode fully before opening the chosen destination. Only rendered pixels go into this PNG.
            using(var stream=new MemoryStream()) {image.Save(stream,ImageFormat.Png);File.WriteAllBytes(path,stream.ToArray());}
        }
    }
}
