using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("MaskMitra")]
[assembly: System.Reflection.AssemblyDescription("Offline screenshot privacy and annotation")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace MaskMitra {
    static class Program {
        [STAThread] static int Main(string[] args) {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length>=2&&args[0]=="--self-test") {try{Tests.Run(args[1],args.Contains("--skip-ocr"));return 0;}catch(Exception ex){Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"FAILED.txt"),ex.ToString());return 1;}}
            Application.ThreadException+=(s,e)=>MessageBox.Show("MaskMitra could not finish that action. Your original file has not been changed.\n\n"+e.Exception.Message,"MaskMitra",MessageBoxButtons.OK,MessageBoxIcon.Information);
            Application.Run(new MainForm(false));return 0;
        }
    }
    static class Theme {
        public static readonly Color Ink=Color.FromArgb(24,32,45),Muted=Color.FromArgb(103,114,128),Paper=Color.FromArgb(247,248,250),Line=Color.FromArgb(222,227,233),Teal=Color.FromArgb(24,112,101),Lime=Color.FromArgb(204,241,119);
        public static Button Button(string text,EventHandler action,bool primary=false,int width=110) {
            var b=new Button{Text=text,UseMnemonic=false,Width=width,Height=36,FlatStyle=FlatStyle.Flat,BackColor=primary?Teal:Color.White,ForeColor=primary?Color.White:Ink,Cursor=Cursors.Hand,Margin=new Padding(0,0,7,6),Font=new Font("Segoe UI",9,FontStyle.Bold),UseVisualStyleBackColor=false};b.FlatAppearance.BorderColor=primary?Teal:Line;b.FlatAppearance.MouseOverBackColor=primary?Color.FromArgb(32,132,119):Color.FromArgb(237,242,245);b.Click+=action;return b;
        }
        public static Label Label(string text,int height=28,bool bold=false) {return new Label{Text=text,Dock=DockStyle.Top,Height=height,ForeColor=bold?Ink:Muted,Font=new Font("Segoe UI",bold?10:9,bold?FontStyle.Bold:FontStyle.Regular),TextAlign=ContentAlignment.MiddleLeft};}
    }
    sealed class Brand : Control {
        public Brand() {DoubleBuffered=true;}
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var br=new SolidBrush(Theme.Lime))g.FillEllipse(br,0,5,44,44);
            using(var br=new SolidBrush(Theme.Ink))g.FillRectangle(br,9,20,27,12);
            g.FillEllipse(Brushes.White,14,24,4,4);g.FillEllipse(Brushes.White,27,24,4,4);
            using(var f=new Font("Segoe UI",20,FontStyle.Bold))using(var br=new SolidBrush(Color.White))g.DrawString("MaskMitra",f,br,56,0);
            using(var f=new Font("Segoe UI",9))using(var br=new SolidBrush(Color.FromArgb(180,192,204)))g.DrawString("A little privacy. Before you share.",f,br,59,36);
        }
    }
    sealed class EditorCanvas : Control {
        public Document Doc;public MarkKind Tool=MarkKind.Hide;public string Note="Look here";
        public event Action<Mark> Added;public event Action<int> Picked;public int Selected=-1;
        Bitmap rendered;float zoom=1;PointF offset=PointF.Empty;PointF start,finish,last;bool drawing,panning;
        public EditorCanvas() {DoubleBuffered=true;BackColor=Color.FromArgb(232,236,240);Cursor=Cursors.Cross;SetStyle(ControlStyles.Selectable,true);TabStop=true;}
        public void RefreshImage(bool reset) {if(rendered!=null)rendered.Dispose();rendered=Doc!=null&&Doc.Original!=null?Images.Render(Doc):null;if(reset){zoom=1;offset=PointF.Empty;}Invalidate();}
        public void Fit() {zoom=1;offset=PointF.Empty;Invalidate();}
        internal RectangleF ImageRect {
            get {if(rendered==null)return RectangleF.Empty;float fit=Math.Min((float)Math.Max(1,Width-70)/rendered.Width,(float)Math.Max(1,Height-70)/rendered.Height);float s=fit*zoom;return new RectangleF((Width-rendered.Width*s)/2+offset.X,(Height-rendered.Height*s)/2+offset.Y,rendered.Width*s,rendered.Height*s);}
        }
        PointF ToImage(Point point) {var r=ImageRect;return new PointF(Math.Max(0,Math.Min(rendered.Width,(point.X-r.X)*rendered.Width/r.Width)),Math.Max(0,Math.Min(rendered.Height,(point.Y-r.Y)*rendered.Height/r.Height)));}
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);var g=e.Graphics;
            if(rendered==null) {
                using(var f=new Font("Segoe UI",25,FontStyle.Bold))using(var br=new SolidBrush(Theme.Ink))using(var sf=new StringFormat{Alignment=StringAlignment.Center})g.DrawString("Share the moment.\nKeep the private bits.",f,br,new RectangleF(20,Height/2-110,Width-40,100),sf);
                using(var f=new Font("Segoe UI",11))using(var br=new SolidBrush(Theme.Muted))using(var sf=new StringFormat{Alignment=StringAlignment.Center})g.DrawString("Capture your screen, paste an image, or drop a file here.\nEverything is processed on this PC.",f,br,new RectangleF(25,Height/2+8,Width-50,70),sf);
                return;
            }
            var r=ImageRect;using(var shadow=new SolidBrush(Color.FromArgb(35,30,40,50)))g.FillRectangle(shadow,r.X+5,r.Y+7,r.Width,r.Height);
            g.InterpolationMode=zoom>2?InterpolationMode.NearestNeighbor:InterpolationMode.HighQualityBicubic;g.DrawImage(rendered,r);
            var saved=g.Save();g.SetClip(r);g.TranslateTransform(r.X,r.Y);float factor=r.Width/rendered.Width;g.ScaleTransform(factor,factor);
            for(int i=0;i<Doc.Suggestions.Count;i++) {
                var s=Doc.Suggestions[i];if(s.Decision==1&&i!=Selected)continue;
                Color color=i==Selected?Theme.Teal:s.Decision==0?Color.FromArgb(219,137,33):Color.FromArgb(101,131,128);
                using(var p=new Pen(color,(i==Selected?3:1.5f)/factor)){p.DashStyle=i==Selected?DashStyle.Solid:DashStyle.Dash;g.DrawRectangle(p,s.Box.X,s.Box.Y,s.Box.Width,s.Box.Height);}
            }
            if(drawing) {
                using(var p=new Pen(Tool==MarkKind.Hide?Theme.Ink:Color.FromArgb(219,75,45),2/factor)) {p.DashStyle=DashStyle.Dash;if(Tool==MarkKind.Arrow)g.DrawLine(p,start,finish);else {var box=Images.Rect(start,finish);g.DrawRectangle(p,box.X,box.Y,box.Width,box.Height);}}
            }g.Restore(saved);
            using(var f=new Font("Segoe UI",9))using(var br=new SolidBrush(Theme.Muted))g.DrawString("Mouse wheel: zoom   |   Middle-drag: pan   |   Right-click: select suggestion",f,br,14,Height-24);
        }
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);Focus();if(rendered==null)return;
            if(e.Button==MouseButtons.Middle){panning=true;last=e.Location;Capture=true;return;}
            if(!ImageRect.Contains(e.Location))return;
            start=finish=ToImage(e.Location);
            if(e.Button==MouseButtons.Right) {for(int i=0;i<Doc.Suggestions.Count;i++)if(Doc.Suggestions[i].Box.Contains(start)){if(Picked!=null)Picked(i);return;}return;}
            if(e.Button!=MouseButtons.Left)return;
            if(Tool==MarkKind.Note||Tool==MarkKind.Step){if(Tool==MarkKind.Note&&String.IsNullOrWhiteSpace(Note))return;if(Added!=null)Added(new Mark{Kind=Tool,Start=start,Text=Note,Number=Doc.Marks.Count(m=>m.Kind==MarkKind.Step)+1});return;}
            drawing=true;Capture=true;
        }
        protected override void OnMouseMove(MouseEventArgs e) {base.OnMouseMove(e);if(panning){offset.X+=e.X-last.X;offset.Y+=e.Y-last.Y;last=e.Location;Invalidate();}if(drawing){finish=ToImage(e.Location);Invalidate();}}
        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e);if(panning){panning=false;Capture=false;}if(!drawing)return;drawing=false;Capture=false;finish=ToImage(e.Location);var box=Images.Rect(start,finish);
            if((Tool==MarkKind.Arrow&&(Math.Abs(start.X-finish.X)>4||Math.Abs(start.Y-finish.Y)>4))||(Tool!=MarkKind.Arrow&&box.Width>=3&&box.Height>=3))if(Added!=null)Added(new Mark{Kind=Tool,Box=box,Start=start,End=finish});Invalidate();
        }
        protected override void OnMouseCaptureChanged(EventArgs e) {base.OnMouseCaptureChanged(e);if(!Capture){drawing=false;panning=false;Invalidate();}}
        protected override void OnMouseWheel(MouseEventArgs e) {base.OnMouseWheel(e);if(rendered==null)return;float old=zoom;zoom=Math.Max(0.5f,Math.Min(8,zoom*(e.Delta>0?1.2f:1/1.2f)));float ratio=zoom/old;offset=new PointF((offset.X+Width/2-e.X)*ratio-Width/2+e.X,(offset.Y+Height/2-e.Y)*ratio-Height/2+e.Y);Invalidate();}
        protected override void Dispose(bool disposing) {if(disposing&&rendered!=null)rendered.Dispose();base.Dispose(disposing);}
    }
    sealed class MainForm : Form {
        internal readonly Document doc=new Document();internal EditorCanvas canvas;
        internal ListBox suggestions;Label status,scanInfo,counts,selectedText;TextBox custom,note;Button scanButton,undoButton,reviewButton;
        readonly Dictionary<MarkKind,Button> toolButtons=new Dictionary<MarkKind,Button>();
        bool scanning,scanAgain,closing,capturing,testMode,hotkey;List<TextLine> lines=new List<TextLine>();
        [DllImport("user32.dll")]static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
        [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr hwnd,int id);
        public MainForm(bool test) {
            testMode=test;Text="MaskMitra — Review before you share";ClientSize=new Size(1280,800);MinimumSize=new Size(1120,740);StartPosition=FormStartPosition.CenterScreen;BackColor=Theme.Paper;ForeColor=Theme.Ink;Font=new Font("Segoe UI",9);AutoScaleMode=AutoScaleMode.Dpi;KeyPreview=true;AllowDrop=true;
            try {Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=Padding.Empty};root.RowStyles.Add(new RowStyle(SizeType.Absolute,90));root.RowStyles.Add(new RowStyle(SizeType.Absolute,94));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,34));Controls.Add(root);
            var header=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Ink,Margin=Padding.Empty,Padding=new Padding(22,17,18,10)};
            header.Controls.Add(new Brand{Dock=DockStyle.Left,Width=365});
            var actions=new FlowLayoutPanel{Dock=DockStyle.Right,Width=460,Padding=new Padding(0,9,0,0)};
            actions.Controls.Add(Theme.Button("Capture",async(s,e)=>await CaptureScreen(),true,105));actions.Controls.Add(Theme.Button("Open image",(s,e)=>Open(),false,105));actions.Controls.Add(Theme.Button("Paste image",(s,e)=>Paste(),false,110));actions.Controls.Add(Theme.Button("Try demo",(s,e)=>LoadDemo(),false,105));header.Controls.Add(actions);root.Controls.Add(header,0,0);
            var tools=new Panel{Dock=DockStyle.Fill,Padding=new Padding(22,12,18,6),Margin=Padding.Empty};
            var toolRow=new FlowLayoutPanel{Dock=DockStyle.Top,Height=43,WrapContents=false};
            AddTool(toolRow,MarkKind.Hide,"Hide",75);AddTool(toolRow,MarkKind.Arrow,"Arrow",78);AddTool(toolRow,MarkKind.Highlight,"Highlight",94);AddTool(toolRow,MarkKind.Note,"Text",70);AddTool(toolRow,MarkKind.Step,"Step",70);
            undoButton=Theme.Button("Undo",(s,e)=>{doc.Undo();RefreshDoc(false);},false,72);toolRow.Controls.Add(undoButton);
            toolRow.Controls.Add(Theme.Button("Fit",(s,e)=>canvas.Fit(),false,55));
            toolRow.Controls.Add(new Label{Text="Note",Width=46,Height=33,TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(10,0,0,0)});
            note=new TextBox{Text="Look here",Width=155,MaxLength=180,Margin=new Padding(0,6,10,0)};note.TextChanged+=(s,e)=>canvas.Note=note.Text;toolRow.Controls.Add(note);
            reviewButton=Theme.Button("Review & export",(s,e)=>Review(),true,149);toolRow.Controls.Add(reviewButton);
            tools.Controls.Add(new Label{Text="01  CAPTURE     /     02  HIDE & EXPLAIN     /     03  REVIEW & COPY",UseMnemonic=false,Dock=DockStyle.Bottom,Height=23,ForeColor=Theme.Muted,Font=new Font("Segoe UI",8,FontStyle.Bold)});tools.Controls.Add(toolRow);root.Controls.Add(tools,0,1);
            var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(18,0,18,0),Margin=Padding.Empty};body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,320));
            canvas=new EditorCanvas{Dock=DockStyle.Fill,Doc=doc,Margin=new Padding(0,0,14,0)};canvas.Added+=m=>{if(doc.Original==null)return;doc.Remember();doc.Marks.Add(m);RefreshDoc(false);};canvas.Picked+=i=>suggestions.SelectedIndex=i;body.Controls.Add(canvas,0,0);
            var side=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=10,BackColor=Color.White,Padding=new Padding(14,12,14,8),Margin=Padding.Empty};
            float[] heights={30,45,42,30,0,38,46,34,36,70};for(int i=0;i<heights.Length;i++)side.RowStyles.Add(new RowStyle(i==4?SizeType.Percent:SizeType.Absolute,i==4?100:heights[i]));
            side.Controls.Add(Theme.Label("PRIVATE DETAILS",28,true),0,0);
            scanInfo=Theme.Label("Offline suggestions. You choose what stays.",44);scanInfo.Dock=DockStyle.Fill;side.Controls.Add(scanInfo,0,1);
            var scanRow=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};scanButton=Theme.Button("Scan again",async(s,e)=>await Scan(),false,116);scanRow.Controls.Add(scanButton);scanRow.Controls.Add(Theme.Button("Hide suggested",(s,e)=>DecideAll(),true,139));side.Controls.Add(scanRow,0,2);
            counts=Theme.Label("No image loaded",28,true);side.Controls.Add(counts,0,3);
            suggestions=new ListBox{Dock=DockStyle.Fill,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=51,BorderStyle=BorderStyle.None,IntegralHeight=false,BackColor=Color.White,Font=Font};suggestions.DrawItem+=DrawSuggestion;suggestions.SelectedIndexChanged+=(s,e)=>{canvas.Selected=suggestions.SelectedIndex;canvas.Invalidate();UpdateSelected();};side.Controls.Add(suggestions,0,4);
            var decisions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};decisions.Controls.Add(Theme.Button("Hide",(s,e)=>Decide(1,false),true,70));decisions.Controls.Add(Theme.Button("Keep",(s,e)=>Decide(2,false),false,70));decisions.Controls.Add(Theme.Button("Hide matches",(s,e)=>Decide(1,true),false,123));side.Controls.Add(decisions,0,5);
            selectedText=Theme.Label("Select a suggestion to hide or keep it.",44);selectedText.Dock=DockStyle.Fill;side.Controls.Add(selectedText,0,6);
            side.Controls.Add(Theme.Label("FIND YOUR OWN TEXT",30,true),0,7);
            var findRow=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};custom=new TextBox{Width=194,MaxLength=500,Margin=new Padding(0,5,6,0)};findRow.Controls.Add(custom);findRow.Controls.Add(Theme.Button("Find",(s,e)=>FindCustom(),false,72));side.Controls.Add(findRow,0,8);
            side.Controls.Add(Theme.Label("Separate names or words with ;\nDetection can miss details. Always review.\nManual Hide works without a scan.",68),0,9);body.Controls.Add(side,1,0);root.Controls.Add(body,0,2);
            status=new Label{Text="Ready  •  Local processing  •  No image history",Dock=DockStyle.Fill,Padding=new Padding(22,0,10,0),TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Muted,Font=new Font("Segoe UI",8.5f)};root.Controls.Add(status,0,3);
            DragEnter+=(s,e)=>e.Effect=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;
            DragDrop+=(s,e)=>{var files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files!=null&&files.Length>0)LoadFile(files[0]);};
            SetTool(MarkKind.Hide);RefreshDoc(false);
            if(!testMode)Shown+=(s,e)=>{hotkey=RegisterHotKey(Handle,187,0x4000|0x2|0x4,(uint)Keys.M);status.Text=hotkey?"Ready  •  Ctrl+Shift+M captures an area while MaskMitra is open  •  Local processing":"Ready  •  Capture button available; Ctrl+Shift+M is in use by another app";};
        }
        void AddTool(FlowLayoutPanel row,MarkKind kind,string name,int width) {var button=Theme.Button(name,(s,e)=>SetTool(kind),false,width);row.Controls.Add(button);toolButtons.Add(kind,button);}
        void SetTool(MarkKind kind) {if(canvas!=null)canvas.Tool=kind;foreach(var p in toolButtons){p.Value.BackColor=p.Key==kind?Theme.Ink:Color.White;p.Value.ForeColor=p.Key==kind?Color.White:Theme.Ink;}if(status!=null)status.Text=kind==MarkKind.Hide?"Hide: drag a rectangle over private details. Right-click a suggestion to select it.":kind==MarkKind.Note?"Text: edit the Note field, then click the image to place it.":kind==MarkKind.Step?"Step: click the image to add a numbered marker.":"Drag on the image to add an "+kind.ToString().ToLowerInvariant()+". Ctrl+Z undoes the last edit.";}
        internal void SetImage(Bitmap image,string path,bool scan) {doc.Load(image,path);lines.Clear();canvas.Selected=-1;scanInfo.Text="Image loaded. Ready for offline scanning.";RefreshDoc(true);if(scan){var ignored=Scan();}}
        void LoadFile(string path) {try{var b=Images.Open(path);SetImage(b,Path.GetFullPath(path),true);}catch(Exception ex){Notice("Could not open this image.\n\n"+ex.Message);}}
        void Open() {using(var d=new OpenFileDialog{Title="Open an image",Filter="Images|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*"})if(d.ShowDialog(this)==DialogResult.OK)LoadFile(d.FileName);}
        void Paste() {try{if(!Clipboard.ContainsImage()){Notice("Copy an image first, then choose Paste image.");return;}using(var image=Clipboard.GetImage()){if(image!=null)SetImage(Images.CleanCopy(image),null,true);}}catch(Exception ex){Notice("Could not read the clipboard.\n\n"+ex.Message);}}
        void LoadDemo() {SetImage(Tests.Demo(),null,true);status.Text="Demo image  •  All names and details are synthetic  •  Try Hide matches, arrows and export";}
        internal async Task Scan() {
            if(doc.Original==null||closing)return;if(scanning){scanAgain=true;return;}scanning=true;scanButton.Enabled=false;int version=doc.Version;scanInfo.Text="Reading text on this PC…";
            using(var copy=Images.CleanCopy(doc.Original)) {
                try {var result=await Task.Run(()=>LocalOcr.Read(copy));if(closing||version!=doc.Version)return;lines=result.Lines;MergeSuggestions();scanInfo.Text=result.Language+" • scan complete\nReview each suggestion before sharing.";}
                catch(Exception ex){if(!closing&&version==doc.Version){scanInfo.Text="Scan unavailable • Manual Hide is ready";status.Text=ex.Message;}}
                finally {scanning=false;if(!closing){scanButton.Enabled=doc.Original!=null;RefreshDoc(false);if(scanAgain){scanAgain=false;BeginInvoke(new Action(async()=>await Scan()));}}}
            }
        }
        void MergeSuggestions() {
            var next=Detection.Find(lines,custom.Text,doc.Original.Size);
            foreach(var candidate in next){var old=doc.Suggestions.FirstOrDefault(s=>Detection.Key(s.Value)==Detection.Key(candidate.Value)&&Math.Abs(s.Box.X-candidate.Box.X)<3&&Math.Abs(s.Box.Y-candidate.Box.Y)<3);if(old!=null)candidate.Decision=old.Decision;}
            // Keep old custom redactions even if the search terms change.
            foreach(var old in doc.Suggestions.Where(s=>s.Decision==1))if(!next.Any(n=>n.Box==old.Box))next.Add(old.Clone());
            doc.Remember();doc.Suggestions=next;RefreshDoc(false);
        }
        void FindCustom(){if(doc.Original==null){Notice("Open an image first.");return;}if(lines.Count==0){Notice("Run an offline scan first. If text cannot be read, use Manual Hide.");return;}MergeSuggestions();}
        internal void Decide(int choice,bool matching) {
            int index=suggestions.SelectedIndex;if(index<0||index>=doc.Suggestions.Count)return;doc.Remember();string key=Detection.Key(doc.Suggestions[index].Value);
            for(int i=0;i<doc.Suggestions.Count;i++)if(i==index||(matching&&Detection.Key(doc.Suggestions[i].Value)==key))doc.Suggestions[i].Decision=choice;RefreshDoc(false);
        }
        void DecideAll(){if(doc.Suggestions.Count==0)return;doc.Remember();foreach(var s in doc.Suggestions)if(s.Decision==0)s.Decision=1;RefreshDoc(false);}
        internal void RefreshDoc(bool reset) {
            int index=suggestions.SelectedIndex;suggestions.BeginUpdate();suggestions.Items.Clear();foreach(var s in doc.Suggestions)suggestions.Items.Add(s);if(index>=0&&index<suggestions.Items.Count)suggestions.SelectedIndex=index;suggestions.EndUpdate();canvas.Selected=suggestions.SelectedIndex;canvas.RefreshImage(reset);
            int hidden=doc.Suggestions.Count(s=>s.Decision==1),pending=doc.Suggestions.Count(s=>s.Decision==0);counts.Text=doc.Original==null?"No image loaded":doc.Suggestions.Count==0?"No suggestions to review":hidden+" hidden  /  "+pending+" to review";
            undoButton.Enabled=doc.CanUndo;reviewButton.Enabled=doc.Original!=null;scanButton.Enabled=doc.Original!=null&&!scanning;UpdateSelected();
        }
        void UpdateSelected(){int i=suggestions.SelectedIndex;selectedText.Text=i<0?"Select a suggestion to hide or keep it.":doc.Suggestions[i].Decision==1?"Hidden in export. Use Keep to show it again.":doc.Suggestions[i].Decision==2?"Kept visible. Hide matches affects matching detected text.":"Review this item. Hide matches covers matching detected text.";}
        void DrawSuggestion(object sender,DrawItemEventArgs e) {
            if(e.Index<0||e.Index>=doc.Suggestions.Count)return;var s=doc.Suggestions[e.Index];bool selected=(e.State&DrawItemState.Selected)!=0;
            using(var br=new SolidBrush(selected?Color.FromArgb(231,243,239):Color.White))e.Graphics.FillRectangle(br,e.Bounds);
            Color dot=s.Decision==1?Theme.Teal:s.Decision==2?Theme.Muted:Color.FromArgb(209,137,40);using(var br=new SolidBrush(dot))e.Graphics.FillEllipse(br,e.Bounds.X+6,e.Bounds.Y+10,7,7);
            string state=s.Decision==1?"HIDDEN":s.Decision==2?"KEEP":"REVIEW";
            using(var f=new Font("Segoe UI",8,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,s.Kind+"  ·  "+state,f,new Rectangle(e.Bounds.X+20,e.Bounds.Y+4,e.Bounds.Width-24,19),dot,TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics,s.Value,Font,new Rectangle(e.Bounds.X+20,e.Bounds.Y+24,e.Bounds.Width-24,22),Theme.Ink,TextFormatFlags.EndEllipsis);
        }
        void Review() {if(doc.Original==null)return;using(var rendered=Images.Render(doc))using(var dialog=new ExportForm(rendered,doc.SourcePath,doc.Suggestions.Count(s=>s.Decision==0)))dialog.ShowDialog(this);}
        async Task CaptureScreen() {
            if(capturing||!Enabled)return;capturing=true;var previous=WindowState;Hide();
            try {await Task.Delay(220);Rectangle area=SystemInformation.VirtualScreen;Images.Validate(area.Width,area.Height);using(var screen=new Bitmap(area.Width,area.Height)) {
                using(var g=Graphics.FromImage(screen))g.CopyFromScreen(area.Location,Point.Empty,area.Size);
                using(var overlay=new CaptureForm(screen,area)){if(overlay.ShowDialog()==DialogResult.OK&&overlay.Selection.Width>2&&overlay.Selection.Height>2){var selection=screen.Clone(overlay.Selection,PixelFormat.Format24bppRgb);SetImage(selection,null,true);}}
            }}catch(Exception ex){Notice("Screen capture did not complete.\n\n"+ex.Message);}finally{Show();WindowState=previous==FormWindowState.Minimized?FormWindowState.Normal:previous;Activate();capturing=false;}
        }
        void Notice(string text){MessageBox.Show(this,text,"MaskMitra",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        protected override bool ProcessCmdKey(ref Message msg,Keys key) {
            if(key==(Keys.Control|Keys.O)){Open();return true;}
            if(key==(Keys.Control|Keys.Shift|Keys.M)){var ignored=CaptureScreen();return true;}
            if(!(ActiveControl is TextBoxBase)) {
                if(key==(Keys.Control|Keys.V)){Paste();return true;}
                if(key==(Keys.Control|Keys.Z)){doc.Undo();RefreshDoc(false);return true;}
                if(key==(Keys.Control|Keys.E)){Review();return true;}
            }return base.ProcessCmdKey(ref msg,key);
        }
        protected override void WndProc(ref Message m){if(m.Msg==0x312&&m.WParam.ToInt32()==187){var ignored=CaptureScreen();}base.WndProc(ref m);}
        protected override void OnFormClosed(FormClosedEventArgs e){closing=true;if(hotkey)UnregisterHotKey(Handle,187);doc.Dispose();base.OnFormClosed(e);}
        protected override void Dispose(bool disposing){if(disposing){closing=true;doc.Dispose();}base.Dispose(disposing);}
    }
    sealed class ExportForm : Form {
        public ExportForm(Bitmap image,string source,int pending) {
            Text="MaskMitra — Final review";ClientSize=new Size(1040,760);MinimumSize=new Size(800,560);StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Paper;Font=new Font("Segoe UI",10);
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new Padding(20)};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,66));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,65));Controls.Add(layout);
            layout.Controls.Add(new Label{Text="One last look before it leaves your screen.\n"+(pending>0?pending+" suggestions are still undecided and remain visible. ":"")+"Check for anything the scan missed. Only this rendered image is exported.",Dock=DockStyle.Fill,ForeColor=Theme.Ink},0,0);
            var picture=new PictureBox{Dock=DockStyle.Fill,Image=image,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.FromArgb(229,234,238)};layout.Controls.Add(picture,0,1);
            var row=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,14,0,0)};
            var feedback=new Label{Text=image.Width+" × "+image.Height+" px  •  Flattened PNG",AutoSize=false,Width=310,Height=38,TextAlign=ContentAlignment.MiddleLeft};
            row.Controls.Add(Theme.Button("Copy clean image",(s,e)=>{try{Clipboard.SetDataObject(new DataObject(DataFormats.Bitmap,image),true,5,150);feedback.Text="Copied. Paste it into your message.";}catch(Exception ex){MessageBox.Show(this,"Clipboard busy. Please try again.\n"+ex.Message,"MaskMitra");}},true,163));
            row.Controls.Add(Theme.Button("Save PNG",(s,e)=>{using(var d=new SaveFileDialog{Title="Save the clean image",Filter="PNG image|*.png",FileName="MaskMitra-clean.png",DefaultExt="png",AddExtension=true,OverwritePrompt=true})if(d.ShowDialog(this)==DialogResult.OK)try{Images.Save(image,d.FileName,source);feedback.Text="Saved your clean image.";}catch(Exception ex){MessageBox.Show(this,ex.Message,"MaskMitra");}},false,120));
            row.Controls.Add(Theme.Button("Back to editing",(s,e)=>Close(),false,143));row.Controls.Add(feedback);layout.Controls.Add(row,0,2);
            FormClosed+=(s,e)=>picture.Image=null;
        }
    }
    sealed class CaptureForm : Form {
        readonly Bitmap image;Point start,end;bool dragging;public Rectangle Selection { get; private set; }
        public CaptureForm(Bitmap source,Rectangle area) {image=source;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;Bounds=area;TopMost=true;ShowInTaskbar=false;DoubleBuffered=true;Cursor=Cursors.Cross;KeyPreview=true;KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape){DialogResult=DialogResult.Cancel;Close();}};}
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.DrawImageUnscaled(image,0,0);Rectangle r=Rectangle.Round(Images.Rect(start,end));using(var shade=new SolidBrush(Color.FromArgb(135,11,17,26))) {
                if(!dragging)e.Graphics.FillRectangle(shade,ClientRectangle);
                else{e.Graphics.FillRectangle(shade,0,0,Width,r.Top);e.Graphics.FillRectangle(shade,0,r.Bottom,Width,Math.Max(0,Height-r.Bottom));e.Graphics.FillRectangle(shade,0,r.Top,r.Left,r.Height);e.Graphics.FillRectangle(shade,r.Right,r.Top,Math.Max(0,Width-r.Right),r.Height);}
            }
            if(dragging)using(var p=new Pen(Theme.Lime,2))e.Graphics.DrawRectangle(p,r);
            using(var f=new Font("Segoe UI",14,FontStyle.Bold))e.Graphics.DrawString("Drag to capture an area   •   Esc to cancel",f,Brushes.White,35,30);
        }
        protected override void OnMouseDown(MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;start=end=e.Location;dragging=true;Capture=true;Invalidate();}
        protected override void OnMouseMove(MouseEventArgs e){if(!dragging)return;end=new Point(Math.Max(0,Math.Min(Width,e.X)),Math.Max(0,Math.Min(Height,e.Y)));Invalidate();}
        protected override void OnMouseUp(MouseEventArgs e){if(!dragging||e.Button!=MouseButtons.Left)return;end=new Point(Math.Max(0,Math.Min(Width,e.X)),Math.Max(0,Math.Min(Height,e.Y)));Selection=Rectangle.Intersect(Rectangle.Round(Images.Rect(start,end)),ClientRectangle);Capture=false;DialogResult=Selection.Width>2&&Selection.Height>2?DialogResult.OK:DialogResult.Cancel;Close();}
    }
}
